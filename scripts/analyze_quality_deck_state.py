# 分析优质卡组探测器状态 JSON，生成统计报告摘要（不负责解读与问答）。
# 用法：python scripts/analyze_quality_deck_state.py [状态文件路径] [--out 输出.md]
# 默认状态文件：quality_deck_state.json（仓库根或 Data/ 下）
# 解读与问答见 docs/quality_deck_state_qa.md。

import json
import sys
from pathlib import Path
from collections import defaultdict


def find_state_path(path_arg: str | None) -> Path:
    if path_arg:
        p = Path(path_arg)
        if p.is_absolute():
            return p
        root = Path(__file__).resolve().parent.parent
        for base in (root, Path.cwd()):
            candidate = base / path_arg
            if candidate.exists():
                return candidate
        return root / path_arg
    root = Path(__file__).resolve().parent.parent
    for name in ("quality_deck_state.json", "Data/quality_deck_state.json"):
        candidate = root / name
        if candidate.exists():
            return candidate
    return root / "quality_deck_state.json"


def item_name_from_key(key: str) -> str | None:
    """从 anchored key 'itemName|shapeIndex' 解析出物品名。"""
    if not key or "|" not in key:
        return None
    return key[: key.rindex("|")]


def shape_index_from_key(key: str) -> int | None:
    """从 anchored key 解析出形状索引。"""
    if not key or "|" not in key:
        return None
    try:
        return int(key[key.rindex("|") + 1 :])
    except ValueError:
        return None


def load_state(path: Path) -> dict | None:
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def build_combo_lookup(dto: dict) -> dict:
    """ComboSig -> { RepresentativeItems, Elo, IsLocalOptimum, IsConfirmed, GameCount }"""
    lookup = {}
    for src in (dto.get("VirtualCombos") or dto.get("Combos") or []), dto.get("HistoryCombos") or []:
        for c in src:
            sig = c.get("ComboSig")
            if not sig:
                continue
            lookup[sig] = {
                "items": c.get("RepresentativeItems") or [],
                "shape": c.get("RepresentativeShape") or [],
                "elo": c.get("Elo", 0),
                "is_local_optimum": c.get("IsLocalOptimum", False),
                "is_confirmed": c.get("IsConfirmed", False),
                "game_count": c.get("GameCount", 0),
            }
    return lookup


