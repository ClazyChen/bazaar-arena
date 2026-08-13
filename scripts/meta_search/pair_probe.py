# -*- coding: utf-8 -*-
"""经验配对探测图（理由图的完备性兜底，第二层）。

思想：模拟器本身就是交互关系的完整定义。对每对无序物品 {A,B}（尺寸和 ≤10）：
- test:  A + B + 惰性填充（补满 10 格）
- ctrlB: A + B0 + 同填充（B0 为与 B 同尺寸的惰性物品）→ B 在 A 语境下的边际 Δ_B|A
- ctrlA: B + A0 + 同填充 → A 在 B 语境下的边际 Δ_A|B
对固定中等强度对手各打 SEEDS 局（左右各半），输出每对的双向边际胜率差。

用途：与静态理由图对照——经验边存在而静态边缺失的对 = 候选未建模通道；
静态边存在而经验不显著的对 = 弱理由（可降权）。定期校准作业（物品数据变更后重跑）。
"""

from __future__ import annotations

import concurrent.futures as cf
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from meta_search import battle, gdf_conditions, reason_graph  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent
OUT = REPO / "out" / "meta_search" / "pair_probe.json"
SIZES = {"Small": 1, "Medium": 2, "Large": 3}
SEEDS = 16
OPPONENT = "图书馆,采掘工具,永恒火炬,嗅盐,灼烧减速魂石"  # 固定中等强度参照


def pick_inert(g: reason_graph.ReasonGraph) -> dict[int, str]:
    """每个尺寸选邻域最小（最惰性）的物品作填充/对照。"""
    out: dict[int, str] = {}
    for sz in (1, 2, 3):
        cands = [n for n, it in g.db.items() if SIZES.get(it.get("Size", ""), 1) == sz]
        cands.sort(key=lambda n: (len(g.neighbors(n)), n))
        out[sz] = cands[0]
    return out


def build_deck(names: list[str], fill_to: int, inert1: str) -> list[str]:
    deck = list(names)
    while len(deck) < fill_to:
        deck.append(inert1)
    return deck


def winrate(sig_a: str, sig_opp: str, level: int, seeds: int,
            cache: battle.BattleCache, tag: str) -> float:
    r = battle.series_batch(tag, sig_a.split(","), "opp", sig_opp.split(","),
                            level, seeds, cache=cache)
    return r.winrate_a


def probe_pair(a: str, b: str, g: reason_graph.ReasonGraph, db: dict,
               inert: dict[int, str], sig_opp: str, level: int,
               cache: battle.BattleCache) -> dict | None:
    sa, sb = SIZES.get(g.db[a].get("Size", ""), 1), SIZES.get(g.db[b].get("Size", ""), 1)
    if sa + sb > 10:
        return None
    inert1 = inert[1]
    fill_n = (10 - sa - sb) // 1  # 惰性物品为 1 格，直接按数量补
    base = [a, b] + [inert1] * fill_n
    ctrl_b = [a, inert[sb]] + [inert1] * (10 - sa - SIZES.get(g.db[inert[sb]].get("Size", ""), 1))
    ctrl_a = [b, inert[sa]] + [inert1] * (10 - sb - SIZES.get(g.db[inert[sa]].get("Size", ""), 1))
    tag = f"{a}|{b}"
    w_test = winrate(",".join(base), sig_opp, level, SEEDS, cache, f"T:{tag}")
    w_cb = winrate(",".join(ctrl_b), sig_opp, level, SEEDS, cache, f"CB:{tag}")
    w_ca = winrate(",".join(ctrl_a), sig_opp, level, SEEDS, cache, f"CA:{tag}")
    return {"a": a, "b": b, "delta_b": round(w_test - w_cb, 4),
            "delta_a": round(w_test - w_ca, 4)}


def main() -> int:
    g = reason_graph.load(REPO / "data" / "items", "mak")
    db = gdf_conditions.load_item_db(REPO / "data" / "items", "mak")
    inert = pick_inert(g)
    print(f"惰性对照: {inert}")
    names = sorted(g.db.keys())
    pairs = [(names[i], names[j]) for i in range(len(names)) for j in range(i + 1, len(names))]
    print(f"pairs={len(pairs)} seeds={SEEDS}")

    cache = battle.BattleCache(REPO / "out" / "meta_search" / "pair_probe_cache.jsonl")
    results: list[dict] = []
    done = 0
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        futs = {pool.submit(probe_pair, a, b, g, db, inert, OPPONENT, 8, cache): (a, b)
                for a, b in pairs}
        for fut in cf.as_completed(futs):
            r = fut.result()
            if r:
                results.append(r)
            done += 1
            if done % 500 == 0:
                print(f"  {done}/{len(pairs)}")
                OUT.write_text(json.dumps(results, ensure_ascii=False), encoding="utf-8")
    OUT.write_text(json.dumps(results, ensure_ascii=False), encoding="utf-8")
    print(f"wrote {len(results)} pair results to {OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
