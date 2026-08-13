# -*- coding: utf-8 -*-
"""Double Oracle / PSRO 外环（阶段 3）。

本模块支持两种 oracle：
- 受限博弈（restricted）：收益查询直接读已有 meta 矩阵 JSON，零对战成本，
  用于验证外环机制（Nash 收敛、BR 正确性、exploitability 下降、再发现率）。
- 开放博弈（open）：收益由 meta_search.battle 自适应评估产生（后续接模板提议器）。

流程：
1. 冷启动策略池 P（少量卡组）；
2. 填 P×P 子矩阵，解混合 Nash σ（fictitious play）；
3. best response：在候选宇宙中找对 σ 期望收益最高的卡组 d*；
4. 若 d* 对 σ 的收益 - 博弈值 > eps，则并入 P，迭代；否则停止。
"""

from __future__ import annotations

import json
import random
import sys
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from meta_search import nash  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent


class MatrixOracle:
    """从矩阵 JSON 提供收益查询；未覆盖的对局返回 None（开放模式下由评估层补齐）。"""

    def __init__(self, matrix_path: Path):
        doc = json.loads(Path(matrix_path).read_text(encoding="utf-8"))
        self.decks = sorted(doc["decks"].keys())
        self.payoffs: dict[tuple[str, str], float] = {}
        for key, p in doc["payoffs"].items():
            a, b = key.split("|", 1)
            self.payoffs[(a, b)] = p["winrate_a"]
            self.payoffs[(b, a)] = 1 - p["winrate_a"]

    def payoff(self, a: str, b: str) -> float | None:
        if a == b:
            return 0.5
        return self.payoffs.get((a, b))


@dataclass
class DOResult:
    history: list[dict] = field(default_factory=list)  # 每轮 {pool_size, value, exploit, added}
    final_sigma: dict[str, float] = field(default_factory=dict)
    pool: list[str] = field(default_factory=list)


def submatrix(oracle: MatrixOracle, pool: list[str]) -> list[list[float]]:
    n = len(pool)
    M = [[0.5] * n for _ in range(n)]
    for i, a in enumerate(pool):
        for j, b in enumerate(pool):
            if i != j:
                v = oracle.payoff(a, b)
                if v is None:
                    raise KeyError(f"missing payoff {a} vs {b}")
                M[i][j] = v
    return M


def solve_sigma(M: list[list[float]], iterations: int = 5000) -> list[float]:
    sigma, _ = nash.fictitious_play(M, iterations)
    return sigma


def expected_payoff_vs_sigma(oracle: MatrixOracle, deck: str, pool: list[str],
                             sigma: list[float]) -> float | None:
    total = 0.0
    for p, w in zip(pool, sigma):
        if w < 1e-9:
            continue
        v = oracle.payoff(deck, p)
        if v is None:
            return None
        total += w * v
    return total


def double_oracle(
    oracle: MatrixOracle,
    universe: list[str],
    init_pool: list[str],
    *,
    eps: float = 0.005,
    max_iter: int = 50,
) -> DOResult:
    pool = list(init_pool)
    result = DOResult()
    for it in range(max_iter):
        M = submatrix(oracle, pool)
        sigma = solve_sigma(M)
        value, exploit, _, _ = nash.evaluate(M, sigma)
        # BR over universe
        best_deck, best_pay = None, -1.0
        for d in universe:
            if d in pool:
                continue
            pay = expected_payoff_vs_sigma(oracle, d, pool, sigma)
            if pay is not None and pay > best_pay:
                best_deck, best_pay = d, pay
        gain = (best_pay - value) if best_deck else 0.0
        result.history.append({
            "iter": it, "pool_size": len(pool), "value": round(value, 4),
            "exploit": round(exploit, 4), "br": best_deck, "br_gain": round(gain, 4),
        })
        if best_deck is None or gain <= eps:
            break
        pool.append(best_deck)
    result.pool = pool
    M = submatrix(oracle, pool)
    sigma = solve_sigma(M)
    result.final_sigma = {p: round(w, 4) for p, w in zip(pool, sigma) if w > 1e-3}
    return result


def main() -> int:
    import argparse

    p = argparse.ArgumentParser(description="受限博弈 Double Oracle 验证（oracle=已有矩阵）")
    p.add_argument("--matrix", type=Path, default=REPO / "out/meta_search/matrix_l8_v1.json")
    p.add_argument("--init", type=int, default=5, help="冷启动池大小（随机抽取）")
    p.add_argument("--seed", type=int, default=42)
    p.add_argument("--eps", type=float, default=0.005)
    args = p.parse_args()

    oracle = MatrixOracle(args.matrix)
    rng = random.Random(args.seed)
    init = rng.sample(oracle.decks, args.init)
    print(f"cold start pool: {init}")

    res = double_oracle(oracle, oracle.decks, init, eps=args.eps)
    for h in res.history:
        print(f"  it={h['iter']:2d} pool={h['pool_size']:2d} value={h['value']:.4f} "
              f"exploit={h['exploit']:.4f} +BR={h['br']} gain={h['br_gain']:.4f}")
    print(f"\nfinal pool ({len(res.pool)}): sigma={res.final_sigma}")

    # 与全矩阵真值 Nash 支撑对照（再发现率）
    names, M_full = nash.load_payoff_fn(args.matrix)
    sigma_full, _ = nash.fictitious_play(M_full, 20000)
    truth_support = {names[i] for i, w in enumerate(sigma_full) if w > 1e-3}
    found = truth_support & set(res.pool)
    print(f"真值 Nash 支撑: {sorted(truth_support)}")
    print(f"再发现: {len(found)}/{len(truth_support)} = {sorted(found)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
