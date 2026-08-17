# -*- coding: utf-8 -*-
"""把收敛认证后的 Mak 精英卡组导入 Web 前端数据库（bazaararena.db），按等级分集合。

牌表来源：收敛闭环产物（docs/meta/mak-l2.md / mak-l5.md / mak-l8.md；
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
]

# 等级 → (集合名, [(卡组名, 有序物品签名), ...])
# 顺序 = 前端从左到右摆位，与真值层评测一致。
LEVEL_DECKS: dict[int, tuple[str, list[tuple[str, list[str]]]]] = {
    17: ("Mak L17 精英（高原代表 · v3）", [
        ("精英 智者杖天平·沙漏（σ）",
         ["智者之杖", "采掘工具", "缩小药水", "天平", "沙漏", "冰霜之怖", "力量药水"]),
        ("精英 智者杖天平·采掘",
         ["缩小药水", "力量药水", "冰霜之怖", "天平", "瓶装龙卷风", "智者之杖", "采掘工具"]),
        ("精英 智者杖天平·光学强化",
         ["缩小药水", "冰霜之怖", "能量药水", "天平", "昏睡药水", "智者之杖", "光学强化"]),
        ("精英 智者杖干扰·魂石（σ）",
         ["昏睡药水", "活力药水", "恶臭蘑菇", "智者之杖", "嗅盐", "瓶装龙卷风", "力量药水", "缩小药水"]),
        ("精英 采样仪图书馆石碑（σ）",
         ["大气采样仪", "图书馆", "飞行药水", "永恒火炬", "寒霜图腾"]),
        ("精英 药水干扰·恶臭",
         ["飞行药水", "挎包", "冰霜之怖", "恶臭蘑菇", "活力药水", "能量药水", "无限药水"]),
        ("精英 采样仪图书馆冻结",
         ["水银", "寒霜图腾", "灼烧冻结魂石", "蓝宝石", "大气采样仪", "图书馆"]),
        ("流派 冰爪采样仪",
         ["飞行药水", "塔兹迪亚匕首", "猫头鹰奥利", "琥珀", "马克显微镜", "智者之杖", "采掘工具", "大气采样仪"]),
    ]),
    14: ("Mak L14 精英（收敛认证 · v3）", [
        ("精英 智者杖天平·冰霜之怖（σ1）",
         ["能量药水", "智者之杖", "采掘工具", "天平", "力量药水", "缩小药水", "冰霜之怖"]),
        ("精英 智者杖天平·霜冻",
         ["智者之杖", "采掘工具", "霜冻药水", "天平", "缩小药水", "冰霜之怖", "力量药水"]),
        ("精英 智者杖干扰·活力",
         ["智者之杖", "嗅盐", "剧毒减速魂石", "活力药水", "恶臭蘑菇", "力量药水", "瓶装龙卷风", "缩小药水"]),
        ("精英 智者杖干扰·勿忘死亡",
         ["缩小药水", "智者之杖", "恶臭蘑菇", "勿忘死亡", "力量药水", "能量药水", "无敌药水"]),
        ("精英 智者杖天平·无敌",
         ["缩小药水", "智者之杖", "采掘工具", "天平", "无敌药水", "力量药水", "恶臭蘑菇"]),
        ("精英 图书馆采样仪冻结（σ2 反制）",
         ["冰霜之怖", "地窖", "飞行药水", "图书馆", "大气采样仪"]),
        ("流派 巨龙吐息药水",
         ["飞行药水", "沸腾烧瓶", "巨龙吐息", "灼烧冻结魂石", "蓝宝石", "图书馆"]),
    ]),
    11: ("Mak L11 精英（收敛认证 · v3）", [
        ("精英 天平充能·智者之杖（σ1）",
         ["智者之杖", "采掘工具", "缩小药水", "天平", "能量药水", "恶臭蘑菇", "力量药水"]),
        ("精英 图书馆冻结毒·黑冰（σ3）",
         ["采掘工具", "剧毒冻结魂石", "蓝宝石", "冰霜之怖", "图书馆", "黑冰"]),
        ("精英 恶臭干扰·智者之杖",
         ["力量药水", "缩小药水", "蛇怪之牙", "恶臭蘑菇", "智者之杖", "沙漏", "能量药水", "无敌药水"]),
        ("精英 图书馆冻结·寒霜图腾",
         ["黑冰", "剧毒冻结魂石", "寒霜图腾", "冰霜之怖", "图书馆"]),
        ("精英 图书馆冻结·沙漏",
         ["图书馆", "剧毒冻结魂石", "沙漏", "冰霜之怖", "蓝宝石", "黑冰"]),
        ("精英 图书馆采样仪冻结",
         ["飞行药水", "寒霜图腾", "剧毒冻结魂石", "采掘工具", "大气采样仪", "图书馆"]),
        ("精英 石碑采样仪·耳环",
         ["飞行药水", "剧毒冻结魂石", "空白石碑", "耳环", "图书馆", "大气采样仪"]),
        ("精英 火炬采样仪",
         ["永恒火炬", "图书馆", "采掘工具", "灼烧冻结魂石", "蓝宝石", "大气采样仪"]),
        ("精英 飞行火炬",
         ["灼烧冻结魂石", "图书馆", "飞行药水", "大气采样仪", "永恒火炬", "采掘工具"]),
        ("精英 冻结控制·L8王形态（σ2）",
         ["剧毒冻结魂石", "冰霜之怖", "寒霜图腾", "冰爪", "力量药水", "彩虹药水", "霜冻药水"]),
    ]),
    8: ("Mak L8 精英（收敛认证 · v3）", [
        ("精英 冻结控制·冰霜之怖（σ1）",
         ["剧毒冻结魂石", "冰霜之怖", "寒霜图腾", "冰爪", "光学强化", "彩虹药水", "无敌药水"]),
        ("精英 减速重炮·股骨（σ2）",
         ["无敌药水", "琥珀", "马格努斯的股骨", "符文药水", "采掘工具", "时间之砂", "嗅盐"]),
        ("精英 自毒武器·注能护腕",
         ["力量药水", "倒刺利爪", "实验体阿尔法", "蕨叶蜘蛛", "蜘蛛连枷", "注能护腕"]),
        ("精英 自毒注射·蕨叶",
         ["瘟疫长柄刀", "光学强化", "蕨叶蜘蛛", "快速注射系统", "肾上腺素调节服"]),
        ("精英 冻结毒·黑冰",
         ["能量药水", "瘟疫长柄刀", "寒霜图腾", "剧毒冻结魂石", "蓝宝石", "黑冰"]),
        ("精英 毒·瘟疫长柄刀",
         ["无敌药水", "力量药水", "蕨叶蜘蛛", "毒液", "瘟疫长柄刀", "能量药水", "瓶装龙卷风"]),
        ("精英 图书馆火炬壳",
         ["力量药水", "图书馆", "采掘工具", "永恒火炬", "嗅盐", "灼烧减速魂石", "无敌药水"]),
        ("精英 药水装填·炼金炉（σ3）",
         ["无敌药水", "符文药水", "瓶装龙卷风", "炼金炉", "飞行药水", "沸腾烧瓶", "碎瓶"]),
        ("流派 药水装填·昏睡反制",
         ["力量药水", "符文药水", "昏睡药水", "炼金炉", "飞行药水", "沸腾烧瓶", "碎瓶"]),
        ("流派 自毒武器·百足鼬变体",
         ["水银", "实验体阿尔法", "百足鼬", "蜘蛛连枷", "注能护腕"]),
        ("流派 天平充能",
         ["采掘工具", "永恒火炬", "能量药水", "天平", "无敌药水", "力量药水", "智者之杖"]),
    ]),
    5: ("Mak L5 精英（收敛认证 · v3）", [
        ("精英 暴击武器·飞毯双射弓",
         ["魔法飞毯", "拆信刀", "无敌药水", "力量药水", "碎瓶", "彩虹药水", "瓶装龙卷风", "符文双射弓"]),
        ("精英 药水装填·碎瓶（σ1）",
         ["碎瓶", "红宝石", "瓶装龙卷风", "符文药水", "炼金炉", "飞行药水", "无限药水", "无敌药水"]),
        ("精英 火炬壳·彩虹",
         ["红宝石", "永恒火炬", "嗅盐", "灼烧减速魂石", "云精灵", "力量药水", "香炉", "彩虹药水", "无敌药水"]),
        ("精英 石碑火炬",
         ["力量药水", "红宝石", "空白石碑", "永恒火炬", "嗅盐", "灼烧减速魂石", "能量药水", "无敌药水"]),
        ("精英 自毒注射",
         ["肾上腺素调节服", "药房", "快速注射系统", "腐朽圣像", "光学强化"]),
        ("精英 火炬壳",
         ["无敌药水", "永恒火炬", "嗅盐", "灼烧减速魂石", "能量药水", "彩虹药水", "红宝石", "力量药水", "香炉"]),
        ("精英 药水装填·炼金炉",
         ["无敌药水", "飞行药水", "炼金炉", "符文药水", "红宝石", "彩虹药水", "瓶装龙卷风", "碎瓶"]),
        ("流派 飞行巨龙（克药水装填）",
         ["贪婪渡鸦", "空惧巨龙", "萤火虫", "永恒火炬", "无敌药水", "灼烧减速魂石"]),
    ]),
    2: ("Mak L2 精英（收敛认证 · v3）", [
        ("精英 灼烧王（σ=1.000）",
         ["瓶装闪电", "红宝石", "香炉", "蜡烛", "火焰药水"]),
        ("精英 剑杖鳄鱼混合",
         ["剑杖", "彩虹药水", "鳄鱼眼泪", "煅烧釜"]),
        ("流派 毒吸血·水蛭",
         ["翡翠", "水蛭", "毒蜥", "毒液", "毒液注射"]),
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
