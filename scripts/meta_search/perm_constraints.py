# -*- coding: utf-8 -*-
"""排列约束类：从物品 YAML 的能力 AST 机械提取位置约束，把同一 multiset 的排列
划分为「机制等价类」，每类只评估一个代表（对应 docs/bazaar-meta-evidence.md §2.2/§3.3）。

约束特征（对每个位置敏感物品提取）：
- CENTER_PARITY：效果取决于左右物品数量是否相等（如 天平 Eq/Ne(Count(左), Count(右))）。
- LEFT_NEIGHBOR / RIGHT_NEIGHBOR / ADJACENT：效果涉及相邻物品身份（如 镜子、沙漏、采掘工具）。
- LEFT_SIDE / RIGHT_SIDE：效果涉及一侧全体物品的成员构成（如 云精灵 加速左侧、塔兹迪亚匕首 左侧药水）。

两个排列等价 ⟺ 对每个受约束物品，其相关特征值完全一致：
- CENTER_PARITY → (左侧物品数 == 右侧物品数)
- NEIGHBOR → 相邻物品名（ multiset 成员身份）
- SIDE → 该侧物品的有序多重集
"""

from __future__ import annotations

import itertools
from dataclasses import dataclass

# ---- AST 模式识别 ----

_CENTER_OPS = {"Eq", "Ne"}
_SIDE_PREDICATES = {
    "StrictlyLeftOfCaster": "LEFT_SIDE",
    "StrictlyRightOfCaster": "RIGHT_SIDE",
    "LeftOfCaster": "LEFT_SIDE",
    "RightOfCaster": "RIGHT_SIDE",
}
_NEIGHBOR_PREDICATES = {
    "AdjacentToCaster": "ADJACENT",
    "LeftAdjacentToCaster": "LEFT_NEIGHBOR",
    "RightAdjacentToCaster": "RIGHT_NEIGHBOR",
    "AdjacentToCasterLeft": "LEFT_NEIGHBOR",
    "AdjacentToCasterRight": "RIGHT_NEIGHBOR",
}


def _walk_types(node, out: list):
    """按先序收集 (type, params) 对，供模式匹配。"""
    if isinstance(node, dict):
        t = node.get("type")
        if isinstance(t, str):
            out.append((t, node.get("params")))
        for v in node.values():
            _walk_types(v, out)
    elif isinstance(node, list):
        for x in node:
            _walk_types(x, out)


def _is_count_of_side(node) -> str | None:
    """识别 Count(...StrictlyLeftOfCaster...) / Count(...StrictlyRightOfCaster...) 结构。"""
    if not isinstance(node, dict) or node.get("type") != "Count":
        return None
    found: list[str] = []
    _collect_side_predicates(node.get("params"), found)
    for pred in found:
        if pred in _SIDE_PREDICATES:
            return _SIDE_PREDICATES[pred]
    return None


def _collect_side_predicates(node, out: list[str]):
    if isinstance(node, dict):
        t = node.get("type")
        if isinstance(t, str):
            out.append(t)
        for v in node.values():
            _collect_side_predicates(v, out)
    elif isinstance(node, list):
        for x in node:
            _collect_side_predicates(x, out)
    elif isinstance(node, str):
        out.append(node)


def item_constraints(item: dict) -> set[str]:
    """从物品定义提取位置约束特征集合。"""
    cons: set[str] = set()
    abilities = item.get("Abilities", []) or []
    auras = item.get("Auras", []) or []
    passives = item.get("Passives", []) or []

    for ab in abilities + passives:
        cond = ab.get("ex_condition") or ab.get("condition")
        if isinstance(cond, dict) and cond.get("type") in _CENTER_OPS:
            params = cond.get("params", [])
            if isinstance(params, list) and len(params) == 2:
                sides = {_is_count_of_side(p) for p in params}
                if sides == {"LEFT_SIDE", "RIGHT_SIDE"}:
                    cons.add("CENTER_PARITY")
        # 条件/目标谓词中的位置引用
        found: list[str] = []
        _collect_side_predicates(ab, found)
        for pred in found:
            if pred in _SIDE_PREDICATES:
                cons.add(_SIDE_PREDICATES[pred])
            elif pred in _NEIGHBOR_PREDICATES:
                cons.add(_NEIGHBOR_PREDICATES[pred])
    for au in auras:
        found = []
        _collect_side_predicates(au, found)
        for pred in found:
            if pred in _SIDE_PREDICATES:
                cons.add(_SIDE_PREDICATES[pred])
            elif pred in _NEIGHBOR_PREDICATES:
                cons.add(_NEIGHBOR_PREDICATES[pred])
    # CENTER_PARITY 蕴含 SIDE 依赖已由布尔特征覆盖，不再重复展开
    return cons


# ---- 排列等价类 ----


def _perm_signature(perm: tuple[str, ...], constraints: dict[str, set[str]]) -> tuple:
    """排列的特征签名：对每个受约束物品计算其约束特征值。"""
    n = len(perm)
    feats: list[tuple] = []
    for idx, name in enumerate(perm):
        cons = constraints.get(name)
        if not cons:
            continue
        left = perm[:idx]
        right = perm[idx + 1:]
        entry: list = [name]
        if "CENTER_PARITY" in cons:
            entry.append(("CENTER", len(left) == len(right)))
        if "LEFT_NEIGHBOR" in cons:
            entry.append(("LN", left[-1] if left else None))
        if "RIGHT_NEIGHBOR" in cons:
            entry.append(("RN", right[0] if right else None))
        if "ADJACENT" in cons:
            entry.append(("ADJ", (left[-1] if left else None, right[0] if right else None)))
        if "LEFT_SIDE" in cons:
            entry.append(("LS", tuple(sorted(left))))
        if "RIGHT_SIDE" in cons:
            entry.append(("RS", tuple(sorted(right))))
        feats.append(tuple(entry))
    return tuple(sorted(feats, key=repr))


@dataclass
class PermClass:
    signature: tuple
    representative: tuple[str, ...]
    size: int  # 类内排列数


def partition_permutations(
    multiset: list[str],
    db: dict[str, dict],
    *,
    max_enumerate: int = 200000,
) -> list[PermClass]:
    """把 multiset 的全部唯一排列按机制等价类划分。

    无任何位置约束时直接返回单类（字典序首个排列为代表）。
    唯一排列数超过 max_enumerate 时抛出 ValueError（调用方应改用抽样）。
    """
    constraints = {name: item_constraints(db[name]) for name in set(multiset) if name in db}
    if not any(constraints.values()):
        return [PermClass((), tuple(sorted(multiset)), 1)]

    uniq = sorted(set(multiset))
    counts = {u: multiset.count(u) for u in uniq}
    total = 1
    import math

    n = len(multiset)
    total = math.factorial(n)
    for c in counts.values():
        total //= math.factorial(c)
    if total > max_enumerate:
        raise ValueError(f"unique permutations {total} > {max_enumerate}")

    classes: dict[tuple, PermClass] = {}
    for perm in itertools.permutations(sorted(multiset)):
        # permutations() 对重复元素会产生重复序列；用集合去重的代价高于直接签名聚合
        sig = _perm_signature(perm, constraints)
        if sig in classes:
            classes[sig].size += 1
        else:
            classes[sig] = PermClass(sig, tuple(perm), 1)
    # 去重后各类 size 含重复序列的权重；此处保留枚举权重（不影响类划分）
    out = sorted(classes.values(), key=lambda c: -c.size)
    # 修正 size 为枚举权重（重复序列计入），调用方仅用于排序参考
    return out
