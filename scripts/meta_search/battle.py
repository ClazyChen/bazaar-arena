# -*- coding: utf-8 -*-
"""对战评估层：可复现、带持久缓存、自适应预算的系列赛。

设计要点（对应 docs/bazaar-meta-evidence.md §2.3 / §3.3）：
- 每局对战显式 seed，结果可逐帧复现（CLI mode=simulate 本身确定）。
- 结果持久缓存（JSONL），同一 (卡组A, 卡组B, seed) 永不重复评估。
- 系列赛左右各半局数，消除 side 槽位偏差。
- 自适应预算：分批对战，每批后计算 Wilson 置信区间；
  CI 半宽 ≤ 容差即停（分带思想：一面倒的对局少量局即停，预算集中在均势对局）。
"""

from __future__ import annotations

import concurrent.futures as cf
import json
import math
import subprocess
import sys
import tempfile
import threading
from dataclasses import dataclass
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent
DEFAULT_CLI = REPO / "bin" / ("bazaararena_cli.exe" if sys.platform == "win32" else "bazaararena_cli")
DEFAULT_META = REPO / "bin" / ("bazaararena_meta.exe" if sys.platform == "win32" else "bazaararena_meta")
DEFAULT_CACHE = REPO / "out" / "meta_search" / "battle_cache_v2.jsonl"


class MetaServer:
    """bazaararena_meta --serve 常驻进程包装：整个管线运行期间一次启动。

    请求/应答为逐行 JSONL（顺序一问一答，无线程交错）。线程安全由调用方保证
    （当前管线均为串行调用；并发场景请加锁）。
    """

    def __init__(self, level: int = 8, hero: str = "Mak", meta: Path = DEFAULT_META):
        import threading

        self._lock = threading.Lock()  # 一问一答协议串行化（多线程调用方安全）
        self.proc = subprocess.Popen(
            [str(meta), "--serve", "--level", str(level), "--hero", hero],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            text=True, encoding="utf-8", errors="replace",
            cwd=str(REPO), bufsize=1,
        )

    def play(self, key: str, names_a: list[str], names_b: list[str], seeds: list[int]) -> list[int]:
        assert self.proc.stdin and self.proc.stdout
        with self._lock:
            self.proc.stdin.write(json.dumps(
                {"id": key, "a": names_a, "b": names_b, "seeds": seeds}, ensure_ascii=False) + "\n")
            self.proc.stdin.flush()
            line = self.proc.stdout.readline()
        if not line:
            raise RuntimeError("meta server closed stdout")
        return json.loads(line)["results"]

    def play_adaptive_wave(self, requests: list[tuple[str, list[str], list[str]]],
                          params: dict, workers: int = 0) -> list[dict]:
        """自适应波次（C++ 内卷）：一批卡组对一次提交，C++ 逐波加局至收敛，单行返回。

        requests: [(key, names_a, names_b)]；params: {ci_tol, batch, max_games,
        sign_margin, base_seed}。侧平衡约定为交替换侧（见 engine/meta/main.cpp）。
        """
        assert self.proc.stdin and self.proc.stdout
        payload: dict = {
            "battles": [{"id": key, "a": na, "b": nb, "seeds": [0]}
                        for key, na, nb in requests],
            "adaptive": params,
        }
        if workers > 0:
            payload["workers"] = workers
        self.proc.stdin.write(json.dumps(payload, ensure_ascii=False) + "\n")
        self.proc.stdin.flush()
        line = self.proc.stdout.readline()
        if not line:
            raise RuntimeError("meta server closed stdout (adaptive wave)")
        return json.loads(line)["adaptive"]

    def play_wave(self, requests: list[tuple[str, list[str], list[str], list[int]]],
                 workers: int = 0) -> dict[str, list[int]]:
        """波次分派：一批请求一行提交，C++ 内部打散到全部线程，单行返回全部结果。"""
        assert self.proc.stdin and self.proc.stdout
        payload: dict = {"battles": [
            {"id": key, "a": na, "b": nb, "seeds": seeds}
            for key, na, nb, seeds in requests
        ]}
        if workers > 0:
            payload["workers"] = workers
        self.proc.stdin.write(json.dumps(payload, ensure_ascii=False) + "\n")
        self.proc.stdin.flush()
        line = self.proc.stdout.readline()
        if not line:
            raise RuntimeError("meta server closed stdout (wave)")
        out = json.loads(line)["wave"]
        return {r["id"]: r["results"] for r in out}

    def close(self):
        if self.proc.stdin:
            self.proc.stdin.close()
        self.proc.wait(timeout=10)

    _shared: "MetaServer | None" = None

    @classmethod
    def shared(cls, level: int = 8) -> "MetaServer":
        if cls._shared is None:
            import atexit

            cls._shared = cls(level=level)
            atexit.register(cls._shared.close)
        return cls._shared