def analyze(dto: dict) -> dict:
    combo_lookup = build_combo_lookup(dto)
    virtual = dto.get("VirtualCombos") or dto.get("Combos") or []
    history = dto.get("HistoryCombos") or []

    # 1. 局部最优而放弃的卡组（IsLocalOptimum 的卡组）
    local_optima = []
    for c in virtual + history:
        if c.get("IsLocalOptimum"):
            local_optima.append({
                "combo_sig": c.get("ComboSig"),
                "items": c.get("RepresentativeItems") or [],
                "elo": c.get("Elo", 0),
                "game_count": c.get("GameCount", 0),
                "in_pool": "Virtual" if any(x.get("ComboSig") == c.get("ComboSig") for x in virtual) else "History",
            })

    # 2. 每个物品锚定玩家的卡组最终状态（按物品分组，同物品不同 shape 分别列出）
    anchored = dto.get("AnchoredPlayers") or []
    by_item: dict[str, list[dict]] = defaultdict(list)
    for a in anchored:
        key = a.get("Key") or ""
        comboSig = a.get("ComboSig") or ""
        item = item_name_from_key(key)
        shape_idx = shape_index_from_key(key)
        entry = combo_lookup.get(comboSig, {})
        by_item[item or key].append({
            "anchor_key": key,
            "shape_index": shape_idx,
            "combo_sig": comboSig,
            "items": entry.get("items", []),
            "elo": entry.get("elo", 0),
            "game_count": entry.get("game_count", 0),
        })

    # 局部最优：按物品组合去重与相似度（供报告与解读用）
    def canonical_items(items: list) -> tuple:
        return tuple(sorted(items)) if items else ()

    local_optima_by_set: dict[tuple, list[dict]] = defaultdict(list)
    for o in local_optima:
        key = canonical_items(o["items"])
        if key:
            local_optima_by_set[key].append(o)

    unique_local_optima_sets = len(local_optima_by_set)
    local_optima_dup_counts = sorted(
        [(k, len(v)) for k, v in local_optima_by_set.items()],
        key=lambda x: x[1],
        reverse=True,
    )
    # 仅差 1 个物品的“组合对”数：在唯一组合层面，两组合大小相同且对称差为 2（各多 1 个不同物品）
    unique_sets_list = list(local_optima_by_set.keys())
    pairs_diff_one = 0
    for i in range(len(unique_sets_list)):
        for j in range(i + 1, len(unique_sets_list)):
            a, b = set(unique_sets_list[i]), set(unique_sets_list[j])
            if len(a) != len(b):
                continue
            if len(a ^ b) == 2:
                pairs_diff_one += 1

    # 每个物品下按 ELO 排序，取最高为“该物品当前最优代表”
    item_final_decks = {}
    for item, entries in by_item.items():
        sorted_entries = sorted(entries, key=lambda x: x["elo"], reverse=True)
        item_final_decks[item] = {
            "by_shape": entries,
            "best_elo": sorted_entries[0]["elo"] if sorted_entries else 0,
            "best_deck": sorted_entries[0] if sorted_entries else None,
        }

    anchored_total = sum(len(v) for v in by_item.values())
    anchored_zero_by_item: dict[str, int] = defaultdict(int)
    anchored_zero_count = 0
    for item, entries in by_item.items():
        for e in entries:
            if (e.get("game_count") or 0) <= 0:
                anchored_zero_count += 1
                anchored_zero_by_item[item] += 1
    anchored_zero_items_top = sorted(anchored_zero_by_item.items(), key=lambda kv: kv[1], reverse=True)[:30]

    # 3. 强度玩家与 Top ELO 的关系
    strength_sigs = dto.get("StrengthPlayerComboSigs") or []
    all_combos = virtual + history
    by_elo = sorted(all_combos, key=lambda c: c.get("Elo") or 0, reverse=True)
    top_10_elo = by_elo[:10]
    top_sigs = {c.get("ComboSig") for c in top_10_elo if c.get("ComboSig")}
    strength_in_top = sum(1 for s in strength_sigs if s in top_sigs)
    strength_elos = []
    for sig in strength_sigs:
        info = combo_lookup.get(sig)
        if info:
            strength_elos.append((sig, info["elo"], info["items"]))
    strength_elos.sort(key=lambda x: x[1], reverse=True)

    # 4. 算法层面可用的统计
    summary = dto.get("Summary") or {}
    pool_size = len(virtual)
    history_size = len(history)
    local_optima_count = summary.get("LocalOptimaCount", len(local_optima))
    confirmed_count = summary.get("ConfirmedCount", 0)
    max_elo = summary.get("MaxElo") or (max((c.get("Elo") or 0) for c in virtual) if virtual else 0)
    min_elo = summary.get("MinElo") or (min((c.get("Elo") or 0) for c in virtual) if virtual else 0)

    return {
        "dto": dto,
        "combo_lookup": combo_lookup,
        "local_optima": local_optima,
        "unique_local_optima_sets": unique_local_optima_sets,
        "local_optima_dup_counts": local_optima_dup_counts,
        "pairs_diff_one_item": pairs_diff_one,
        "item_final_decks": dict(item_final_decks),
        "by_item_raw": dict(by_item),
        "anchored_total": anchored_total,
        "anchored_zero_count": anchored_zero_count,
        "anchored_zero_items_top": anchored_zero_items_top,
        "strength_sigs": strength_sigs,
        "strength_elos": strength_elos,
        "top_10_elo": top_10_elo,
        "top_sigs": top_sigs,
        "strength_in_top_count": strength_in_top,
        "pool_size": pool_size,
        "history_size": history_size,
        "local_optima_count": local_optima_count,
        "confirmed_count": confirmed_count,
        "max_elo": max_elo,
        "min_elo": min_elo,
        "current_season": dto.get("CurrentSeason", 0),
        "total_games": dto.get("TotalGames", 0),
        "anchored_count": len(anchored),
        "strength_count": len(strength_sigs),
    }


