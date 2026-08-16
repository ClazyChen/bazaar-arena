# -*- coding: utf-8 -*-
"""开放 DO 运行验收（阶段 3 判据）：

1. multiset 重合：开放循环池中的卡组与真值 Nash 支撑（由 --matrix 动态求解，
   不写死——物品修复会使旧支撑失效，如 2026-08 遗物再生王崩盘）及 meta 胜率 top10 卡组的引擎核心重合度。
2. 强度对照：开放池 σ 支撑卡组 vs 真值支撑卡组的直接系列赛（真对战，CI 控制）。
3. exploitability 轨迹：history 中 best_gain 随迭代的变化（应单调趋 0）。
"""

import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from meta_search import battle, gdf_conditions, nash  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent


def truth_support_from_matrix(matrix_path: Path, top: int = 5) -> dict[str, str]:
    """从真值矩阵动态求解 Nash 支撑（按权重降序取前 top 个）。"""
    doc = json.loads(Path(matrix_path).read_text(encoding="utf-8"))
    names, M = nash.load_payoff_fn(matrix_path)
    sigma, _ = nash.fictitious_play(M)
    _value, _exploit, _br, support = nash.evaluate(M, sigma)
    ranked = sorted(support, key=lambda i: -sigma[i])[:top]
    return {names[i]: doc["decks"][names[i]]["signature"] for i in ranked}


# 各流派引擎核心（用于部分重合判定；2026-08 物品修复后口径）
ENGINES = {
    "药水引擎": {"碎瓶", "沸腾烧瓶", "炼金炉"},
    "遗物再生引擎": {"先祖墓", "魂戒"},
    "自毒注射": {"快速注射系统", "肾上腺素调节服"},
    "自毒武器": {"注能护腕", "实验体阿尔法"},
    "减速控制": {"时间之砂", "嗅盐"},
    "冻结控制": {"冰霜之怖", "寒霜图腾"},
    "图书馆引擎": {"图书馆", "采掘工具", "永恒火炬"},
    "天平充能": {"天平", "永恒火炬"},
    "剧毒武器": {"瘟疫长柄刀", "蕨叶蜘蛛"},
}


def ms(sig: str) -> frozenset:
    return frozenset(sig.split(","))


def main() -> int:
    import argparse

    ap = argparse.ArgumentParser()
    ap.add_argument("--log", type=Path, default=REPO / "out/meta_search/open_do_l8.json")
    ap.add_argument("--matrix", type=Path, default=REPO / "out/meta_search/matrix_l8_v3.json",
                    help="真值矩阵（Nash 支撑动态求解，不写死）")
    args = ap.parse_args()
    run = json.loads(args.log.read_text(encoding="utf-8"))
    pool = run["pool"]
    sigma = run["sigma"]
    truth_support = truth_support_from_matrix(args.matrix)
    print(f"真值支撑（来自 {args.matrix.name}）：{list(truth_support)}")
    print("== 1. 引擎核心重合 ==")
    for name, core in ENGINES.items():
        hit = [p for p in pool if core <= set(p.split(","))]
        print(f"  {name}: {'✓' if hit else '✗'} {hit[:1]}")
    print("\n== 2. 与真值支撑 multiset 精确重合 ==")
    for name, sig in truth_support.items():
        exact = ms(sig) in {ms(p) for p in pool}
        print(f"  {name}: {'✓ 精确' if exact else '✗'}")

    print("\n== 3. 强度对照（开放池 top3 σ 支撑 vs 真值支撑，41 局） ==")
    db = gdf_conditions.load_item_db(REPO / "data" / "items", "mak")
    cache = battle.BattleCache()
    # 用完整 pool 重解 σ（final sigma 的键被截断到 60 字符，不可用于回查）
    sys.path.insert(0, str(REPO / "scripts"))
    from meta_search.open_do import OpenRun

    orun = OpenRun(8, db, cache)
    full_sigma, _ = orun.sigma_on(pool)
    support = sorted(zip(pool, full_sigma), key=lambda t: -t[1])[:3]
    for sig, w in support:
        open_key = f"open:{sig}"  # 缓存键必须唯一到卡组，不能复用字面量
        print(f"  open σ={w:.3f} | {sig[:50]}")
        for tname, tsig in truth_support.items():
            r = battle.series_batch(open_key, sig.split(","), f"truth:{tsig}", tsig.split(","),
                                    8, 41, cache=cache)
            print(f"    vs {tname}: wr={r.winrate_a:.3f} ±{r.ci_half:.3f}")

    print("\n== 4. best_gain 轨迹 ==")
    for h in run["history"]:
        print(f"  it={h['iter']:2d} pool={h['pool']:2d} gain={h['best_gain']:.4f} {h['best'][:40]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