def run_batch(
    requests: list[tuple[str, list[str], list[str], list[int]]],
    level: int,
    *,
    workers: int = 0,
    meta: Path = DEFAULT_META,
    work_dir: Path | None = None,
) -> dict[str, list[int]]:
    """批量对战（bazaararena_meta，C++ 层）：单进程执行全部 (卡组对, seed) 组合。

    requests: [(key, names_a, names_b, seeds)] —— names 为展示名列表（可含魂石变体），
    物品规则（quest 覆写/overridable/变体）全部由 C++ 侧按 GDF 规则处理，
    Python 不再复刻任何规则（见 engine/meta/main.cpp）。
    返回 key → 每 seed 结果（0=A胜/1=B胜/2=平局），与 CLI allowTie=true 逐局一致。
    """
    if not requests:
        return {}
    job = {
        "level": level,
        "workers": workers,
        "battles": [
            {"id": key, "a": names_a, "b": names_b, "seeds": seeds}
            for key, names_a, names_b, seeds in requests
        ],
    }
    wd = Path(work_dir) if work_dir else Path(tempfile.gettempdir())
    inp = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w", suffix=".json", prefix="meta_batch_", dir=wd, delete=False, encoding="utf-8"
        ) as f:
            json.dump(job, f, ensure_ascii=False)
            inp = Path(f.name)
        outp = inp.with_suffix(".jsonl")
        proc = subprocess.run(
            [str(meta), "--input", str(inp), "--output", str(outp)],
            capture_output=True, text=True, encoding="utf-8", errors="replace",
            cwd=str(REPO),
        )
        if proc.returncode != 0:
            raise RuntimeError(f"bazaararena_meta failed: {proc.stdout} {proc.stderr}")
        out: dict[str, list[int]] = {}
        for line in outp.read_text(encoding="utf-8").splitlines():
            if line.strip():
                r = json.loads(line)
                out[r["id"]] = r["results"]
        return out
    finally:
        if inp is not None:
            inp.unlink(missing_ok=True)
            inp.with_suffix(".jsonl").unlink(missing_ok=True)


def series_batch(
    key_a: str,
    names_a: list[str],
    key_b: str,
    names_b: list[str],
    level: int,
    games: int,
    *,
    base_seed: int = 1000,
    cache: BattleCache | None = None,
    meta: Path = DEFAULT_META,
) -> SeriesResult:
    """签名版系列赛（C++ 批量端点）。与 series() 语义一致：左右各半、平局 0.5。

    names_* 为展示名列表；物品规则全部由 bazaararena_meta（GDF 规则）处理。
    缓存键与 series() 相同（key+seed），两种接口结果互通（已验证逐局一致）。
    """
    seeds = [base_seed + i for i in range(games)]
    half = games // 2

    missing: list[tuple[int, int, list[str], list[str]]] = []  # (i, seed, side0, side1)
    for i, seed in enumerate(seeds):
        swapped = i >= half
        ka, kb = (key_b, key_a) if swapped else (key_a, key_b)
        if cache and cache.get(ka, kb, seed) is not None:
            continue
        na, nb = (names_b, names_a) if swapped else (names_a, names_b)
        missing.append((i, seed, na, nb))
    batch_out: dict[str, list[int]] = {}
    if missing:
        # 常驻服务；按 side 顺序分组合并 seeds（每方向至多一次往返）
        groups: dict[tuple, list[tuple[int, int]]] = {}
        for i, seed, na, nb in missing:
            groups.setdefault((tuple(na), tuple(nb)), []).append((i, seed))
        server = MetaServer.shared(level)
        for (na, nb), items in groups.items():
            results = server.play("batch", list(na), list(nb), [s for _, s in items])
            for (i, seed), r in zip(items, results):
                batch_out[f"g{i}"] = [r]
        if cache:
            for i, seed, na, nb in missing:
                swapped = i >= half
                ka, kb = (key_b, key_a) if swapped else (key_a, key_b)
                cache.put(ka, kb, seed, batch_out[f"g{i}"][0])

    wins_a = wins_b = ties = 0
    for i, seed in enumerate(seeds):
        swapped = i >= half
        ka, kb = (key_b, key_a) if swapped else (key_a, key_b)
        r = cache.get(ka, kb, seed) if cache else batch_out.get(f"g{i}", [None])[0]
        if r is None:
            raise RuntimeError(f"missing battle result for {ka}|{kb} seed={seed}")
        if r == 2:
            ties += 1
        elif (r == 0) != swapped:
            wins_a += 1
        else:
            wins_b += 1
    total = wins_a + wins_b + ties
    wr = (wins_a + 0.5 * ties) / total if total else 0.0
    return SeriesResult(wr, wins_a, wins_b, ties, total, _wilson_half(wr, total), True)


