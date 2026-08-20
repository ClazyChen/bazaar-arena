# -*- coding: utf-8 -*-
"""GDF 等级规则的 Python 复刻（与 engine/src/bazaararena/gdf/GdfLevelRules.cpp 对齐）。

用途：把「物品签名」（逗号分隔有序物品名，含魂石/烙刀变体展示名）转换为
bazaararena_cli mode=simulate 的 items 数组，使 CLI 对战条件与 GDF 搜索时的
条件完全一致（战斗档位、任务进度覆写、overridable 按等级缩放）。

注意：本文件是 C++ 逻辑的**复刻**，C++ 侧规则变更时需同步（见各函数注释中的源码出处）。
"""

from __future__ import annotations

from pathlib import Path

import yaml

# ---- 战斗档位（GdfLevelRules::CombatTier）----

TIER_ORDER = ("bronze", "silver", "gold", "diamond")


def combat_tier(level: int) -> str:
    if level <= 4:
        return "bronze"
    if level <= 7:
        return "silver"
    if level <= 10:
        return "gold"
    return "diamond"


# ---- Mak 任务进度按等级覆写（GdfItemPrototypeCache::ComputeMakQuestOverride）----

def mak_quest_override(name: str, level: int) -> int | None:
    """返回指定等级下任务物品的 quest 位图覆写值；非任务物品返回 None。"""
    if name in ("时间之砂", "永恒火炬", "生命导体", "腐朽圣像"):
        if level <= 2:
            return 0
        if level <= 4:
            return 1
        if level <= 7:
            return 3
        return 7
    if name == "寒霜图腾":
        if level <= 5:
            return 0
        if level == 6:
            return 1
        if level == 7:
            return 3
        return 7
    if name == "先祖墓":
        if level <= 5:
            return 0
        if level == 6:
            return 1
        return 3
    if name == "空白石碑":
        if level <= 2:
            return 0
        if level == 3:
            return 1
        if level == 4:
            return 3
        if level <= 6:
            return 7
        if level <= 8:
            return 15
        return 31
    return None


# ---- 魂石四变体（引擎 ItemPool 注入的展示名 → db_key + quest 位图）----
# Q1=剧毒(1), Q2=灼烧(2), Q3=减速(4), Q4=冻结(8)

SOUL_VARIANTS = {
    "剧毒减速魂石": 1 + 4,
    "剧毒冻结魂石": 1 + 8,
    "灼烧减速魂石": 2 + 4,
    "灼烧冻结魂石": 2 + 8,
}

# ---- 烙刀双变体（引擎 ItemPool 注入的展示名 → db_key + quest 位图）----
# Q1=减速(1), Q2=加速(2)；与 ResolveItemAlias（DeckRep.cpp）一致

BRAND_VARIANTS = {
    "减速烙刀": 1,
    "加速烙刀": 2,
}


# ---- overridable 按等级缩放（GdfLevelRules::ComputeOverridableValue）----

def min_tier_index(item: dict) -> int:
    t = str(item.get("Tier", "Bronze")).lower()
    return TIER_ORDER.index(t) if t in TIER_ORDER else 0


def _tier_vals(item: dict, key: str) -> list[int]:
    """与 codegen 一致的 4 槽位展开：YAML 列表第 i 项 → 槽位 min_tier+i（越界钳制）。

    此前版本错误地按绝对档位索引（bronze=0 起），对 Silver/Gold 起步的物品
    （如 智者之杖 Silver：[5,10,15] 实为 silver/gold/diamond）产生系统性错位。
    """
    vals = item.get(key)
    if not isinstance(vals, list) or not vals:
        return []
    base = min_tier_index(item)
    out = []
    for slot in range(4):
        i = min(max(slot - base, 0), len(vals) - 1)
        out.append(int(vals[i]))
    return out


def overridable_value(item: dict, key: str, level: int) -> int:
    b, s, g, d = _tier_vals(item, key)
    if level <= 2:
        return b // 2
    if level == 3:
        return b
    if level in (4, 5):
        return (b + s) // 2
    if level == 6:
        return s
    if level in (7, 8):
        return (s + g) // 2
    if level == 9:
        return g
    if level in (10, 11):
        return (g + d) // 2
    if level == 12:
        return d
    return d + (level - 12) * (d - g) // 2


# ---- 物品库加载与签名转换 ----

_DB_CACHE: dict[str, dict[str, dict]] = {}


def load_item_db(data_dir: str | Path, hero: str = "mak") -> dict[str, dict]:
    key = f"{Path(data_dir).resolve()}::{hero}"
    if key not in _DB_CACHE:
        doc = yaml.safe_load(open(Path(data_dir) / f"{hero}.yaml", encoding="utf-8"))
        _DB_CACHE[key] = {it["Name"]: it for it in doc["items"]}
    return _DB_CACHE[key]


def signature_to_items(
    signature: str | list[str],
    level: int,
    db: dict[str, dict],
) -> list[dict]:
    """有序物品签名 → CLI items 数组（含 tier / quest / overridable 覆写）。

    signature 中可出现魂石/烙刀变体展示名；其余名字须为 db 中的物品 Name。
    """
    names = signature.split(",") if isinstance(signature, str) else list(signature)
    tier = combat_tier(level)
    items: list[dict] = []
    for raw in names:
        name = raw.strip()
        attrs: dict[str, int] = {}
        if name in SOUL_VARIANTS:
            base, quest = "魂石", SOUL_VARIANTS[name]
        elif name in BRAND_VARIANTS:
            base, quest = "烙刀", BRAND_VARIANTS[name]
        else:
            base = name
            quest = mak_quest_override(name, level)
        item = db.get(base, {})
        for ov_key in item.get("overridable", []) or []:
            attrs[ov_key.lower()] = overridable_value(item, ov_key, level)
        if quest is not None:
            attrs["quest"] = quest
        entry: dict = {"key": base, "tier": tier}
        if attrs:
            entry["attrsOverride"] = attrs
        items.append(entry)
    return items


def canonical_key(items: list[dict], level: int) -> str:
    """卡组的规范化键（用于缓存）：有序 (key,tier,attrs) 元组的稳定序列化。"""
    import json

    return json.dumps(
        {"level": level, "items": items}, ensure_ascii=False, sort_keys=True
    )
