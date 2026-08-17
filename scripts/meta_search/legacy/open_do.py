# -*- coding: utf-8 -*-
"""开放博弈 Double Oracle（阶段 3 主循环）：真实对战评估 + 模板提议器。

与受限模式（double_oracle.py，矩阵 oracle）的差别：
- 宇宙不再限于已有矩阵；收益由 meta_search.battle 自适应评估（带缓存）；
- 每轮：补全池内缺失收益 → 解 σ → 模板提议候选 → 评估候选 vs 池 → 最优者入池；
- 收敛判据：最优候选对 σ 的期望收益 - 池博弈值 ≤ eps。

验收对照：最终池与真值 Nash 支撑（受限博弈已解出：炼金炉/先祖墓/腐朽圣像）按
multiset 重合与对战胜率双重比较。
"""

from __future__ import annotations

import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent.parent))
from meta_search import battle, gdf_conditions, nash  # noqa: E402
from meta_search.legacy import proposer, templates  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent.parent


@dataclass
class OpenRun:
    level: int
    db: dict
    cache: battle.BattleCache
    payoffs: dict[tuple[str, str], float] = field(default_factory=dict)
    adaptive_params: dict = field(default_factory=lambda: {
        "ci_tol": 0.06, "batch": 8, "max_games": 64, "sign_margin": 0.15, "base_seed": 1000})

    def ensure_payoffs(self, pairs: list[tuple[str, str]]) -> None:
        """整批补齐缺失 payoff：一次自适应波次（C++ 内卷），不做逐对串行请求。"""
        missing = [(a, b) for a, b in pairs
                   if a != b and (a, b) not in self.payoffs]
        if not missing:
            return
        server = battle.MetaServer.shared(self.level)
        chunk = 1500
        for start in range(0, len(missing), chunk):
            part = missing[start:start + chunk]
            reqs = [(f"{i}", a.split(","), b.split(",")) for i, (a, b) in enumerate(part)]
            out = server.play_adaptive_wave(reqs, self.adaptive_params)
            for (a, b), r in zip(part, out):
                wr = (r["wins_a"] + 0.5 * r["ties"]) / r["games"]
                self.payoffs[(a, b)] = wr
                self.payoffs[(b, a)] = 1 - wr

    def payoff(self, a: str, b: str) -> float:
        if a == b:
            return 0.5
        return self.payoffs[(a, b)]

    def sigma_on(self, pool: list[str], iterations: int = 5000):
        n = len(pool)
        self.ensure_payoffs([(pool[i], pool[j]) for i in range(n) for j in range(i + 1, n)])
        M = [[0.5] * n for _ in range(n)]
        for i, a in enumerate(pool):
            for j, b in enumerate(pool):
                if i != j:
                    M[i][j] = self.payoffs[(a, b)]
        sigma, _ = nash.fictitious_play(M, iterations)
        value, _, _, _ = nash.evaluate(M, sigma)
        return sigma, value

    def vs_sigma(self, sig: str, pool: list[str], sigma: list[float]) -> float:
        self.ensure_payoffs([(sig, p) for p in pool if p != sig])
        return sum(w * self.payoffs[(sig, p)] for p, w in zip(pool, sigma) if w > 1e-9)


def _jaccard(sig_a: str, sig_b: str) -> float:
    a, b = set(sig_a.split(",")), set(sig_b.split(","))
    return len(a & b) / len(a | b) if a | b else 0.0