def write_report(result: dict, out_path: Path | None) -> str:
    md: list[str] = []
    r = result

    md.append("# 优质卡组探测器状态分析报告")
    md.append("")
    md.append("## 一、运行概况")
    md.append("")
    md.append(f"- **当前赛季**: {r['current_season']}")
    md.append(f"- **总对局数**: {r['total_games']}")
    md.append(f"- **虚拟玩家池大小**: {r['pool_size']}")
    md.append(f"- **历史池大小**: {r['history_size']}")
    md.append(f"- **已确认卡组数**: {r['confirmed_count']}")
    md.append(f"- **局部最优卡组数**: {r['local_optima_count']}")
    md.append(f"- **ELO 范围**: [{r['min_elo']:.1f}, {r['max_elo']:.1f}]")
    md.append(f"- **锚定玩家数**: {r['anchored_count']}")
    md.append(f"- **强度玩家数**: {r['strength_count']}")
    md.append("")

    md.append("---")
    md.append("")
    md.append("## 二、问题 1：找到了哪些局部最优而放弃的卡组？")
    md.append("")
    md.append("以下卡组在爬山时被标记为**局部最优**（无法通过单步同尺寸替换改进），锚定玩家会将该卡组记录为该形状下的「当前最优」并重启为随机卡组继续探索。")
    md.append("")
    local_optima = r["local_optima"]
    if not local_optima:
        md.append("（本次状态中未发现标记为局部最优的卡组，或局部最优仅存在于虚拟池统计数量中。）")
    else:
        # 按 ELO 降序
        local_optima_sorted = sorted(local_optima, key=lambda x: x["elo"], reverse=True)
        md.append("| ELO | 对局数 | 池 | 代表物品列表 |")
        md.append("|-----|--------|-----|--------------|")
        for o in local_optima_sorted[:100]:  # 最多 100 条
            items_str = ", ".join(o["items"][:12])
            if len(o["items"]) > 12:
                items_str += ", ..."
            md.append(f"| {o['elo']:.1f} | {o['game_count']} | {o['in_pool']} | {items_str} |")
        if len(local_optima_sorted) > 100:
            md.append(f"\n（仅列出前 100 条，共 {len(local_optima_sorted)} 条局部最优卡组。）")

        # 局部最优去重与相似度统计（供解读文档使用）
        unique_sets = r.get("unique_local_optima_sets", 0)
        dup_counts = r.get("local_optima_dup_counts") or []
        pairs_diff_one = r.get("pairs_diff_one_item", 0)
        if unique_sets > 0 and dup_counts:
            md.append("")
            md.append("### 局部最优去重与相似度（统计摘要）")
            md.append("")
            md.append(f"- 局部最优**条目总数**（按 ComboSig）: **{len(local_optima_sorted)}**")
            md.append(f"- **唯一物品组合数**（按物品集合去重）: **{unique_sets}**")
            md.append(f"- 即平均每组物品组合约有 **{len(local_optima_sorted) / unique_sets:.1f}** 条局部最优条目（不同排列/不同 ComboSig）。")
            md.append(f"- 在唯一组合层面，**仅差 1 个物品**的组合对数量: **{pairs_diff_one}**（两组合大小相同、对称差为 2）。")
            md.append("")
            md.append("重复次数最多的物品组合（Top 10，按条目数）：")
            md.append("")
            md.append("| 重复条数 | 代表物品列表 |")
            md.append("|----------|--------------|")
            for key, cnt in dup_counts[:10]:
                items_str = ", ".join(list(key)[:12])
                if len(key) > 12:
                    items_str += ", ..."
                md.append(f"| {cnt} | {items_str} |")
    md.append("")

    md.append("---")
    md.append("")
    md.append("## 三、问题 2：每个物品相关的锚定玩家的卡组最终优化成什么样子？")
    md.append("")
    md.append("锚定玩家 key 格式为 `物品名|形状索引`；同一物品可有多个形状（小中大数量不同）的锚定玩家。下表按**物品**分组，列出该物品下各形状的当前卡组（代表排列）与 ELO。")
    md.append("")

    anchored_total = r.get("anchored_total", 0)
    anchored_zero_count = r.get("anchored_zero_count", 0)
    if anchored_total > 0 and anchored_zero_count > 0:
        md.append("### 补充：为何大量赛季后仍会出现「对局数为 0」的形状？")
        md.append("")
        md.append(f"- 本次状态中锚定条目总数: **{anchored_total}**；其中对局数为 0 的条目: **{anchored_zero_count}**。")
        md.append("- 关键原因是运行机制：**每赛季每个物品只选择 1 名锚定代表**进入当季活跃玩家集合参与匹配与爬山（实现位于 `AnchoredRepresentativeScheduler.SelectRepresentatives`）。未被选中的形状，本赛季不会产生任何对局增长。")
        md.append("- 在“非探索”分支下代表选择使用 **softmax(ELO)** 加权抽样；当某个形状的 ELO 明显高于其它形状时，softmax 会非常尖锐，导致低分形状几乎选不到，从而长期停留在 **ELO≈1500、对局数=0** 的冷启动状态。")
        md.append("- 另一个常见原因是：某些锚定形状在较晚赛季才被创建/重启出来，而在保存状态后程序停止，导致它们还未来得及被抽到代表。")

        top = r.get("anchored_zero_items_top") or []
        if top:
            md.append("")
            md.append("对局数为 0 的形状最多的物品（Top 10）：")
            md.append("")
            md.append("| 物品 | 0 对局形状数 |")
            md.append("|------|--------------|")
            for item, cnt in top[:10]:
                md.append(f"| {item} | {cnt} |")

        md.append("")
        md.append("**建议（可操作）**：")
        md.append("")
        md.append("- **强制覆盖/保底**：代表选择时优先保证“最低对局数/最低被选次数”的形状获得最低配额（例如每 K 个赛季轮换一次，或每赛季强制抽 1 次 0 对局形状）。")
        md.append("- **提高探索或温度**：提高 `RepresentativeExploreProb` 或增大 `RepresentativeTemperature`，避免 softmax 过于尖锐。")
        md.append("- **代表选择的 minGames 逻辑应与目标一致**：当前 `MinGamesForRepresentative` 是“低于阈值降权”，如果目标是覆盖冷启动形状，应改为“低于阈值加权上调/强制优先”。")
        md.append("")

    item_final = r["item_final_decks"]
    if not item_final:
        md.append("（无锚定玩家数据。）")
    else:
        # 按物品名排序；每个物品下按 best_elo 降序
        for item in sorted(item_final.keys(), key=lambda i: (item_final[i]["best_elo"], i), reverse=True):
            info = item_final[item]
            md.append(f"### 物品: {item}")
            md.append("")
            md.append(f"- 该物品下最高 ELO: **{info['best_elo']:.1f}**")
            md.append("")
            md.append("| 形状索引 | ELO | 对局数 | 代表物品列表 |")
            md.append("|----------|-----|--------|--------------|")
            for e in sorted(info["by_shape"], key=lambda x: x["elo"], reverse=True):
                items_str = ", ".join(e["items"][:14])
                if len(e["items"]) > 14:
                    items_str += ", ..."
                md.append(f"| {e['shape_index']} | {e['elo']:.1f} | {e['game_count']} | {items_str} |")
            md.append("")
    md.append("")

    md.append("---")
    md.append("")
    md.append("## 四、问题 3：是否有必要保留强度玩家这一概念？")
    md.append("")
    strength_elos = r["strength_elos"]
    top_10 = r["top_10_elo"]
    strength_in_top = r["strength_in_top_count"]
    strength_count = r["strength_count"]
    md.append("### 数据观察")
    md.append("")
    md.append(f"- 当前强度玩家数: **{strength_count}**")
    md.append(f"- Top 10（按 ELO）卡组中，由强度玩家持有的数量: **{strength_in_top}** / 10")
    if strength_count > 0:
        pct = 100.0 * strength_in_top / min(10, strength_count)
        md.append(f"- 即：强度玩家覆盖了当前顶尖卡组中的 **{strength_in_top}/10** 席。")
    md.append("")
    md.append("**强度玩家当前卡组（按 ELO 降序）：**")
    md.append("")
    md.append("| 排名 | ELO | 代表物品列表 |")
    md.append("|------|-----|--------------|")
    for i, (sig, elo, items) in enumerate(strength_elos[:15], 1):
        items_str = ", ".join(items[:12])
        if len(items) > 12:
            items_str += ", ..."
        md.append(f"| {i} | {elo:.1f} | {items_str} |")
    md.append("")
    md.append("**结论与建议**：")
    md.append("")
    if strength_in_top == 0:
        md.append("- 当前状态下强度玩家与 Top 10 **没有重叠**。这通常不代表“强度玩家概念无效”，而更可能是强度玩家数量过少、注入不足或被合并/放弃过快。**建议保留强度玩家**，但需要提高注入覆盖（频率/数量）并延长放弃判定窗口，再观察其是否能持续占据高分段。")
    elif strength_in_top >= 7 and strength_count >= 5:
        md.append("- 强度玩家较好地覆盖了 Top 10 高 ELO 卡组，说明「纯强度」探索有效，**建议保留强度玩家**，用于发现不依赖单一物品的全局强卡组，并与锚定玩家（物品向推荐）形成互补。")
    elif strength_in_top <= 2 and strength_count >= 3:
        md.append("- 强度玩家与 Top 10 重叠较少，可能原因：注入较晚、被合并/放弃较多、或锚定玩家已占据高分。可考虑：增加注入频率/数量、或延长强度玩家放弃前的赛季数，再观察；**仍建议保留**，因锚定无法覆盖「无特定锚定物品」的强卡组。")
    else:
        md.append("- 强度玩家与 Top 10 有一定重叠。**建议保留强度玩家**：其职责是探索「任意替换」下的强卡组，与锚定玩家的「某物品最强搭配」形成双轨；若移除，仅靠锚定难以保证全局强度榜的多样性。")
    md.append("")

    md.append("---")
    md.append("")
    md.append("## 五、问题 4：还有哪些算法层面的优化建议？")
    md.append("")
    md.append("基于本次状态文件的统计，可从以下方向考虑（不改变现有设计的前提下）：")
    md.append("")
    md.append("1. **局部最优与重启策略**：若局部最优数量很多且 ELO 分布分散，可考虑对「高 ELO 局部最优」做有限次数的多步扰动（如允许临时换 2 张再爬山），以试探是否跳出盆地；代价是单次赛季耗时增加。")
    md.append("")
    md.append("2. **锚定代表采样与预算**：当前每赛季对锚定代表做 HillClimb，若锚定玩家数很大，可继续沿用「每物品抽样代表」并配合分阶段（先少局评估筛掉明显劣解，再对候选加局）。见 quality-deck-finder-perf-report 的 P2。")
    md.append("")
    md.append("3. **强度玩家与历史池多样性**：若强度玩家合并后数量变少，可适当提高注入频率或单次注入数量，避免高分段只有锚定卡组；同时历史池的相似度踢人可保留，以控制池大小与匹配质量。")
    md.append("")
    md.append("4. **ELO 分段与匹配**：若 MaxElo 与 MinElo 差距很大，分段匹配能减少「强弱悬殊」的对局；可检查 SegmentBounds 与当前 ELO 分布是否匹配，必要时做一次分段边界重算（如按分位数）。")
    md.append("")
    md.append("5. **Priors 与冷启动**：若物品/形状很多，新注入玩家的 Prior 权重依赖已有对局；可考虑在运行一段时间后导出 Priors，作为下次「从空状态启动」的初值，加速收敛。")
    md.append("")

    md.append("---")
    md.append("")
    md.append("*本报告为统计摘要，由 scripts/analyze_quality_deck_state.py 根据状态 JSON 自动生成。解读与问答见 **docs/quality_deck_state_qa.md**。*")
    md.append("")

    report = "\n".join(md)
    if out_path:
        out_path.parent.mkdir(parents=True, exist_ok=True)
        with open(out_path, "w", encoding="utf-8") as f:
            f.write(report)
    return report


def main() -> None:
    args = sys.argv[1:]
    path_arg = None
    out_path = None
    i = 0
    while i < len(args):
        if args[i] == "--out" and i + 1 < len(args):
            out_path = Path(args[i + 1])
            if not out_path.is_absolute():
                out_path = Path(__file__).resolve().parent.parent / out_path
            i += 2
            continue
        if not args[i].startswith("-"):
            path_arg = args[i]
        i += 1

    path = find_state_path(path_arg)
    if not path.exists():
        print(f"状态文件不存在: {path}", file=sys.stderr)
        print("请指定有效路径，或将 quality_deck_state.json 放在仓库根或 Data/ 下。", file=sys.stderr)
        sys.exit(1)

    dto = load_state(path)
    if not dto:
        print("无法解析状态 JSON。", file=sys.stderr)
        sys.exit(1)

    result = analyze(dto)
    report = write_report(result, out_path)

    if out_path:
        print(f"已写入: {out_path}")
    else:
        print(report)


if __name__ == "__main__":
    main()
