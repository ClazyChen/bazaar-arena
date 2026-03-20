# -*- coding: utf-8 -*-
"""从 quality_deck_state.json 提取按 Elo 排序的 TOP N 虚拟卡组，并写出 JSON + Markdown 报告。"""
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path


def shape_to_str(shape: list[int]) -> str:
    m = {1: "S", 2: "M", 3: "L"}
    return "".join(m.get(x, str(x)) for x in shape)


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument(
        "--input",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "quality_deck_state.json",
    )
    ap.add_argument("--top", type=int, default=100)
    ap.add_argument(
        "--json-out",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "Data" / "quality_top100.json",
    )
    ap.add_argument(
        "--md-out",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "docs" / "quality-top100-report.md",
    )
    args = ap.parse_args()

    with args.input.open(encoding="utf-8") as f:
        data = json.load(f)

    vc = data.get("VirtualCombos") or []
    sorted_vc = sorted(vc, key=lambda c: (-c.get("Elo", 0.0), c.get("ComboSig", "")))
    top = sorted_vc[: args.top]

    # 机器可读
    args.json_out.parent.mkdir(parents=True, exist_ok=True)
    out_payload = {
        "Source": str(args.input),
        "StateVersion": data.get("Version"),
        "TopN": args.top,
        "GeneratedFrom": {
            "VirtualCombosCount": len(vc),
            "HistoryCombosCount": len(data.get("HistoryCombos") or []),
        },
        "Decks": [],
    }
    for i, c in enumerate(top, start=1):
        shape = c.get("RepresentativeShape") or []
        items = c.get("RepresentativeItems") or []
        out_payload["Decks"].append(
            {
                "Rank": i,
                "Elo": c.get("Elo"),
                "ComboSig": c.get("ComboSig"),
                "RepresentativeShape": shape,
                "ShapePattern": shape_to_str(shape),
                "RepresentativeItems": items,
                "IsConfirmed": c.get("IsConfirmed"),
                "IsLocalOptimum": c.get("IsLocalOptimum"),
                "GameCount": c.get("GameCount"),
            }
        )
    with args.json_out.open("w", encoding="utf-8") as f:
        json.dump(out_payload, f, ensure_ascii=False, indent=2)

    # 结构统计（基于 TOP N）
    sml_list = []
    item_counter: Counter[str] = Counter()
    shape_pattern_counter: Counter[str] = Counter()
    for c in top:
        shape = c.get("RepresentativeShape") or []
        items = c.get("RepresentativeItems") or []
        s = sum(1 for x in shape if x == 1)
        m = sum(1 for x in shape if x == 2)
        l = sum(1 for x in shape if x == 3)
        sml_list.append((s, m, l))
        shape_pattern_counter[shape_to_str(shape)] += 1
        for name in items:
            item_counter[name] += 1

    summary = data.get("Summary") or {}
    cfg = data.get("ConfigSnapshot") or {}

    lines: list[str] = []
    lines.append("# Quality Deck Finder — TOP{} 卡组与结构分析\n".format(args.top))
    lines.append("由 `scripts/extract_quality_top100.py` 从状态文件自动生成。\n")
    lines.append("## 状态文件概览\n")
    lines.append("| 字段 | 值 |")
    lines.append("|------|-----|")
    lines.append("| 路径 | `{}` |".format(args.input.as_posix()))
    lines.append("| Version | {} |".format(data.get("Version")))
    lines.append("| CurrentSeason | {} |".format(data.get("CurrentSeason")))
    lines.append("| TotalGames | {} |".format(data.get("TotalGames")))
    lines.append("| TotalClimbs | {} |".format(data.get("TotalClimbs")))
    lines.append("| TotalRestarts | {} |".format(data.get("TotalRestarts")))
    lines.append("| VirtualCombos | {} |".format(len(vc)))
    lines.append("| HistoryCombos | {} |".format(len(data.get("HistoryCombos") or [])))
    lines.append("| Summary.PoolSize | {} |".format(summary.get("PoolSize")))
    lines.append("| Summary.HistoryPoolSize | {} |".format(summary.get("HistoryPoolSize")))
    lines.append("| Summary.MaxElo | {} |".format(summary.get("MaxElo")))
    lines.append("| Summary.MinElo | {} |".format(summary.get("MinElo")))
    lines.append("| Summary.ConfirmedCount | {} |".format(summary.get("ConfirmedCount")))
    lines.append("| Summary.LocalOptimaCount | {} |".format(summary.get("LocalOptimaCount")))
    lines.append("")
    lines.append("### ConfigSnapshot（节选）\n")
    if cfg:
        for k in (
            "GamesPerEval",
            "InitialElo",
            "EloK",
            "SegmentCap",
            "InnerWars",
            "ConfirmOpponents",
            "ConfirmGamesPerOpponent",
        ):
            if k in cfg:
                lines.append("- **{}**: {}".format(k, cfg[k]))
    else:
        lines.append("- （无）")
    lines.append("")

    lines.append("## 数据结构说明（与 `Combo.cs` / `StatePersistence` 对齐）\n")
    lines.append("- **ComboSig**：与排列无关的组合签名，形如 `c=小,中,大|物品A,物品B,...`（物品名已排序）。")
    lines.append("- **RepresentativeShape**：各槽尺寸编码，`1`=小 `2`=中 `3`=大；与 **RepresentativeItems** 等长一一对应，表示用于对战的「代表排列」（本文件中多为 5 或 6 槽）。")
    lines.append("- **Elo**：该组合在搜索过程中的评分。")
    lines.append("- **IsConfirmed / IsLocalOptimum / GameCount**：确认状态、局部最优标记、本赛季（或当前统计窗口内）对局数。")
    lines.append("")

    lines.append("## TOP{} 列表\n".format(args.top))
    lines.append("| 排名 | Elo | 槽型 | 物品（按槽位） | Confirmed | LocalOpt | Games |")
    lines.append("|-----:|----:|:-----|:---------------|:----------|:---------:|------:|")
    for row in out_payload["Decks"]:
        items_join = "，".join(row["RepresentativeItems"])
        if len(items_join) > 80:
            items_join = items_join[:77] + "..."
        lines.append(
            "| {} | {:.1f} | {} | {} | {} | {} | {} |".format(
                row["Rank"],
                row["Elo"],
                row["ShapePattern"],
                items_join,
                "是" if row["IsConfirmed"] else "否",
                "是" if row["IsLocalOptimum"] else "否",
                row["GameCount"],
            )
        )
    lines.append("")
    lines.append("完整字段（含 ComboSig）见：`{}`。\n".format(args.json_out.as_posix()))

    lines.append("## TOP{} 结构分析\n".format(args.top))
    n_conf = sum(1 for c in top if c.get("IsConfirmed"))
    n_loc = sum(1 for c in top if c.get("IsLocalOptimum"))
    lines.append("- **Confirmed 数量**: {} / {}".format(n_conf, args.top))
    lines.append("- **LocalOptimum 数量**: {} / {}".format(n_loc, args.top))
    lines.append("")

    if sml_list:
        avg_s = sum(t[0] for t in sml_list) / len(sml_list)
        avg_m = sum(t[1] for t in sml_list) / len(sml_list)
        avg_l = sum(t[2] for t in sml_list) / len(sml_list)
        lines.append("### 槽位尺寸计数（小/中/大）\n")
        slot_lens = [len(c.get("RepresentativeShape") or []) for c in top]
        mn_sl, mx_sl = (min(slot_lens), max(slot_lens)) if slot_lens else (0, 0)
        lines.append(
            "- TOP{} 内平均：小 {:.2f}、中 {:.2f}、大 {:.2f}；槽位数范围 {}～{}（本状态为 Vanessa 等池时常见 5 槽：4 小+1 中，或 6 小）".format(
                args.top, avg_s, avg_m, avg_l, mn_sl, mx_sl
            )
        )
        sml_tuples = Counter(sml_list)
        lines.append("- 最常见的 (小,中,大) 分布（前 10）：")
        for (s, m, l), cnt in sml_tuples.most_common(10):
            lines.append("  - ({},{},{}): {} 套".format(s, m, l, cnt))
        lines.append("")

    lines.append("### 代表排列槽型串（ShapePattern）Top 15\n")
    for pat, cnt in shape_pattern_counter.most_common(15):
        lines.append("- `{}`: {} 套".format(pat, cnt))
    lines.append("")

    lines.append("### 物品出现频次 Top 40（跨 TOP{} 槽位计数）\n".format(args.top))
    for name, cnt in item_counter.most_common(40):
        lines.append("- **{}**: {}".format(name, cnt))
    lines.append("")

    elo_min = top[-1]["Elo"] if top else 0
    elo_max = top[0]["Elo"] if top else 0
    lines.append("### Elo 边界\n")
    lines.append("- TOP1 Elo: **{:.1f}**".format(elo_max))
    lines.append("- TOP{} 门槛 Elo: **{:.1f}**".format(args.top, elo_min))
    lines.append("")

    args.md_out.parent.mkdir(parents=True, exist_ok=True)
    with args.md_out.open("w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    print("Wrote", args.json_out)
    print("Wrote", args.md_out)


if __name__ == "__main__":
    main()