def run(
    level: int = 8,
    init_sigs: list[str] | None = None,
    eps: float = 0.01,
    max_iter: int = 12,
    add_per_iter: int = 2,
    iso_threshold: float = 0.6,
    gdf_topk: Path | None = None,
    gdf_sample: int = 100,
    reason_engines: int = 0,
    fill_variants: int = 3,
    log_path: Path | None = None,
) -> dict:
    db = gdf_conditions.load_item_db(REPO / "data" / "items", "mak")
    cache = battle.BattleCache()
    orun = OpenRun(level, db, cache)

    pool = init_sigs or [
        "图书馆,采掘工具,永恒火炬,嗅盐,灼烧减速魂石",
        "碎瓶,沸腾烧瓶,炼金炉,飞行药水,力量药水,无敌药水",
        "天平,永恒火炬,采掘工具,力量药水,能量药水,智者之杖,魂石",
    ]
    # 修正：天平冷启动卡组需满足居中（7 件奇数且居中），由 layout 保证
    _tp_tmpl = templates.Template(name="天平充能", core=[], center_item="天平")
    pool[2] = ",".join(templates.layout(pool[2].split(","), _tp_tmpl, db))

    history = []
    rg = None
    if reason_engines > 0:
        from meta_search.legacy import reason_graph

        rg = reason_graph.load(REPO / "data" / "items", "mak")
    for it in range(max_iter):
        sigma, value = orun.sigma_on(pool)
        cands = []
        if gdf_topk:
            cands += proposer.gdf_topk_candidates(
                gdf_topk, sample=gdf_sample, seed=200 + it, exclude=set(pool))
        if rg is not None:
            cands += proposer.reason_engine_candidates(
                rg, db, top_engines=reason_engines, fill_variants=fill_variants,
                seed=300 + it)
        # 整批补齐 候选×池 payoff（一次自适应波次），再本地打分
        live = [c["signature"] for c in cands if c["signature"] not in pool]
        orun.ensure_payoffs([(sig, p) for sig in live for p in pool if p != sig])
        scored: list[tuple[str, float]] = []
        for sig in live:
            pay = sum(w * orun.payoffs[(sig, p)] for p, w in zip(pool, sigma) if w > 1e-9)
            scored.append((sig, pay))
        scored.sort(key=lambda t: -t[1])
        # 多样性入池：按 gain 降序，跳过与池内/本轮已选过度同构（Jaccard ≥ 阈值）者
        picked: list[tuple[str, float]] = []
        for sig, pay in scored:
            if len(picked) >= add_per_iter:
                break
            if pay - value <= eps and not picked:
                # 即便未过 eps，也记录首个最优者用于轨迹
                picked.append((sig, pay))
                break
            if pay - value <= eps:
                continue
            if any(_jaccard(sig, p) >= iso_threshold for p in pool) or \
               any(_jaccard(sig, q) >= iso_threshold for q, _ in picked):
                continue
            picked.append((sig, pay))
        best_sig, best_pay = picked[0] if picked else (None, 0.0)
        gain = best_pay - value if best_sig else 0.0
        new_sigs = [s for s, p_ in picked if p_ - value > eps]
        rec = {"iter": it, "pool": len(pool), "value": round(value, 4),
               "evals": len(scored), "best_gain": round(gain, 4),
               "added": [s[:60] for s in new_sigs],
               "best": (best_sig or "")[:60]}
        history.append(rec)
        print(rec)
        if log_path:
            log_path.parent.mkdir(parents=True, exist_ok=True)
            log_path.write_text(json.dumps({"history": history, "pool": pool},
                                           ensure_ascii=False, indent=1), encoding="utf-8")
        if not new_sigs:
            break
        pool.extend(new_sigs)

    sigma, value = orun.sigma_on(pool)
    final = {
        "pool": pool,
        "sigma": {p[:60]: round(w, 4) for p, w in zip(pool, sigma) if w > 1e-3},
        "value": round(value, 4),
        "history": history,
    }
    if log_path:
        log_path.write_text(json.dumps(final, ensure_ascii=False, indent=1), encoding="utf-8")
    return final


if __name__ == "__main__":
    import argparse

    p = argparse.ArgumentParser()
    p.add_argument("--per-template", type=int, default=0, help="已退役（手写模板提议器），忽略")
    p.add_argument("--max-iter", type=int, default=12)
    p.add_argument("--eps", type=float, default=0.01)
    p.add_argument("--gdf-topk", type=Path, default=None,
                   help="GDF 枚举 full topk 文件（第三提议器）")
    p.add_argument("--gdf-sample", type=int, default=100)
    p.add_argument("--reason-engines", type=int, default=0,
                   help="理由图引擎提议器：取 top N 个挖掘引擎（0=禁用）")
    p.add_argument("--fill-variants", type=int, default=3)
    p.add_argument("--log", type=Path, default=REPO / "out/meta_search/open_do_l8.json")
    args = p.parse_args()
    final = run(max_iter=args.max_iter,
                eps=args.eps, gdf_topk=args.gdf_topk, gdf_sample=args.gdf_sample,
                reason_engines=args.reason_engines, fill_variants=args.fill_variants,
                log_path=args.log)
    print(json.dumps(final["sigma"], ensure_ascii=False, indent=1))
