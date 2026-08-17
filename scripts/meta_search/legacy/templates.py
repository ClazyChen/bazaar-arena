# -*- coding: utf-8 -*-
"""引擎模板库（阶段 3 提议器）。

实证依据（docs/bazaar-meta-evidence.md §2.1/§2.5）：卡组 = 引擎核心 + 流派无关填充。
模板 = (核心物品, 装配约束, 填充策略)。核心来自 Mak L8 meta 的流派聚类；
填充位取 elite 频率高的通用件（软先验，非硬剪枝）。

装配约束：
- CENTER：<item> 必须在物品数意义下居中（左右物品数相等）→ 总物品数须为奇数。
- NO_WEAPON：全卡组不得含 Weapon 标签（图书馆）。
- SOUL_MUTEX：魂石四变体两两互斥（基底 魂石 与变体可共存，与 meta 精英一致）。
"""

from __future__ import annotations

from dataclasses import dataclass, field

# 通用填充件（elite 频率软先验，按复测矩阵 top 卡组频率排序）
FILLERS = [
    "力量药水", "能量药水", "无敌药水", "琥珀", "活力药水", "彩虹药水",
    "耳环", "沙漏", "空白石碑", "嗅盐", "采掘工具", "永恒火炬", "智者之杖",
    "瓶装龙卷风", "符文药水", "香炉", "魔法石", "云精灵", "秘密配方",
    "魂石", "蛇怪之牙", "瘟疫长柄刀", "蕨叶蜘蛛", "毒液", "时间之砂",
]

WEAPON_TAG = "Weapon"
SOUL_VARIANT_SET = {"剧毒减速魂石", "剧毒冻结魂石", "灼烧减速魂石", "灼烧冻结魂石"}


@dataclass
class Template:
    name: str
    core: list[str]  # 引擎核心（有序无关，布局由装配约束决定）
    center_item: str | None = None  # 需要居中的物品
    no_weapon: bool = False
    fillers: list[str] = field(default_factory=lambda: list(FILLERS))




def check_assembly(items: list[str], tmpl: Template, db: dict[str, dict]) -> str | None:
    """返回 None 表示满足装配约束，否则返回违规原因。"""
    names = set(items)
    if tmpl.no_weapon:
        for n in names:
            if WEAPON_TAG in (db.get(n, {}).get("Tags", []) or []):
                return f"NO_WEAPON 违规: {n}"
    souls = (names & SOUL_VARIANT_SET) | ({"魂石"} if "魂石" in names else set())
    if len(souls) > 1:
        return f"SOUL_MUTEX 违规: {souls}"
    if tmpl.center_item:
        if tmpl.center_item not in names:
            return f"缺中心件 {tmpl.center_item}"
        if len(items) % 2 == 0:
            return "CENTER 要求总物品数为奇数"
    return None


def layout(items: list[str], tmpl: Template, db: dict[str, dict]) -> list[str]:
    """构造满足装配约束的代表布局（而非枚举排列后筛选）。

    规则：CENTER 物品置于物品数中位；其余按「核心居中、填充向两端」的默认启发式排布。
    布局仅作为等价类代表——类内差异由 perm_constraints 保证不敏感或另行消歧。
    """
    seq = list(items)
    if tmpl.center_item and tmpl.center_item in seq:
        seq.remove(tmpl.center_item)
        mid = len(seq) // 2  # 奇数总长 → 插入后左右各 len/2
        seq = seq[:mid] + [tmpl.center_item] + seq[mid:]
    return seq
