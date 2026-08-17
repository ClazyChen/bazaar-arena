# -*- coding: utf-8 -*-
"""引擎核心挖掘：在理由图上枚举高内部闭包的 2–4 件子集，替代手写模板库。

方法：
- 从有理由边的物品对出发，经共享邻域扩展为 3、4 件闭包；
- 评分 = 内部理由边总数（双向计）；要求每个成员至少 1 条内部边（闭包性）；
- 近重复合并（成员 Jaccard ≥ 0.75 时保留高分者）；
- 输出候选引擎清单与每条内部边的理由文本，供人工审核。
"""

from __future__ import annotations

import itertools
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent.parent))
from meta_search.legacy.reason_graph import ReasonGraph, load  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent.parent
SIZES = {"Small": 1, "Medium": 2, "Large": 3}


def mine_engines(g: ReasonGraph, *, max_members: int = 4, max_slots: int = 8,
                 min_pair: float = 2.0, per_member: float = 2.0,
                 family_quota: int = 5, beam: int | None = None,
                 apply_dedupe: bool = True):
    """加权闭包挖掘。家族配额：按枢纽成员（邻域最大者）分组，每组至多 quota 个。

    beam=None（默认）不做截断——完备性不应依赖截断旋钮；
    apply_dedupe=False 返回未去重的全量闭包（覆盖自检用）。
    """
    names = sorted(g.db.keys())

    def size_of(sub):
        return sum(SIZES.get(g.db[n].get("Size", ""), 1) for n in sub)

    def closure_ok(sub):
        return all(
            any(b in g.edges.get(a, {}) or a in g.edges.get(b, {}) for b in sub if b != a)
            for a in sub
        )

    found: dict[tuple[str, ...], float] = {}
    for a in names:
        for b in g.edges.get(a, {}):
            sub = tuple(sorted((a, b)))
            if size_of(sub) > max_slots or sub in found:
                continue
            sc = g.internal_edges(list(sub))
            if sc >= min_pair:
                found[sub] = sc
    frontier = sorted(found.items(), key=lambda kv: -kv[1])
    if beam:
        frontier = frontier[:beam]
    for k in range(3, max_members + 1):
        nxt: dict[tuple[str, ...], float] = {}
        for sub, _sc in frontier:
            votes: dict[str, int] = {}
            for m in sub:
                for c in g.neighbors(m):
                    if c not in sub:
                        votes[c] = votes.get(c, 0) + 1
            for c, v in votes.items():
                if v < 2:
                    continue
                new = tuple(sorted(sub + (c,)))
                if len(new) != k or new in nxt or size_of(new) > max_slots or not closure_ok(new):
                    continue
                sc = g.internal_edges(list(new))
                if sc >= per_member * k:
                    nxt[new] = sc
        found.update(nxt)
        frontier = sorted(nxt.items(), key=lambda kv: -kv[1])
        if beam:
            frontier = frontier[:beam]
        if not frontier:
            break
    if not apply_dedupe:
        return sorted(((sc, sub) for sub, sc in found.items()), key=lambda t: -t[0])
    # 近重复合并 + 家族配额（枢纽 = 邻域最大的成员）
    ranked = sorted(found.items(), key=lambda kv: -kv[1])
    kept: list[tuple[float, tuple[str, ...]]] = []
    family_count: dict[str, int] = {}
    for sub, sc in ranked:
        if any(len(set(sub) & set(k)) / len(set(sub) | set(k)) >= 0.75 for _, k in kept):
            continue
        hub = max(sub, key=lambda n: len(g.neighbors(n)))
        if family_count.get(hub, 0) >= family_quota:
            continue
        family_count[hub] = family_count.get(hub, 0) + 1
        kept.append((sc, sub))
    return kept


