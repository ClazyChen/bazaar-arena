# -*- coding: utf-8 -*-
"""把收敛认证后的 Mak 精英卡组导入 Web 前端数据库（bazaararena.db），按等级分集合。

牌表来源：收敛闭环产物（docs/meta/mak-l2.md … mak-l17.md；
out/elite_report/*/final_decks.json）。档位与覆写复用 scripts/meta_search/gdf_conditions.py，
保证前端对局与真值层逐帧一致：战斗档位按等级、任务物品 quest 覆写、
overridable 缩放、魂石变体展开为 魂石+quest 位图。

幂等：同名 collection 存在时整组删除重建（ON DELETE CASCADE）；同时清理历史旧集合。

用法（仓库根目录）：
    python scripts/import_reference_decks_mak.py              # 导入全部等级
    python scripts/import_reference_decks_mak.py --level 8    # 只导入某一等级
    python scripts/import_reference_decks_mak.py --db <path>  # 覆盖数据库路径
"""

from __future__ import annotations

import argparse
import sqlite3
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from meta_search.gdf_conditions import load_item_db, signature_to_items

TIER_TO_INT = {"bronze": 0, "silver": 1, "gold": 2, "diamond": 3, "legendary": 4}
SLOT_ATTR_KEYS = ("custom_0", "custom_1", "custom_2", "custom_3", "custom_4", "quest")

# 历史集合名（导入时清理）
LEGACY_COLLECTIONS = [
    "Mak L8 参照精英（真值层）",
    "Mak L8 参照精英（真值层 v3·物品修复后）",
    "Mak L2 精英（收敛认证 · v3）",
    "Mak L5 精英（收敛认证 · v3）",
    "Mak L8 精英（收敛认证 · v3）",
    "Mak L11 精英（收敛认证 · v3）",
    "Mak L14 精英（收敛认证 · v3）",
    "Mak L17 精英（高原代表 · v3）",
]

