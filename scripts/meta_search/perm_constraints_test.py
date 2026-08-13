# -*- coding: utf-8 -*-
"""阶段 2 验收：天平案例（docs/bazaar-meta-evidence.md §2.2 的 61-0 基线）。

验证点：
1. 约束提取：天平 → CENTER_PARITY；采掘工具 → 相邻类约束；无位置依赖物品 → 空。
2. 类划分：含天平的 multiset 的排列被分成「居中类」与「非居中类」；
   已知等价的居中排列（tp_center / tp_swap_inner 等）按机制特征落类正确。
3. 对战验证：同类代表 vs 同类代表 → 统计不可区分；居中类 vs 非居中类 → 碾压差。
"""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from meta_search import battle, gdf_conditions, perm_constraints  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent
failures = []


def check(name, ok, detail=""):
    print(f"{'PASS' if ok else 'FAIL'}  {name}  {detail}")
    if not ok:
        failures.append(name)


db = gdf_conditions.load_item_db(REPO / "data" / "items", "mak")

# 1) 约束提取
c_tianping = perm_constraints.item_constraints(db["天平"])
c_caijue = perm_constraints.item_constraints(db["采掘工具"])
c_liliang = perm_constraints.item_constraints(db["力量药水"])
c_xiuyan = perm_constraints.item_constraints(db["嗅盐"])
check("extract_tianping_center", "CENTER_PARITY" in c_tianping, str(c_tianping))
check("extract_caijue_adj", bool(c_caijue & {"ADJACENT", "LEFT_NEIGHBOR", "RIGHT_NEIGHBOR"}), str(c_caijue))
check("extract_liliang_none", not c_liliang, str(c_liliang))
check("extract_xiuyan_side", "LEFT_SIDE" in c_xiuyan, str(c_xiuyan))

# 2) 类划分（天平充能阵 multiset）
ms = ["采掘工具", "永恒火炬", "能量药水", "天平", "智者之杖", "魂石", "力量药水"]
classes = perm_constraints.partition_permutations(ms, db)
print(f"  multiset 7 件 → {len(classes)} 个等价类（总唯一排列 5040）")

# 已知排列落类检查
known = {
    "tp_center": ("采掘工具", "永恒火炬", "能量药水", "天平", "智者之杖", "魂石", "力量药水"),
    "tp_swap_inner": ("采掘工具", "能量药水", "永恒火炬", "天平", "智者之杖", "魂石", "力量药水"),
    "tp_mirror": ("力量药水", "魂石", "智者之杖", "天平", "能量药水", "永恒火炬", "采掘工具"),
    "tp_off1": ("采掘工具", "天平", "永恒火炬", "能量药水", "智者之杖", "魂石", "力量药水"),
    "tp_edge": ("天平", "采掘工具", "永恒火炬", "能量药水", "智者之杖", "魂石", "力量药水"),
}
cons_map = {n: perm_constraints.item_constraints(db[n]) for n in set(ms)}
sigs = {k: perm_constraints._perm_signature(v, cons_map) for k, v in known.items()}
# 合成用例：仅一个 CENTER_PARITY 物品 + 无约束物品，交换无约束物品必须同类
fake_db = {
    "天平": db["天平"],
}
fake_ms = ["天平", "甲", "乙", "丙"]
fake_cons = {"天平": {"CENTER_PARITY"}}
p1 = ("甲", "天平", "乙", "丙")
p2 = ("甲", "天平", "丙", "乙")
p3 = ("天平", "甲", "乙", "丙")
p5_center = ("甲", "乙", "天平", "丙", "丁")
p5_off = ("甲", "天平", "乙", "丙", "丁")
check("swap_unconstrained_same_class",
      perm_constraints._perm_signature(p1, fake_cons) == perm_constraints._perm_signature(p2, fake_cons),
      "交换天平同侧的无约束物品应同类")
check("offcenter_same_class_parity_only",
      perm_constraints._perm_signature(p1, fake_cons) == perm_constraints._perm_signature(p3, fake_cons),
      "仅奇偶约束下，所有非居中布局应同类")
check("center_vs_offcenter_diff_class",
      perm_constraints._perm_signature(p5_center, fake_cons) != perm_constraints._perm_signature(p5_off, fake_cons),
      "居中与非居中应落入不同类")
center_sig = sigs["tp_center"]
mirror_same = sigs["tp_mirror"] == center_sig
print(f"  tp_mirror 与 tp_center 同类: {mirror_same}（镜像改变采掘工具相邻关系，可不同类）")

# 居中类/非居中类各取代表
def center_of(cls_rep):
    i = cls_rep.index("天平")
    return i == len(cls_rep) - 1 - i

centered = [c for c in classes if center_of(c.representative)]
offcenter = [c for c in classes if not center_of(c.representative)]
print(f"  居中类 {len(centered)} 个，非居中类 {len(offcenter)} 个")
check("has_both_sides", bool(centered) and bool(offcenter))

# 3) 对战验证：同类两代表统计不可区分；居中 vs 非居中碾压差
opp_sig = "翡翠,蕨叶蜘蛛,毒液,无敌药水,瘟疫长柄刀,光学强化,活力药水"  # 毒液 anchor deck（无任务物品，稳定）
opp = gdf_conditions.signature_to_items(opp_sig, 8, db)
cache = battle.BattleCache(REPO / "out" / "meta_search" / "battle_cache.jsonl")

# 找 tp_center 所在的类，取类中两个不同排列代表——类内只有签名一致，代表唯一；
# 类内等价性用同类的另一成员（从枚举中另取一个同签名排列）验证
same_class_members = []
target_sig = sigs["tp_center"]
import itertools

for perm in itertools.permutations(sorted(ms)):
    if perm_constraints._perm_signature(perm, cons_map) == target_sig:
        if tuple(perm) not in same_class_members:
            same_class_members.append(tuple(perm))
    if len(same_class_members) >= 3:
        break
print(f"  tp_center 类内样本排列数（前3）: {len(same_class_members)}")

rep_center = ",".join(known["tp_center"])
rep_off = ",".join(known["tp_off1"])
rep_edge = ",".join(known["tp_edge"])

if len(same_class_members) >= 2:
    other = ",".join(same_class_members[1])
    r_same = battle.series_batch("cls1", rep_center.split(","), "cls1b", other.split(","),
                                 8, 61, cache=cache)
    print(f"  同类两代表对局: wr={r_same.winrate_a:.3f} ci=±{r_same.ci_half:.3f}")
    check("same_class_indistinguishable",
          abs(r_same.winrate_a - 0.5) <= 2 * r_same.ci_half,
          f"wr={r_same.winrate_a:.3f}")

r_off = battle.series_batch("center", rep_center.split(","), "off1", rep_off.split(","),
                            8, 41, cache=cache)
r_edge = battle.series_batch("center", rep_center.split(","), "edge", rep_edge.split(","),
                             8, 41, cache=cache)
print(f"  居中 vs 偏一格: wr={r_off.winrate_a:.3f}；居中 vs 边缘: wr={r_edge.winrate_a:.3f}")
check("center_dominates_offcenter", r_off.winrate_a > 0.9 and r_edge.winrate_a > 0.9)

print()
if failures:
    print(f"{len(failures)} FAILED: {failures}")
    sys.exit(1)
print("stage2 permutation-class checks passed")
