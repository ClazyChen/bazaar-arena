#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
调用 bin/bazaararena_gdf（--enumerate-anchors），解析每个锚点物品在**满槽最后一档**
的 Top-1 候选，汇总写入表格文件。

可调参数：--pool-hero、--level、--top-k、--top-multiplier、--data-dir、--gdf、--repo-root。
其余与 GDF 一致且固定：--lambda-anchor 0.5、--mu-diversity 0.1、--diversity-exclude-seeds；
不传入 --workers（沿用可执行文件默认硬件并发）、不传入 --seed/--exclude-item/--timing。

运行时将 GDF 的**完整文本输出**实时打印到控制台（与写入汇总文件的内容来源一致；汇总仍只写入 `-o` 指定路径）。
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
import threading
from pathlib import Path

_SEEDS_RE = re.compile(r"^\[GDF\] seeds:\s*(.+)\s*$")
_TOP1_RE = re.compile(
    r"^\s*1\.\s*RR=(?P<rr>[-\d.]+)\s+anchor_m=(?P<am>[-\d.]+)\s+Swiss=(?P<sw>[-\d.]+)\s+\|\s*(?P<sig>.+)\s*$"
)


def _default_gdf_exe(repo_root: Path) -> Path:
    if sys.platform == "win32":
        return repo_root / "bin" / "bazaararena_gdf.exe"
    return repo_root / "bin" / "bazaararena_gdf"


def _parse_gdf_output(text: str) -> list[tuple[str, str, str, str, str]]:
    """
    对每个 [GDF] seeds: 块，取**最后一次**出现的 rank-1 行（即最大 size 档的 Top-1）。
    返回列表 (anchor, rr, anchor_m, swiss, deck_signature)。
    """
    rows: list[tuple[str, str, str, str, str]] = []
    current_anchor: str | None = None
    last_top1: tuple[str, str, str, str] | None = None

    for raw in text.splitlines():
        line = raw.rstrip("\n")
        m_seeds = _SEEDS_RE.match(line)
        if m_seeds:
            if current_anchor is not None and last_top1 is not None:
                rr, am, sw, sig = last_top1
                rows.append((current_anchor, rr, am, sw, sig))
            current_anchor = m_seeds.group(1).strip()
            last_top1 = None
            continue
        m1 = _TOP1_RE.match(line)
        if m1 and current_anchor is not None:
            last_top1 = (
                m1.group("rr"),
                m1.group("am"),
                m1.group("sw"),
                m1.group("sig").strip(),
            )

    if current_anchor is not None and last_top1 is not None:
        rr, am, sw, sig = last_top1
        rows.append((current_anchor, rr, am, sw, sig))

    return rows


def main() -> int:
    script_dir = Path(__file__).resolve().parent
    default_repo = script_dir.parent

    p = argparse.ArgumentParser(
        description="枚举池内全部锚点物品，提取每锚点满槽档 Top-1 并写入汇总文件。"
    )
    p.add_argument(
        "-o",
        "--output",
        type=Path,
        required=True,
        help="汇总输出路径（UTF-8 TSV：anchor, rr, anchor_m, swiss, deck）",
    )
    p.add_argument(
        "--repo-root",
        type=Path,
        default=default_repo,
        help="仓库根目录（默认：本脚本上级目录）",
    )
    p.add_argument(
        "--gdf",
        type=Path,
        default=None,
        help="bazaararena_gdf 可执行文件路径（默认：仓库 bin 下）",
    )
    p.add_argument(
        "--data-dir",
        type=Path,
        default=None,
        help="物品 YAML 目录（默认：<repo-root>/data/items）",
    )
    p.add_argument(
        "--pool-hero",
        default="Vanessa",
        help="物品池英雄过滤（默认 Vanessa；与 GDF 一致：Vanessa|Mak|Common|All）",
    )
    p.add_argument("--level", type=int, default=2, help="玩家等级（默认 2）")
    p.add_argument("--top-k", type=int, default=10, help="每档 beam 大小（默认 10）")
    p.add_argument(
        "--top-multiplier",
        type=int,
        default=3,
        help="瑞士阶段晋级倍数 M，晋级人数 min(N, k*M)（默认 3）",
    )
    args = p.parse_args()

    repo_root: Path = args.repo_root.resolve()
    gdf: Path = (args.gdf or _default_gdf_exe(repo_root)).resolve()
    data_dir: Path = (args.data_dir or (repo_root / "data" / "items")).resolve()

    if not gdf.is_file():
        print(f"error: GDF executable not found: {gdf}", file=sys.stderr)
        return 2
    if not data_dir.is_dir():
        print(f"error: data items directory not found: {data_dir}", file=sys.stderr)
        return 2

    cmd: list[str] = [
        str(gdf),
        "--data-dir",
        str(data_dir),
        "--enumerate-anchors",
        "--pool-hero",
        str(args.pool_hero),
        "--level",
        str(int(args.level)),
        "--top-k",
        str(int(args.top_k)),
        "--top-multiplier",
        str(int(args.top_multiplier)),
        "--lambda-anchor",
        "0.5",
        "--mu-diversity",
        "0.1",
        "--diversity-exclude-seeds",
    ]
    # 不传 --output：GDF 写 stdout；我们边读边打到控制台，避免「只写文件时控制台无输出」。
    proc = subprocess.Popen(
        cmd,
        cwd=str(repo_root),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
        bufsize=0,
    )
    assert proc.stdout is not None
    stderr_data: list[str] = []

    def _drain_stderr() -> None:
        if proc.stderr is None:
            return
        stderr_data.append(proc.stderr.read())

    err_thread = threading.Thread(target=_drain_stderr, daemon=True)
    err_thread.start()

    chunks: list[str] = []
    try:
        for line in proc.stdout:
            chunks.append(line)
            sys.stdout.write(line)
            sys.stdout.flush()
    finally:
        proc.stdout.close()
    err_thread.join(timeout=600)
    err = "".join(stderr_data)
    proc.wait()
    if proc.returncode != 0:
        if err:
            sys.stderr.write(err)
        print(f"error: GDF exited with code {proc.returncode}", file=sys.stderr)
        return proc.returncode or 1
    if err:
        sys.stderr.write(err)

    raw_text = "".join(chunks)

    rows = _parse_gdf_output(raw_text)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="\n") as out:
        out.write("anchor\trr\tanchor_m\tswiss\tdeck\n")
        for anchor, rr, am, sw, sig in rows:
            sig_esc = sig.replace("\t", " ").replace("\n", " ")
            out.write(f"{anchor}\t{rr}\t{am}\t{sw}\t{sig_esc}\n")

    print(f"wrote {len(rows)} rows to {args.output.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