# 等级 → (集合名, [(卡组名, 有序物品签名), ...])
# 顺序 = 前端从左到右摆位，与真值层评测一致。
LEVEL_DECKS: dict[int, tuple[str, list[tuple[str, list[str]]]]] = {
    17: ("Mak L17 精英（高原代表 · v4）", [
        ("精英 恶臭干扰·魂戒",
         ["昏睡药水", "魂戒", "挎包", "碎瓶", "霜冻药水", "能量药水", "活力药水", "恶臭蘑菇"]),
        ("精英 恶臭干扰·日光矛",
         ["日光矛", "无限药水", "能量药水", "恶臭蘑菇", "挎包", "飞行药水", "昏睡药水"]),
        ("精英 智者杖天平·云精灵",
         ["智者之杖", "云精灵", "恶臭蘑菇", "天平", "力量药水", "缩小药水", "彩虹药水"]),
        ("精英 智者杖恶臭·冰霜（σ2）",
         ["缩小药水", "冰霜之怖", "蓝宝石", "恶臭蘑菇", "智者之杖", "能量药水", "力量药水"]),
        ("精英 智者杖恶臭·煅烧釜",
         ["智者之杖", "嗅盐", "恶臭蘑菇", "煅烧釜", "力量药水", "昏睡药水", "缩小药水"]),
        ("精英 摆锤智者杖·采样仪（σ1）",
         ["大气采样仪", "蕨叶蜘蛛", "摆锤", "智者之杖", "力量药水", "符文药水"]),
        ("精英 智者杖恶臭·龙卷",
         ["智者之杖", "能量药水", "缩小药水", "恶臭蘑菇", "瓶装龙卷风", "冰霜之怖", "力量药水"]),
        ("流派 百足鼬图书馆黑冰",
         ["飞行药水", "大气采样仪", "冰霜之怖", "图书馆", "地窖"]),
        ("流派 采样仪石碑火炬",
         ["大气采样仪", "图书馆", "寒霜图腾", "永恒火炬", "飞行药水"]),
        ("流派 采样仪图书馆寒霜",
         ["剧毒冻结魂石", "寒霜图腾", "蓝宝石", "图书馆", "大气采样仪", "飞行药水"]),
        ("流派 冰霜烈焰冻结",
         ["地窖", "图书馆", "大气采样仪", "剧毒冻结魂石", "蓝宝石", "采掘工具"]),
    ]),
    14: ("Mak L14 精英（收敛认证 · v4）", [
        ("精英 天平碎瓶龙卷",
         ["力量药水", "碎瓶", "能量药水", "天平", "采掘工具", "智者之杖", "恶臭蘑菇"]),
        ("精英 智者杖干扰·魂石（σ2）",
         ["缩小药水", "活力药水", "智者之杖", "嗅盐", "剧毒减速魂石", "恶臭蘑菇", "力量药水", "琥珀"]),
        ("精英 智者杖天平·缩小",
         ["瓶装龙卷风", "智者之杖", "云精灵", "天平", "恶臭蘑菇", "力量药水", "无敌药水"]),
        ("精英 智者杖天平·无敌",
         ["能量药水", "智者之杖", "瓶装龙卷风", "天平", "力量药水", "无敌药水", "恶臭蘑菇"]),
        ("精英 智者杖天平·光学",
         ["昏睡药水", "智者之杖", "光学强化", "天平", "能量药水", "冰霜之怖", "蓝宝石"]),
        ("精英 智者杖天平·沙漏（σ1）",
         ["能量药水", "无敌药水", "智者之杖", "天平", "灼烧冻结魂石", "力量药水", "冰霜之怖"]),
        ("精英 采样仪图书馆·地窖（σ3 针对）",
         ["大气采样仪", "冰霜之怖", "蓝宝石", "地窖", "图书馆"]),
        ("流派 图书馆采样仪冻结",
         ["冰霜之怖", "地窖", "飞行药水", "图书馆", "大气采样仪"]),
        ("流派 蕨叶摆锤毒",
         ["恶臭蘑菇", "蕨叶蜘蛛", "瘟疫长柄刀", "剧毒冻结魂石", "蓝宝石", "采掘工具"]),
        ("流派 采样仪图书馆·黑冰",
         ["图书馆", "采掘工具", "灼烧冻结魂石", "蓝宝石", "冰霜之怖", "黑冰"]),
        ("流派 镜子图书馆冻结",
         ["大气采样仪", "永恒火炬", "寒霜图腾", "水银", "图书馆"]),
    ]),
    11: ("Mak L11 精英（收敛认证 · v4）", [
        ("精英 智者杖天平·龙卷",
         ["智者之杖", "瓶装龙卷风", "缩小药水", "天平", "能量药水", "恶臭蘑菇", "力量药水"]),
        ("精英 智者杖干扰·恶臭（σ1）",
         ["恶臭蘑菇", "符文药水", "能量药水", "天平", "无敌药水", "力量药水", "智者之杖"]),
        ("精英 智者杖天平·云精灵",
         ["力量药水", "智者之杖", "云精灵", "天平", "缩小药水", "恶臭蘑菇", "琥珀"]),
        ("精英 图书馆冻结毒·黑冰（σ2）",
         ["冰霜之怖", "图书馆", "黑冰", "采掘工具", "灼烧冻结魂石", "蓝宝石"]),
        ("精英 图书馆冻结·沙漏",
         ["黑冰", "冰霜之怖", "沙漏", "剧毒冻结魂石", "蓝宝石", "图书馆"]),
        ("精英 图书馆冻结毒·寒霜",
         ["黑冰", "图书馆", "剧毒冻结魂石", "寒霜图腾", "冰霜之怖"]),
        ("精英 采样仪图书馆·沙漏",
         ["图书馆", "大气采样仪", "水银", "沙漏", "灼烧冻结魂石", "蓝宝石", "采掘工具"]),
        ("精英 蕨叶摆锤毒（σ3 针对）",
         ["盗龙轿辇", "蕨叶蜘蛛", "摆锤", "剧毒冻结魂石", "蓝宝石", "飞行药水"]),
        ("流派 冰霜冻结毒·瘟疫",
         ["采掘工具", "瘟疫长柄刀", "剧毒冻结魂石", "黑冰", "恶臭蘑菇", "霜冻药水"]),
        ("流派 采样仪石碑·耳环",
         ["飞行药水", "大气采样仪", "图书馆", "耳环", "空白石碑", "灼烧冻结魂石"]),
        ("流派 冻结控制·L8王形态",
         ["剧毒冻结魂石", "冰霜之怖", "黑冰", "冰爪", "采掘工具", "能量药水", "霜冻药水"]),
        ("流派 剑杖摆锤",
         ["图书馆", "大气采样仪", "永恒火炬", "寒霜图腾", "灼烧冻结魂石"]),
    ]),
    8: ("Mak L8 精英（收敛认证 · v4）", [
        ("精英 摆锤图书馆·显微镜",
         ["灼烧冻结魂石", "图书馆", "马克显微镜", "摆锤", "永恒火炬", "沙漏"]),
        ("精英 冰霜冻结王（σ1）",
         ["剧毒冻结魂石", "冰霜之怖", "寒霜图腾", "冰爪", "光学强化", "彩虹药水", "无敌药水"]),
        ("精英 冰爪天平冻结",
         ["冰爪", "嗅盐", "能量药水", "天平", "剧毒冻结魂石", "寒霜图腾", "蓝宝石"]),
        ("精英 寒霜冻结毒",
         ["采掘工具", "冰爪", "力量药水", "剧毒冻结魂石", "寒霜图腾", "蓝宝石", "能量药水", "符文药水"]),
        ("精英 股骨重炮（σ2）",
         ["力量药水", "琥珀", "符文药水", "时间之砂", "嗅盐", "灼烧减速魂石", "马格努斯的股骨"]),
        ("精英 蕨叶摆锤冻结（σ3）",
         ["蕨叶蜘蛛", "摆锤", "永恒火炬", "寒霜图腾", "蓝宝石", "无敌药水"]),
        ("精英 瘟疫毒摆锤",
         ["瘟疫长柄刀", "摆锤", "蕨叶蜘蛛", "毒液", "昏睡药水", "力量药水"]),
        ("精英 智者杖冰爪充能",
         ["力量药水", "冰爪", "能量药水", "灼烧减速魂石", "寒霜图腾", "智者之杖", "采掘工具"]),
        ("流派 药水装填·炼金炉",
         ["符文药水", "无敌药水", "瓶装龙卷风", "炼金炉", "飞行药水", "沸腾烧瓶", "碎瓶"]),
        ("流派 图书馆火炬壳",
         ["采掘工具", "永恒火炬", "嗅盐", "灼烧减速魂石", "力量药水", "琥珀", "图书馆"]),
        ("流派 自毒注射",
         ["肾上腺素调节服", "快速注射系统", "蕨叶蜘蛛", "瘟疫长柄刀", "光学强化"]),
        ("流派 注能自毒武器",
         ["倒刺利爪", "实验体阿尔法", "蜘蛛连枷", "注能护腕", "水蛭", "力量药水"]),
    ]),
    5: ("Mak L5 精英（收敛认证 · v4）", [
        ("精英 自毒注射·蛇首手杖",
         ["蛇首手杖", "水银", "快速注射系统", "蜘蛛连枷", "注能护腕", "肾上腺素调节服"]),
        ("精英 暴击武器·双射弓（σ=1.000）",
         ["手术刀", "嗅盐", "符文双射弓", "魔法飞毯", "永恒火炬", "力量药水", "云精灵"]),
        ("精英 飞行巨龙·渡鸦",
         ["贪婪渡鸦", "空惧巨龙", "萤火虫", "永恒火炬", "红宝石", "灼烧减速魂石"]),
        ("精英 飞行巨龙·符文",
         ["符文药水", "萤火虫", "空惧巨龙", "永恒火炬", "嗅盐", "灼烧减速魂石", "红宝石"]),
        ("精英 飞毯双射弓",
         ["魔法飞毯", "拆信刀", "无敌药水", "力量药水", "碎瓶", "彩虹药水", "瓶装龙卷风", "符文双射弓"]),
        ("流派 自毒注射·药房版",
         ["肾上腺素调节服", "药房", "快速注射系统", "腐朽圣像", "光学强化"]),
        ("流派 石碑火炬",
         ["力量药水", "红宝石", "香炉", "空白石碑", "永恒火炬", "嗅盐", "灼烧减速魂石", "无敌药水"]),
        ("流派 研钵武器巨龙",
         ["萤火虫", "瓶装龙卷风", "无敌药水", "研钵与研杵", "空惧巨龙", "猫头鹰奥利", "瓶装闪电"]),
        ("流派 火炬壳·香炉",
         ["香炉", "无敌药水", "彩虹药水", "永恒火炬", "嗅盐", "灼烧减速魂石", "云精灵", "红宝石", "力量药水"]),
        ("流派 药水装填·炼金炉",
         ["无敌药水", "飞行药水", "炼金炉", "符文药水", "红宝石", "彩虹药水", "瓶装龙卷风", "碎瓶"]),
        ("流派 药水装填·碎瓶",
         ["碎瓶", "红宝石", "瓶装龙卷风", "符文药水", "炼金炉", "飞行药水", "无限药水", "无敌药水"]),
    ]),
    2: ("Mak L2 精英（收敛认证 · v4）", [
        ("精英 灼烧王（σ=1.000）",
         ["瓶装闪电", "火焰药水", "红宝石", "香炉", "蜡烛"]),
        ("精英 剑杖混搭·香炉",
         ["鳄鱼眼泪", "彩虹药水", "剑杖", "香炉", "红宝石"]),
        ("精英 剑杖混搭·煅烧釜",
         ["剑杖", "香炉", "红宝石", "煅烧釜"]),
        ("流派 地刺陷阱毒",
         ["毒蜥", "倒刺利爪", "毒液", "翡翠", "鳄鱼眼泪", "真菌孢子"]),
        ("流派 水蛭毒",
         ["翡翠", "真菌孢子", "毒液注射", "毒蜥", "剑杖"]),
    ]),
}


