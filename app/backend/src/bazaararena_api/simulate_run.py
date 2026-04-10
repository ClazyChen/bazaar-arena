from __future__ import annotations

import json
import os
import secrets
import sys
import subprocess
import tempfile
from datetime import datetime, timezone
from pathlib import Path

from bazaararena_api.db import get_connection, repo_root

TIER_TO_ENGINE = ("bronze", "silver", "gold", "diamond", "legendary")


def random_sim_seed() -> int:
    """供 HTTP 在未指定 seed 时使用；始终写入 job.payload.seed，便于与 CLI 本地复现一致。"""
    return secrets.randbelow(1 << 31)


def default_cli_path() -> Path | None:
    """默认使用仓库根目录 bin/ 下的 CLI（与 engine/CMakeLists.txt 中 RUNTIME_OUTPUT_DIRECTORY 一致）。"""
    env = os.environ.get("BAZAARARENA_CLI")
    if env:
        p = Path(env)
        return p if p.is_file() else None
    root = repo_root()
    name = "bazaararena_cli.exe" if sys.platform == "win32" else "bazaararena_cli"
    canonical = root / "bin" / name
    return canonical if canonical.is_file() else None


# 每个 cli 路径只校验一次（与 C++ `PrintVersion` 中的 contract 字符串对齐）
_cli_identity_ok: set[str] = set()
# 与上面 key 一致：供 HTTP 回传，便于 Web 端确认未指向陈旧 exe
_cli_version_line: dict[str, str] = {}


def cached_cli_version_line(cli: Path | None) -> str | None:
    if cli is None:
        return None
    return _cli_version_line.get(str(cli.resolve()))


def ensure_cli_identity(cli: Path) -> None:
    """
    调用 `bazaararena_cli --version`：若返回 0，则必须含 contract=1（与 engine/cli/main.cpp 一致）。
    若返回非 0，视为旧版无 --version，不拦截。
    用于在跑模拟前发现「同名但非对战 JSON CLI」的可执行文件。
    """
    key = str(cli.resolve())
    if key in _cli_identity_ok:
        return
    try:
        proc = subprocess.run(
            [str(cli), "--version"],
            cwd=str(cli.parent),
            capture_output=True,
            text=True,
            timeout=15,
        )
    except OSError as e:
        raise RuntimeError(f"无法执行 {cli} --version：{e}") from e
    merged = ((proc.stdout or "") + "\n" + (proc.stderr or "")).strip()
    if proc.returncode != 0:
        _cli_identity_ok.add(key)
        return
    if "contract=1" not in merged or "simulate+json" not in merged:
        raise RuntimeError(
            "bazaararena_cli --version 输出不符合对战模拟契约（应含 `contract=1` 与 `simulate+json`）。"
            f"当前输出（截断）：{merged[:900]!r}。"
            f"说明 {cli} 不是本仓库 engine/cli/main.cpp 编出的可执行文件（常见：同名占位程序或其它工程产物）。"
            "请重新编译：`cmake -S engine -B <build-dir> --build <build-dir> --config Release --target bazaararena_cli`"
            "（产物在仓库根 `bin/`），或设置 BAZAARARENA_CLI 指向正确的可执行文件。"
        )
    first = merged.splitlines()[0].strip() if merged else ""
    _cli_version_line[key] = first[:700] if first else ""
    _cli_identity_ok.add(key)


def _deck_slots(conn, deck_id: int) -> list[dict[str, object]]:
    cur = conn.execute(
        "SELECT position, item_name, tier FROM deck_slots WHERE deck_id = ? ORDER BY position",
        (deck_id,),
    )
    return [
        {"position": int(r["position"]), "item_name": r["item_name"], "tier": int(r["tier"])}
        for r in cur.fetchall()
    ]


def _deck_repro_snapshot(conn, deck_id: int) -> dict[str, object]:
    row = conn.execute(
        "SELECT id, name, player_level FROM decks WHERE id = ?",
        (deck_id,),
    ).fetchone()
    if not row:
        raise ValueError(f"deck not found: {deck_id}")
    slots = _deck_slots(conn, deck_id)
    return {
        "deckId": int(row["id"]),
        "name": str(row["name"]),
        "playerLevel": int(row["player_level"]),
        "slots": slots,
    }


def build_cli_repro_document(
    deck_id_0: int,
    deck_id_1: int,
    seed: int,
    *,
    debug_level: str = "detailed",
    max_events: int = 50000,
) -> dict[str, object]:
    """
    与 bazaararena_cli --input 兼容的 JSON（schemaVersion/mode/payload），
    并附加 cliReproMeta（卡组快照等）；引擎解析时忽略未知顶层键。
    """
    conn = get_connection()
    try:
        d0 = _deck_repro_snapshot(conn, deck_id_0)
        d1 = _deck_repro_snapshot(conn, deck_id_1)
    finally:
        conn.close()

    job = build_simulate_job(
        deck_id_0,
        deck_id_1,
        seed,
        debug_level=debug_level,
        max_events=max_events,
    )
    exported_at = datetime.now(timezone.utc).replace(microsecond=0).isoformat()
    out: dict[str, object] = {**job}
    out["cliReproMeta"] = {
        "exportedAt": exported_at,
        "deck0_as_side0": d0,
        "deck1_as_side1": d1,
        "hint": "可直接作为 bazaararena_cli --input；cliReproMeta 仅供查阅。",
    }
    return out


