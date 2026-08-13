# -*- coding: utf-8 -*-
"""二人零和混合博弈近似求解：fictitious play + exploitability。

输入：meta_search.matrix 产物（payoffs 以 A 视角胜率存储，零和：B 的收益 = 1 - winrate_a）。
输出：混合策略 sigma（卡组权重）、博弈值、exploitability（单方最优偏离可提升的收益）。

说明：fictitious play 在零和博弈上收敛到 Nash（速率 O(1/sqrt(T))，足够矩阵分析用）；
后续如需精确解可换 LP（需引入 scipy，暂保持零依赖）。
"""

from __future__ import annotations

import json
import sys
from pathlib import Path


def load_payoff_fn(matrix_path: Path):
    doc = json.loads(Path(matrix_path).read_text(encoding="utf-8"))
    names = sorted(doc["decks"].keys())
    idx = {n: i for i, n in enumerate(names)}
    n = len(names)
    # M[i][j] = 行方 i 对列方 j 的胜率（行方收益）
    M = [[0.5] * n for _ in range(n)]
    for key, p in doc["payoffs"].items():
        a, b = key.split("|", 1)
        M[idx[a]][idx[b]] = p["winrate_a"]
        M[idx[b]][idx[a]] = 1 - p["winrate_a"]
    return names, M


def fictitious_play(M: list[list[float]], iterations: int = 20000):
    n = len(M)
    row_counts = [0.0] * n  # 行方混合（各纯策略累计被选次数）
    col_counts = [0.0] * n
    # 初始：各自最佳应对均匀对手
    row_payoffs = [sum(M[i][j] for j in range(n)) / n for i in range(n)]
    col_payoffs = [sum(M[i][j] for i in range(n)) / n for j in range(n)]  # 列方想最小化
    br_r = max(range(n), key=lambda i: row_payoffs[i])
    br_c = min(range(n), key=lambda j: col_payoffs[j])
    row_counts[br_r] += 1
    col_counts[br_c] += 1
    for _ in range(iterations):
        # 行方对列方经验混合的最佳应对（最大化收益）
        sigma_c = [c / sum(col_counts) for c in col_counts]
        pay_r = [sum(M[i][j] * sigma_c[j] for j in range(n)) for i in range(n)]
        br_r = max(range(n), key=lambda i: pay_r[i])
        row_counts[br_r] += 1
        # 列方对行方经验混合的最佳应对（最小化行方收益）
        sigma_r = [c / sum(row_counts) for c in row_counts]
        pay_c = [sum(M[i][j] * sigma_r[i] for i in range(n)) for j in range(n)]
        br_c = min(range(n), key=lambda j: pay_c[j])
        col_counts[br_c] += 1
    total_r, total_c = sum(row_counts), sum(col_counts)
    return [c / total_r for c in row_counts], [c / total_c for c in col_counts]


def evaluate(M: list[list[float]], sigma: list[float]):
    """返回 (博弈值, exploitability, 最佳应对索引, 支撑集)。"""
    n = len(M)
    value = sum(sigma[i] * sigma[j] * M[i][j] for i in range(n) for j in range(n))
    # 单方偏离收益（对称博弈视角：任一方换成纯策略 i 对抗 sigma）
    pay = [sum(M[i][j] * sigma[j] for j in range(n)) for i in range(n)]
    br = max(range(n), key=lambda i: pay[i])
    # 对手（最小化方）的偏离
    pay_min = [sum(M[i][j] * sigma[i] for i in range(n)) for j in range(n)]
    br_min = min(range(n), key=lambda j: pay_min[j])
    exploit = (pay[br] - value) + (value - pay_min[br_min])
    support = [i for i, w in enumerate(sigma) if w > 1e-3]
    return value, exploit, br, support


def main() -> int:
    import argparse

    p = argparse.ArgumentParser()
    p.add_argument("--matrix", type=Path, required=True)
    p.add_argument("--iterations", type=int, default=20000)
    p.add_argument("--top", type=int, default=15)
    args = p.parse_args()

    names, M = load_payoff_fn(args.matrix)
    sigma, _ = fictitious_play(M, args.iterations)
    value, exploit, br, support = evaluate(M, sigma)
    print(f"decks={len(names)} value={value:.4f} exploitability={exploit:.4f}")
    print(f"support size={len(support)}, best response={names[br]}")
    print("\n== 混合策略权重 top ==")
    for i in sorted(range(len(names)), key=lambda i: -sigma[i])[: args.top]:
        if sigma[i] > 1e-4:
            print(f"  {sigma[i]:.4f}  {names[i]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