def import_level(conn: sqlite3.Connection, item_db: dict, sizes: dict[str, int],
                 level: int) -> None:
    collection_name, decks = LEVEL_DECKS[level]
    budget = 4 if level <= 1 else 6 if level == 2 else 8 if level == 3 else 10

    converted: list[tuple[str, list[dict]]] = []
    for deck_name, names in decks:
        items = signature_to_items(names, level, item_db)
        total = sum(sizes.get(it["key"], 99) for it in items)
        if total > budget:
            raise SystemExit(f"卡组 {deck_name} 超槽位：{total} > {budget}")
        converted.append((deck_name, items))

    for name in {collection_name, *LEGACY_COLLECTIONS}:
        row = conn.execute("SELECT id FROM deck_collections WHERE name = ?", (name,)).fetchone()
        if row:
            conn.execute("DELETE FROM deck_collections WHERE id = ?", (int(row["id"]),))

    sort_order = int(
        conn.execute("SELECT COALESCE(MAX(sort_order), -1) + 1 FROM deck_collections").fetchone()[0]
    )
    now = datetime.now(timezone.utc).replace(microsecond=0).isoformat()
    cur = conn.execute(
        "INSERT INTO deck_collections (name, sort_order, created_at) VALUES (?, ?, ?)",
        (collection_name, sort_order, now),
    )
    cid = int(cur.lastrowid)
    for i, (deck_name, items) in enumerate(converted):
        cur = conn.execute(
            "INSERT INTO decks (collection_id, name, player_level, sort_order) VALUES (?, ?, ?, ?)",
            (cid, deck_name, level, i),
        )
        did = int(cur.lastrowid)
        for pos, it in enumerate(items):
            ao = {k.lower(): v for k, v in (it.get("attrsOverride") or {}).items()}
            conn.execute(
                """
                INSERT INTO deck_slots (
                    deck_id, position, item_name, tier,
                    custom_0, custom_1, custom_2, custom_3, custom_4, quest
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (did, pos, it["key"], TIER_TO_INT[it["tier"]],
                 *(ao.get(k) for k in SLOT_ATTR_KEYS)),
            )
        print(f"  deck id={did}  {deck_name}")
    print(f"collection id={cid}「{collection_name}」：{len(converted)} 套（L{level}）")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--db", default=str(ROOT / "app" / "backend" / "data" / "bazaararena.db"))
    ap.add_argument("--level", type=int, default=None, help="只导入该等级（默认全部）")
    args = ap.parse_args()
    db_path = Path(args.db)
    if not db_path.is_file():
        raise SystemExit(f"数据库不存在：{db_path}（先运行 python tools/gen_items_sqlite.py）")

    item_db = load_item_db(ROOT / "data" / "items", "mak")
    conn = sqlite3.connect(str(db_path))
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    try:
        sizes = {r["name"]: int(r["size"]) for r in conn.execute("SELECT name, size FROM items")}
        for level in sorted(LEVEL_DECKS):
            if args.level is not None and level != args.level:
                continue
            import_level(conn, item_db, sizes, level)
        conn.commit()
        print(f"完成 -> {db_path}")
    finally:
        conn.close()


if __name__ == "__main__":
    main()
