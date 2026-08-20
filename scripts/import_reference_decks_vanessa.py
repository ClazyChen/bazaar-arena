# -*- coding: utf-8 -*-
"""把收敛认证后的 Vanessa 精英卡组导入 Web 前端数据库（bazaararena.db），按等级分集合。

牌表来源：收敛闭环产物（docs/meta/vanessa-l2.md …；
out/elite_report/vanessa_l*/final_decks.json）。档位与覆写复用 scripts/meta_search/gdf_conditions.py，
保证前端对局与真值层逐帧一致：战斗档位按等级、overridable 缩放、
烙刀变体展开为 烙刀+quest 位图（减速烙刀=Q1 / 加速烙刀=Q2）。

幂等：同名 collection 存在时整组删除重建（ON DELETE CASCADE）；同时清理历史旧集合。

用法（仓库根目录）：
    python scripts/import_reference_decks_vanessa.py              # 导入全部等级
    python scripts/import_reference_decks_vanessa.py --level 2    # 只导入某一等级
    python scripts/import_reference_decks_vanessa.py --db <path>  # 覆盖数据库路径
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
LEGACY_COLLECTIONS: list[str] = []

# 等级 → (集合名, [(卡组名, 有序物品签名), ...])
# 顺序 = 前端从左到右摆位，与真值层评测一致。
LEVEL_DECKS: dict[int, tuple[str, list[tuple[str, list[str]]]]] = {
    17: ("Vanessa L17 精英（高原代表 · v4）", [
        ("精英 船骸多重·绵鳚（avg王）",
         ["船骸", "拍立蚌", "绵鳚", "划艇", "船锚", "鱼饵"]),
        ("精英 潜艇滑翔机盾",
         ["潜水头盔", "潜行滑翔机", "潜艇", "湿件战服"]),
        ("精英 船骸多重·鱼叉",
         ["船骸", "拍立蚌", "船锚", "划艇", "鱼叉"]),
        ("精英 大坝清场·滑翔机（σ1）",
         ["大坝", "温馨海湾", "潜行滑翔机", "拍立蚌"]),
        ("精英 船骸多重·HUD",
         ["集成式HUD", "船锚", "划艇", "鱼饵", "拍立蚌", "船骸"]),
        ("精英 水系盾·吹箭枪（σ3）",
         ["灼烧梭鱼", "吹箭枪", "潜水头盔", "套娃", "潜水配重", "湿件战服", "枪套", "拍立蚌"]),
        ("精英 大坝清场·灼烧",
         ["灼烧梭鱼", "绵鳚", "温馨海湾", "大坝", "鱼饵", "拍立蚌"]),
        ("流派 滑翔机加速·温馨海湾",
         ["鱼饵", "码头缆索", "绵鳚", "温馨海湾", "潜行滑翔机"]),
        ("流派 减速烙刀·灯塔",
         ["烈酒杯", "拍立蚌", "灯塔", "绊索", "减速烙刀", "十手"]),
        ("流派 船骸多重·鸦巢",
         ["船骸", "鸦巢", "拍立蚌", "船锚", "消音器"]),
        ("流派 冰山冻结·温馨海湾",
         ["温馨海湾", "潜行滑翔机", "小冰镐", "冰山"]),
    ]),
    14: ("Vanessa L14 精英（收敛认证 · v4）", [
        ("精英 船骸水系多重·船锚（avg王）",
         ["拍立蚌", "船锚", "划艇", "六分仪", "船骸"]),
        ("精英 刺刀手枪水系盾·枪套（σ1）",
         ["拍立蚌", "枪套", "湿件战服", "潜水配重", "刺刀手枪", "潜水头盔", "套娃"]),
        ("精英 刺刀手枪减速暴击（σ2）",
         ["飞镖发射器", "拍立蚌", "潜水配重", "皮皮虾", "烈酒杯", "枪套", "手里剑", "锁镰", "刺刀手枪"]),
        ("精英 HUD鮟鱇减速灼烧",
         ["蔻森娜纹章", "集成式HUD", "幽渊鮟鱇", "珍珠", "拍立蚌", "船骸", "烈酒杯"]),
        ("精英 元初体冰山充能（σ3 针对）",
         ["鸦巢", "沉眠元初体", "燃烧子弹", "冰山"]),
        ("精英 滑翔机飞行·船锚",
         ["鸦巢", "星图", "船锚", "潜行滑翔机"]),
        ("精英 纹章减速暴击·十手",
         ["十手", "枪套", "皮皮虾", "蔻森娜纹章", "烈酒杯", "飞镖发射器", "手里剑", "锁镰", "抓钩", "拍立蚌"]),
        ("精英 摩托飞行·准镜",
         ["蔻森娜纹章", "套娃", "定制准镜", "喷射摩托", "潜行滑翔机", "潜水配重"]),
        ("流派 望远镜减速暴击",
         ["拍立蚌", "灯塔", "侦查望远镜", "皮皮虾", "飞镖发射器", "烈酒杯", "枪套"]),
        ("流派 船骸多重·船首像",
         ["消音器", "划艇", "船锚", "船骸", "船首像"]),
        ("流派 灯塔减速族·十手",
         ["烈酒杯", "灯塔", "皮皮虾", "飞镖发射器", "绵鳚", "十手", "拍立蚌", "枪套"]),
        ("流派 宝驹滚石冷却",
         ["海影宝驹", "标枪", "划艇", "滚石", "套娃"]),
        ("流派 灯塔减速族·流星索",
         ["皮皮虾", "烈酒杯", "飞镖发射器", "流星索", "拍立蚌", "十手", "灯塔", "枪套"]),
    ]),
    11: ("Vanessa L11 精英（收敛认证 · v4）", [
        ("精英 刺刀手枪水系盾·枪套（σ1）",
         ["刺刀手枪", "湿件战服", "潜水配重", "拍立蚌", "枪套", "套娃", "潜水头盔"]),
        ("精英 刺刀手枪水系盾·灼烧梭鱼",
         ["灼烧梭鱼", "潜水配重", "湿件战服", "拍立蚌", "套娃", "潜水头盔", "刺刀手枪"]),
        ("精英 单武器潜艇",
         ["湿件战服", "套娃", "鸦巢", "潜艇", "消音器"]),
        ("精英 摩托滑翔机飞行",
         ["潜行滑翔机", "喷射摩托", "潜水配重", "鸦巢"]),
        ("精英 手里剑减速暴击",
         ["拍立蚌", "烈酒杯", "蔻森娜纹章", "手里剑", "锁镰", "刺刀手枪", "套娃", "飞镖发射器", "枪套"]),
        ("精英 望远镜减速暴击",
         ["飞镖发射器", "拍立蚌", "皮皮虾", "烈酒杯", "枪套", "手里剑", "锁镰", "十手", "侦查望远镜"]),
        ("精英 宠物石飞行暴击（σ2 针对）",
         ["宠物石", "蔻森娜纹章", "拍立蚌", "喷射摩托", "潜行滑翔机", "套娃"]),
        ("流派 藏刃匕首飞行",
         ["蔻森娜纹章", "藏刃匕首", "宠物石", "潜行滑翔机", "喷射摩托", "套娃"]),
        ("流派 船舵减速毒",
         ["拍立蚌", "绊索", "鸦巢", "飞镖发射器", "船舵", "皮皮虾"]),
        ("流派 灯塔皮皮虾减速",
         ["灯塔", "皮皮虾", "绵鳚", "套娃", "十手", "拍立蚌", "绊索"]),
    ]),
    8: ("Vanessa L8 精英（收敛认证 · v4）", [
        ("精英 刺刀手枪水系盾（σ=1.000）",
         ["灼烧梭鱼", "潜水配重", "湿件战服", "拍立蚌", "刺刀手枪", "潜水头盔", "套娃"]),
        ("精英 刺刀手枪水系盾·珍珠",
         ["拍立蚌", "珍珠", "刺刀手枪", "潜水头盔", "套娃", "潜水配重", "湿件战服"]),
        ("精英 悬浮板水系盾·珍珠",
         ["珍珠", "潜水配重", "带刃悬浮板", "套娃", "潜水头盔", "湿件战服", "拍立蚌"]),
        ("精英 悬浮板水系盾·淬锋钢",
         ["湿件战服", "拍立蚌", "潜水配重", "带刃悬浮板", "套娃", "潜水头盔", "淬锋钢"]),
        ("流派 烙刀灼烧·篝火",
         ["大钢弩", "绵鳚", "拍立蚌", "燃烧子弹", "灼烧梭鱼", "篝火", "加速烙刀"]),
        ("流派 大钢弩水车加速",
         ["皮皮虾", "烈酒杯", "抓钩", "大钢弩", "拍立蚌", "水车", "十手"]),
        ("流派 灯塔皮皮虾减速",
         ["拍立蚌", "十手", "绵鳚", "皮皮虾", "灯塔", "套娃", "绊索"]),
        ("流派 灯塔减速·火药桶",
         ["拍立蚌", "火药桶", "灯塔", "烈酒杯", "皮皮虾", "绵鳚", "十手"]),
        ("流派 灯塔减速族·流星索",
         ["抓钩", "流星索", "烈酒杯", "灯塔", "拍立蚌", "十手", "绵鳚", "皮皮虾"]),
        ("流派 滑翔机飞行·鱼叉",
         ["潜行滑翔机", "木桶", "船舵", "鱼叉"]),
        ("流派 鸦巢弩炮·消音器",
         ["套娃", "弩炮", "消音器", "鸦巢", "潜水配重", "灼烧梭鱼"]),
    ]),
    5: ("Vanessa L5 精英（收敛认证 · v4）", [
        ("精英 带刃悬浮板水系盾（σ=1.000）",
         ["套娃", "带刃悬浮板", "潜水配重", "龟壳", "珍珠", "拍立蚌", "湿件战服"]),
        ("精英 烙刀灼烧·燃烧子弹",
         ["加速烙刀", "绵鳚", "灼烧梭鱼", "燃烧子弹", "套娃", "篝火", "大钢弩"]),
        ("精英 滑翔机飞行加速",
         ["潜行滑翔机", "木桶", "船舵", "海影宝驹"]),
        ("精英 减速烙刀皮皮虾",
         ["救生圈", "流星索", "皮皮虾", "拍立蚌", "减速烙刀", "十手", "火药角", "烈酒杯"]),
        ("精英 潜水配重加速灼烧",
         ["救生圈", "潜水配重", "灼烧梭鱼", "加速烙刀", "绵鳚", "大钢弩", "烈酒杯"]),
        ("精英 鱼雷弹药灼烧",
         ["鱼雷", "燃烧响炮", "绵鳚", "打火机", "燃烧子弹", "套娃", "灼烧梭鱼", "流星索", "拍立蚌"]),
        ("流派 旗舰多重武器",
         ["拍立蚌", "弹簧刀", "旗舰", "绵鳚", "鱼雷", "潜水配重", "珊瑚"]),
        ("流派 锁镰减速武器",
         ["十手", "锁镰", "减速烙刀", "绵鳚", "皮皮虾", "拍立蚌", "流星索", "烈酒杯", "抓钩"]),
        ("流派 武士刀悬浮板武器",
         ["手斧", "弹簧刀", "武士刀", "带刃悬浮板", "套娃", "潜行滑翔机"]),
        ("流派 抛石机灼烧",
         ["灼烧梭鱼", "加速烙刀", "船舵", "篝火", "抛石机"]),
    ]),
    2: ("Vanessa L2 精英（收敛认证 · v4）", [
        ("精英 毒须鲶加速毒（σ1）",
         ["毒须鲶", "木桶", "沙滩充气球", "流星索"]),
        ("精英 马龙鱼飞行·水草",
         ["淬锋钢", "木桶", "马龙鱼", "水草"]),
        ("精英 武士刀重武器",
         ["弹簧刀", "武士刀", "木桶", "淬锋钢"]),
        ("精英 火炮灼烧",
         ["打火机", "绵鳚", "燃烧响炮", "火炮", "灼烧梭鱼"]),
        ("精英 马龙鱼飞行·充气球",
         ["弹簧刀", "马龙鱼", "沙滩充气球", "淬锋钢"]),
        ("精英 迷幻蝠鲼减速灼烧（σ2 针对）",
         ["抓钩", "迷幻蝠鲼", "皮皮虾", "绵鳚", "打火机", "燃烧响炮"]),
        ("流派 淬锋钢五武器",
         ["靴里剑", "手斧", "流星索", "武士刀", "淬锋钢"]),
        ("流派 灼毒混伤·葡萄弹",
         ["灼烧梭鱼", "毒须鲶", "绵鳚", "燃烧响炮", "打火机", "葡萄弹"]),
        ("流派 纯灼烧·火药角",
         ["火药角", "灼烧梭鱼", "葡萄弹", "绵鳚", "燃烧响炮", "打火机"]),
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

    item_db = load_item_db(ROOT / "data" / "items", "vanessa")
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