_ALLOWED_DEBUG = frozenset({"none", "summary", "detailed"})


def build_simulate_job(
    deck_id_0: int,
    deck_id_1: int,
    seed: int,
    *,
    debug_level: str = "detailed",
    max_events: int = 50000,
) -> dict[str, object]:
    conn = get_connection()
    try:
        sides_payload: list[dict[str, object]] = []
        for did in (deck_id_0, deck_id_1):
            row = conn.execute(
                "SELECT id, player_level FROM decks WHERE id = ?",
                (did,),
            ).fetchone()
            if not row:
                raise ValueError(f"deck not found: {did}")
            level = int(row["player_level"])
            slots = _deck_slots(conn, did)
            items: list[dict[str, str]] = []
            for s in slots:
                tier_i = int(s["tier"])
                if tier_i < 0 or tier_i > 4:
                    raise ValueError(f"invalid tier {tier_i} for deck {did}")
                items.append(
                    {
                        "key": str(s["item_name"]),
                        "tier": TIER_TO_ENGINE[tier_i],
                    }
                )
            sides_payload.append(
                {
                    "sideId": len(sides_payload),
                    "level": level,
                    "items": items,
                }
            )
    finally:
        conn.close()

    if debug_level not in _ALLOWED_DEBUG:
        raise ValueError(f"debug_level must be one of {sorted(_ALLOWED_DEBUG)}")
    dbg_enabled = debug_level != "none"
    dbg_lv = debug_level if dbg_enabled else "none"

    payload: dict[str, object] = {
        "allowTie": True,
        "debug": {"enabled": dbg_enabled, "level": dbg_lv, "maxEvents": int(max_events)},
        "sides": sides_payload,
        "seed": int(seed),
    }

    return {
        "schemaVersion": 1,
        "jobId": f"http-{deck_id_0}-vs-{deck_id_1}",
        "mode": "simulate",
        "payload": payload,
    }


def run_simulate_json(job: dict[str, object], timeout_sec: float = 120.0) -> dict[str, object]:
    cli = default_cli_path()
    if cli is None:
        raise FileNotFoundError(
            "未找到 bazaararena_cli：请在仓库根执行 "
            "`cmake -S engine -B <build-dir> && cmake --build <build-dir> --config Release --target bazaararena_cli`"
            "（可执行文件应出现在 `<repo>/bin/`），或设置环境变量 BAZAARARENA_CLI 指向可执行文件。"
        )

    ensure_cli_identity(cli)

    with tempfile.TemporaryDirectory() as td:
        td_path = Path(td)
        in_path = td_path / "in.json"
        out_path = td_path / "out.json"
        in_path.write_text(json.dumps(job, ensure_ascii=False), encoding="utf-8")
        try:
            proc = subprocess.run(
                [str(cli), "--input", str(in_path), "--output", str(out_path)],
                cwd=str(cli.parent),
                capture_output=True,
                text=True,
                timeout=timeout_sec,
            )
        except subprocess.TimeoutExpired:
            raise RuntimeError("bazaararena_cli timed out") from None
        except OSError as e:
            raise RuntimeError(
                f"无法启动 bazaararena_cli（{cli}）：{e}。若已编译，请设置环境变量 BAZAARARENA_CLI 指向可执行文件。"
            ) from e

        if not out_path.is_file():
            err_parts = [
                f"exit={proc.returncode}",
                f"cli={cli}",
            ]
            if proc.stderr and proc.stderr.strip():
                err_parts.append(f"stderr={proc.stderr.strip()[:4000]}")
            if proc.stdout and proc.stdout.strip():
                err_parts.append(f"stdout={proc.stdout.strip()[:2000]}")
            wrong_binary_hint = ""
            if proc.returncode == 0 and "items=" in (proc.stdout or ""):
                wrong_binary_hint = (
                    " 当前现象：退出码为 0 但没有生成 --output 指定的文件，且标准输出像「版本号 + 物品列表」。"
                    "这说明该路径下的可执行文件很可能不是本仓库 engine/cli/main.cpp 编出来的对战模拟器"
                    "（同名文件被其它程序覆盖、或从未用当前源码重编）。"
                    "请在本机重新编译："
                    "`cmake -S engine -B <build-dir> && cmake --build <build-dir> --config Release --target bazaararena_cli`"
                    "（产物在仓库根 `bin/`），或设置环境变量 BAZAARARENA_CLI 指向正确的 bazaararena_cli。"
                )
            elif proc.returncode == 0:
                wrong_binary_hint = (
                    " 退出码为 0 但未写出输出文件：请确认 BAZAARARENA_CLI 指向的是"
                    "本仓库编译的 bazaararena_cli（应支持 --input/--output 并写出 JSON）。"
                )
            raise RuntimeError(
                "bazaararena_cli 未写出输出文件（out.json）。"
                + wrong_binary_hint
                + " 其它常见原因：缺少 MSVC 运行库导致进程异常、输入路径无法读取。"
                + " 详情：" + " | ".join(err_parts)
            )

        try:
            raw = out_path.read_text(encoding="utf-8")
            return json.loads(raw)
        except json.JSONDecodeError as e:
            raise RuntimeError(f"bazaararena_cli 输出不是合法 JSON：{e}") from e