def mine_engines_v2(g: ReasonGraph, *, max_members: int = 5, max_slots: int = 8,
                    min_pair: float = 1.0, per_member: float = 1.5,
                    core_bonus_weight: float = 8.0,
                    family_quota: int = 3, channel_quota: int = 8,
                    apply_dedupe: bool = True):
    """核心播种 + 角色感知的引擎挖掘（v2）。

    与 v1 的差别：
    - 引擎必须含 ≥1 个数值核心（成长型/触发收益型/高基础型，见 reason_graph.core_roles）；
    - 评分 = 闭包理由分 + core_bonus_weight × 核心强度分
      （数值量级 × 触发频率权重，按 key 的 p90 归一）——「边多但数值低」不再压过「核心强」；
    - 两级家族配额：核心件配额（family_quota）+ 核心通道配额（channel_quota，
      按 Burn/Poison/Damage/Regen… 分组）——多样性直接落到机制轴上。
    """
    names = sorted(g.db.keys())

    def size_of(sub):
        return sum(SIZES.get(g.db[n].get("Size", ""), 1) for n in sub)

    def closure_ok(sub):
        return all(
            any(b in g.edges.get(a, {}) or a in g.edges.get(b, {}) for b in sub if b != a)
            for a in sub
        )

    def has_core(sub):
        return any(g.core_score.get(n, 0) > 0 for n in sub)

    def score(sub):
        closure = g.internal_edges(list(sub))
        core_bonus = max((g.core_score.get(n, 0) for n in sub), default=0)
        return closure + core_bonus_weight * core_bonus

    def core_of(sub):
        return max(sub, key=lambda n: g.core_score.get(n, 0))

    found: dict[tuple[str, ...], float] = {}
    # 2 件：核心件 × 其邻域
    for a in names:
        if g.core_score.get(a, 0) <= 0:
            continue
        for b in g.neighbors(a):
            sub = tuple(sorted((a, b)))
            if size_of(sub) > max_slots or sub in found:
                continue
            sc = score(sub)
            if sc >= min_pair and closure_ok(sub):
                found[sub] = sc
    frontier = list(found.keys())
    for k in range(3, max_members + 1):
        nxt: dict[tuple[str, ...], float] = {}
        for sub in frontier:
            votes: dict[str, int] = {}
            for m in sub:
                for c in g.neighbors(m):
                    if c not in sub:
                        votes[c] = votes.get(c, 0) + 1
            for c, v in votes.items():
                if v < 2:  # 闭包密度门槛：新成员须与 ≥2 个现有成员有边
                    continue
                new = tuple(sorted(sub + (c,)))
                if len(new) != k or new in nxt or size_of(new) > max_slots or not closure_ok(new):
                    continue
                sc = score(new)
                if sc >= per_member * k:
                    nxt[new] = sc
        found.update(nxt)
        frontier = list(nxt.keys())
    if not apply_dedupe:
        return sorted(((sc, sub) for sub, sc in found.items()), key=lambda t: -t[0])
    ranked = sorted(found.items(), key=lambda kv: -kv[1])
    kept: list[tuple[float, tuple[str, ...]]] = []
    family_count: dict[str, int] = {}
    channel_count: dict[str, int] = {}
    for sub, sc in ranked:
        if any(len(set(sub) & set(k)) / len(set(sub) | set(k)) >= 0.75 for _, k in kept):
            continue
        fam = core_of(sub)
        ch = g.core_channel.get(fam, "?")
        if family_count.get(fam, 0) >= family_quota or channel_count.get(ch, 0) >= channel_quota:
            continue
        family_count[fam] = family_count.get(fam, 0) + 1
        channel_count[ch] = channel_count.get(ch, 0) + 1
        kept.append((sc, sub))
    return kept


def main() -> int:
    import argparse

    p = argparse.ArgumentParser()
    p.add_argument("--top", type=int, default=30)
    p.add_argument("--max-members", type=int, default=4)
    p.add_argument("--max-slots", type=int, default=8)
    p.add_argument("--v2", action="store_true", help="核心播种+数值加权挖掘")
    args = p.parse_args()

    g = load(REPO / "data" / "items", "mak")
    if args.v2:
        kept = mine_engines_v2(g, max_members=args.max_members, max_slots=args.max_slots)
    else:
        kept = mine_engines(g, max_members=args.max_members, max_slots=args.max_slots)
    print(f"候选引擎 {len(kept)} 个（去重后），top {args.top}:\n")
    for sc, sub in kept[: args.top]:
        print(f"[score={sc}] {' + '.join(sub)}")
        for a, b in itertools.permutations(sub, 2):
            for r in g.reasons_between(a, b):
                print(f"    {b} → {a}: {r}")
        print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
