#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
将 out/l2|l3|l4|l5/generality.csv 合并为一个 Excel：
列为 item, l2, l3, l4, l5（值为 generality）。

输入 CSV 期望包含表头：rank,item,generality
"""

from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path


def _read_generality_csv(path: Path) -> dict[str, float]:
    if not path.is_file():
        raise FileNotFoundError(str(path))
    with path.open("r", encoding="utf-8", newline="") as f:
        r = csv.DictReader(f)
        if r.fieldnames is None:
            raise ValueError(f"empty csv: {path}")
        need = {"item", "generality"}
        got = set(r.fieldnames)
        if not need.issubset(got):
            raise ValueError(f"bad header in {path}: expected at least {sorted(need)}, got {r.fieldnames}")
        out: dict[str, float] = {}
        for row in r:
            item = (row.get("item") or "").strip()
            if not item:
                continue
            g_raw = (row.get("generality") or "").strip()
            if not g_raw:
                continue
            try:
                g = float(g_raw)
            except ValueError as e:
                raise ValueError(f"bad generality value in {path}: item={item!r} generality={g_raw!r}") from e
            out[item] = g
        return out


def main() -> int:
    script_dir = Path(__file__).resolve().parent
    default_repo = script_dir.parent

    p = argparse.ArgumentParser(description="合并 out/l2|l3|l4|l5/generality.csv 到一个 xlsx。")
    p.add_argument(
        "--repo-root",
        type=Path,
        default=default_repo,
        help="仓库根目录（默认：本脚本上级目录）",
    )
    p.add_argument(
        "--out",
        type=Path,
        default=None,
        help="输出 xlsx 路径（默认：<repo-root>/out/generality_l2_l3_l4_l5.xlsx）",
    )
    args = p.parse_args()

    repo_root: Path = args.repo_root.resolve()
    out_path: Path = (args.out or (repo_root / "out" / "generality_l2_l3_l4_l5.xlsx")).resolve()

    try:
        from openpyxl import Workbook  # type: ignore
    except Exception:
        print(
            "error: 缺少依赖 openpyxl。请先安装：\n"
            "  python -m pip install openpyxl\n",
            file=sys.stderr,
        )
        return 2

    p2 = repo_root / "out" / "l2" / "generality.csv"
    p3 = repo_root / "out" / "l3" / "generality.csv"
    p4 = repo_root / "out" / "l4" / "generality.csv"
    p5 = repo_root / "out" / "l5" / "generality.csv"

    try:
        l2 = _read_generality_csv(p2)
        l3 = _read_generality_csv(p3)
        l4 = _read_generality_csv(p4)
        l5 = _read_generality_csv(p5)
    except Exception as e:
        print(f"error: {e}", file=sys.stderr)
        return 2

    items = sorted(set(l2) | set(l3) | set(l4) | set(l5))

    wb = Workbook()
    ws = wb.active
    ws.title = "generality"
    ws.append(["item", "l2", "l3", "l4", "l5"])
    for item in items:
        ws.append([item, l2.get(item), l3.get(item), l4.get(item), l5.get(item)])

    out_path.parent.mkdir(parents=True, exist_ok=True)
    wb.save(out_path)
    print(f"wrote {len(items)} items to {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