def series_batch_adaptive(
    key_a: str,
    names_a: list[str],
    key_b: str,
    names_b: list[str],
    level: int,
    *,
    ci_tol: float = 0.05,
    batch: int = 8,
    max_games: int = 96,
    sign_margin: float = 0.15,
    base_seed: int = 1000,
    cache: BattleCache | None = None,
    meta: Path = DEFAULT_META,
) -> SeriesResult:
    """签名版自适应预算系列赛（停止条件与 series_adaptive 相同）。"""
    played = 0
    res: SeriesResult | None = None
    while True:
        target = min(played + batch - (played + batch) % 2, max_games)
        if target <= played:
            break
        res = series_batch(key_a, names_a, key_b, names_b, level, target,
                           base_seed=base_seed, cache=cache, meta=meta)
        played = res.games
        lo, hi = res.winrate_a - res.ci_half, res.winrate_a + res.ci_half
        sign_clear = lo > 0.5 + sign_margin or hi < 0.5 - sign_margin
        if res.ci_half <= ci_tol or sign_clear or played >= max_games:
            return SeriesResult(
                res.winrate_a, res.wins_a, res.wins_b, res.ties, res.games,
                res.ci_half, res.ci_half <= ci_tol or sign_clear,
            )
    assert res is not None
    return res


@dataclass
class BattleCache:
    """JSONL 追加式持久缓存。键：(key_a, key_b, seed)。"""

    def __init__(self, path: Path = DEFAULT_CACHE):
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self._lock = threading.Lock()
        self._mem: dict[tuple[str, str, int], int] = {}
        if self.path.exists():
            for line in self.path.read_text(encoding="utf-8").splitlines():
                if not line.strip():
                    continue
                try:
                    r = json.loads(line)
                except json.JSONDecodeError:
                    # 多进程并发追加可能产生截断行；跳过（容忍性加载）
                    continue
                self._mem[(r["a"], r["b"], r["seed"])] = r["winner"]

    def get(self, a: str, b: str, seed: int) -> int | None:
        return self._mem.get((a, b, seed))

    def put(self, a: str, b: str, seed: int, winner: int) -> None:
        with self._lock:
            if (a, b, seed) in self._mem:
                return
            self._mem[(a, b, seed)] = winner
            with self.path.open("a", encoding="utf-8") as f:
                f.write(json.dumps({"a": a, "b": b, "seed": seed, "winner": winner}) + "\n")


@dataclass
class SeriesResult:
    winrate_a: float  # 平局按 0.5 计
    wins_a: int
    wins_b: int
    ties: int
    games: int
    ci_half: float  # Wilson 95% 半宽（按胜率点估计）
    decisive: bool  # 是否在预算内收敛


def _wilson_half(w: float, n: int, z: float = 1.96) -> float:
    if n == 0:
        return 1.0
    denom = 1 + z * z / n
    center = (w + z * z / (2 * n)) / denom
    half = z * math.sqrt(w * (1 - w) / n + z * z / (4 * n * n)) / denom
    return half
