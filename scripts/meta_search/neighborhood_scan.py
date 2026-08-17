# -*- coding: utf-8 -*-
"""精英卡组邻域穷举扫描（Mak L8）。

对每套精英卡组穷举三类单步扰动并用 bazaararena_meta 真值层评估：
1. 单格替换：每个槽位换成池内所有尺寸兼容的物品（同位置），魂石家族互斥、禁重复；
2. 减件：依次移除每件物品（少于 10 格合法）；
3. 排列：同一 multiset 的机制等价类代表（perm_constraints.partition_permutations）。

产物（默认写入 out/neighborhood/）：
- report.md：每套精英的槽位认证（承重/可平换/可升级）、减件代价、位置敏感度，
  以及全局的升级发现与顶层克制卡清单；
- scan_results.jsonl：全部 (候选 × 对手) 原始胜率，供二次分析。

用法（仓库根目录）：
    python scripts/meta_search/neighborhood_scan.py
    python scripts/meta_search/neighborhood_scan.py --seeds 60 --seed-base 9000
    python scripts/meta_search/neighborhood_scan.py --decks-file decks.json --scan 卡组A,卡组B
        --decks-file：JSON {名称: [有序物品名]}，替换内置 v3 精英场；
        --scan：只对这些卡组生成扰动候选（对手场仍为 decks-file 全体）。
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))

from meta_search.gdf_conditions import load_item_db
from meta_search.perm_constraints import partition_permutations

META_EXE = ROOT / "bin" / "bazaararena_meta.exe"

# 与 engine/src/bazaararena/gdf/ItemPool.cpp 一致的 Mak 排除清单。
IGNORED_MAK = {"产药药水", "催化剂", "筛盘", "奥秘之书", "亚罕典籍", "蒸馏器", "空灵灰烬"}
SOUL_FAMILY = {"魂石", "剧毒减速魂石", "剧毒冻结魂石", "灼烧减速魂石", "灼烧冻结魂石"}
SOUL_VARIANTS = ["剧毒减速魂石", "剧毒冻结魂石", "灼烧减速魂石", "灼烧冻结魂石"]
SIZE_OF = {"Small": 1, "Medium": 2, "Large": 3}
# 与 GdfLevelRules::IsMinTierAllowedInPool 一致
MIN_TIER_LEVEL_GATE = {"bronze": 1, "silver": 5, "gold": 8, "diamond": 11}


def max_slots_for_level(level: int) -> int:
    """与 GdfLevelRules::MaxSlotsForLevel 一致。"""
    if level <= 1:
        return 4
    if level == 2:
        return 6
    if level == 3:
        return 8
    return 10

# v3 精英（docs/archive/mak-l8-meta-v3-postfix.md §2/§4）
ELITES: dict[str, list[str]] = {
    "冰霜冻结王": ["剧毒冻结魂石", "冰霜之怖", "寒霜图腾", "冰爪", "光学强化", "瓶装龙卷风", "无敌药水"],
    "股骨重炮": ["剧毒减速魂石", "琥珀", "马格努斯的股骨", "符文药水", "采掘工具", "时间之砂", "嗅盐"],
    "注能自毒武器": ["蛇怪之牙", "水银", "实验体阿尔法", "蕨叶蜘蛛", "蜘蛛连枷", "注能护腕"],
    "图书馆壳": ["力量药水", "图书馆", "采掘工具", "永恒火炬", "嗅盐", "灼烧减速魂石", "无敌药水"],
    "炼金炉": ["力量药水", "符文药水", "瓶装龙卷风", "炼金炉", "飞行药水", "沸腾烧瓶", "碎瓶"],
    "自毒注射": ["瘟疫长柄刀", "光学强化", "腐朽圣像", "快速注射系统", "肾上腺素调节服"],
    "瘟疫毒": ["剧毒减速魂石", "力量药水", "蕨叶蜘蛛", "毒液", "瘟疫长柄刀", "嗅盐", "瓶装龙卷风"],
    "黑冰冻结毒": ["能量药水", "瘟疫长柄刀", "寒霜图腾", "剧毒冻结魂石", "蓝宝石", "黑冰"],
    "天平充能": ["采掘工具", "永恒火炬", "能量药水", "天平", "灼烧减速魂石", "力量药水", "智者之杖"],
}

TIE_DELTA = 0.05  # 9 对手 × 40 局的 avg，±0.05 约 2σ


def build_pool(db: dict[str, dict], level: int) -> list[str]:
    pool = []
    for name, it in db.items():
        if name in IGNORED_MAK:
            continue
        tier = str(it.get("Tier", "Bronze")).lower()
        if level < MIN_TIER_LEVEL_GATE.get(tier, 99):
            continue
        pool.append(name)
    if "魂石" in pool:
        pool.extend(SOUL_VARIANTS)
    return sorted(pool)


def size_of(name: str, db: dict[str, dict]) -> int:
    base = "魂石" if name in SOUL_VARIANTS else name
    return SIZE_OF[db[base]["Size"]]


def gen_candidates(elite: list[str], pool: list[str], db: dict[str, dict],
                   budget: int) -> dict[str, list[str]]:
    """候选名 → 有序物品列表。含减件/替换/排列三类。"""
    cands: dict[str, list[str]] = {}
    in_deck = set(elite)
    total = sum(size_of(n, db) for n in elite)

    for i, item in enumerate(elite):
        removed = elite[:i] + elite[i + 1 :]
        cands[f"-{item}@{i}"] = removed

    for i, item in enumerate(elite):
        s = size_of(item, db)
        rest_soul = (in_deck - {item}) & SOUL_FAMILY
        for c in pool:
            if c == item or c in in_deck:
                continue
            if total - s + size_of(c, db) > budget:
                continue
            if c in SOUL_FAMILY and rest_soul:
                continue  # 魂石家族互斥
            cands[f"{item}@{i}→{c}"] = elite[:i] + [c] + elite[i + 1 :]

    try:
        classes = partition_permutations(elite, db, max_enumerate=400000)
    except ValueError:
        classes = []  # 唯一排列过多（≥10 件物品）：跳过排列扫描
        print(f"warn: {elite} 排列过多，跳过排列扫描", file=sys.stderr)
    for k, cls in enumerate(classes):
        perm = list(cls.representative)
        if perm != elite:
            cands[f"排列#{k}"] = perm
    return cands


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", type=int, default=40)
    ap.add_argument("--seed-base", type=int, default=7000)
    ap.add_argument("--out-dir", default=str(ROOT / "out" / "neighborhood"))
    ap.add_argument("--max-perm-classes", type=int, default=60)
    ap.add_argument("--decks-file", default=None,
                    help="JSON {名称: [有序物品名]}，替换内置 v3 精英场")
    ap.add_argument("--scan", default=None,
                    help="逗号分隔；只对这些卡组生成扰动（对手场仍为全体）")
    ap.add_argument("--level", type=int, default=8, help="玩家等级（默认 8）")
    args = ap.parse_args()
    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    elites = ELITES
    if args.decks_file:
        elites = {k: list(v) for k, v in
                  json.load(open(args.decks_file, encoding="utf-8")).items()}
    scan_names = list(elites.keys())
    if args.scan:
        scan_names = args.scan.split(",")
        missing = [n for n in scan_names if n not in elites]
        if missing:
            raise SystemExit(f"--scan 中的卡组不在 decks 里：{missing}")

    db = load_item_db(ROOT / "data" / "items", "mak")
    pool = build_pool(db, args.level)
    print(f"pool: {len(pool)} items", file=sys.stderr)

    # ---- 生成全部候选 ----
    all_cands: dict[str, list[str]] = {}  # 唯一 id → 物品列表
    elite_cands: dict[str, list[str]] = {}  # 精英名 → 候选 id 列表
    for ename in scan_names:
        deck = elites[ename]
        cands = gen_candidates(deck, pool, db, max_slots_for_level(args.level))
        # 排列类过多时分层抽样：头部按类大小（质量集中处）+ 尾部等距（覆盖长尾）
        perm_ids = [k for k in cands if k.startswith("排列#")]
        if len(perm_ids) > args.max_perm_classes:
            half = args.max_perm_classes // 2
            head = perm_ids[:half]
            rest = perm_ids[half:]
            step = max(1, len(rest) // max(1, args.max_perm_classes - half))
            tail = rest[::step][: args.max_perm_classes - half]
            keep = set(head) | set(tail)
            cands = {k: v for k, v in cands.items() if not k.startswith("排列#") or k in keep}
        ids = []
        for tag, items in cands.items():
            cid = f"{ename}|{tag}"
            all_cands[cid] = items
            ids.append(cid)
        elite_cands[ename] = ids
        print(f"{ename}: {len(ids)} candidates", file=sys.stderr)

    # ---- 构建任务单：候选 × 精英场 ----
    seeds = list(range(args.seed_base, args.seed_base + args.seeds))
    battles = []
    for cid, items in all_cands.items():
        for oname, odeck in elites.items():
            battles.append({"id": f"{cid}||{oname}", "a": items, "b": odeck, "seeds": seeds})
    # 精英自身基线（同 seed 同批跑，保证可比）
    for ename, deck in elites.items():
        for oname, odeck in elites.items():
            battles.append({"id": f"@BASE|{ename}||{oname}", "a": deck, "b": odeck, "seeds": seeds})
    job = {"data_dir": "data/items", "hero": "Mak", "level": args.level, "battles": battles}
    job_path = out_dir / "scan_job.json"
    out_path = out_dir / "scan_results.jsonl"
    job_path.write_text(json.dumps(job, ensure_ascii=False), encoding="utf-8")
    print(f"battles: {len(battles)} games: {len(battles) * len(seeds)}", file=sys.stderr)
    subprocess.run([str(META_EXE), "--input", str(job_path), "--output", str(out_path)],
                   cwd=ROOT, check=True)

    # ---- 汇总 ----
    wr: dict[str, dict[str, float]] = {}
    for line in open(out_path, encoding="utf-8"):
        r = json.loads(line)
        cid, oname = r["id"].rsplit("||", 1)
        n = r["wins_a"] + r["wins_b"] + r["ties"]
        wr.setdefault(cid, {})[oname] = (r["wins_a"] + 0.5 * r["ties"]) / n

    opp_names = list(elites.keys())
    base_avg = {}
    for ename in opp_names:
        rows = wr[f"@BASE|{ename}"]
        base_avg[ename] = sum(rows[o] for o in opp_names) / len(opp_names)

    def avg_of(cid: str) -> float:
        rows = wr[cid]
        return sum(rows[o] for o in opp_names) / len(opp_names)

    # ---- 报告 ----
    lines: list[str] = []
    lines.append(f"# 精英邻域穷举认证报告（Mak L{args.level}）\n")
    lines.append(f"> seeds {args.seed_base}–{args.seed_base + args.seeds - 1}，"
                 f"每对局 {args.seeds} 局，对手 = {len(opp_names)} 卡组。阈值 ±{TIE_DELTA} ≈ 2σ。\n")

    upgrades_global: list[tuple[float, str, str]] = []  # (Δ, elite, tag)
    for ename in scan_names:
        deck = elites[ename]
        b = base_avg[ename]
        lines.append(f"\n## {ename}（基线 avg {b:.3f}）\n")
        lines.append("阵容：" + ",".join(deck) + "\n")

        repl: dict[int, list[tuple[float, str]]] = {}
        removals: list[tuple[float, str]] = []
        perms: list[tuple[float, str]] = []
        for cid in elite_cands[ename]:
            tag = cid.split("|", 1)[1]
            a = avg_of(cid)
            if tag.startswith("-"):
                removals.append((a, tag))
            elif tag.startswith("排列#"):
                perms.append((a, tag))
            else:
                slot = int(tag.split("@")[1].split("→")[0])
                repl.setdefault(slot, []).append((a, tag))

        lines.append("| 槽位 | 物品 | 最佳替代 | Δ | 认证 |")
        lines.append("|---|---|---|---|---|")
        for slot in sorted(repl):
            item = deck[slot]
            rows = sorted(repl[slot], key=lambda x: -x[0])
            best_a, best_tag = rows[0]
            best_c = best_tag.split("→")[1]
            delta = best_a - b
            n_tie = sum(1 for a, _ in rows if abs(a - b) < TIE_DELTA)
            if delta >= TIE_DELTA:
                cert = f"**可升级**（{best_c}）"
                upgrades_global.append((delta, ename, f"{item}@{slot}→{best_c}"))
            elif n_tie > 0:
                cert = f"可平换（{n_tie} 件在噪声内，如 {best_c}）"
            else:
                worst_a = rows[-1][0]
                cert = f"承重（最佳替代 {best_c} {delta:+.3f}，最差 {worst_a - b:+.3f}）"
            lines.append(f"| {slot} | {item} | {best_c} | {delta:+.3f} | {cert} |")

        if removals:
            worst = min(removals)
            best_r = max(removals)
            lines.append(f"\n减件：最痛 {worst[1][1:]}（{worst[0] - b:+.3f}），"
                         f"最轻 {best_r[1][1:]}（{best_r[0] - b:+.3f}）")
        if perms:
            pmax, pmin = max(perms)[0], min(perms)[0]
            spread = pmax - pmin
            note = "位置敏感" if spread >= TIE_DELTA else "位置不敏感"
            lines.append(f"排列：{len(perms)} 个等价类，avg 极差 {spread:.3f}（{note}）；"
                         f"基线顺序是否最优类：{'否，更优类存在' if pmax > b + TIE_DELTA else '是/噪声内'}")

    if upgrades_global:
        lines.append("\n## 升级发现（Δ ≥ 阈值）\n")
        for delta, ename, tag in sorted(upgrades_global, key=lambda x: -x[0]):
            lines.append(f"- {ename}：{tag}（{delta:+.3f}）")
    else:
        lines.append("\n## 升级发现\n\n全部精英的所有单格替换均在基线之下或噪声内——单格扰动邻域无升级。\n")

    # 顶层克制卡：在 avg 不太垮（≥ 父代 -0.10）的候选中，找对前二卡组的最高胜率
    lines.append("\n## 对顶层卡组的克制卡（候选 avg ≥ 父代基线 −0.10）\n")
    kings = sorted(opp_names, key=lambda n: -base_avg[n])[:2]
    for king in kings:
        rows = []
        for ename in scan_names:
            for cid in elite_cands[ename]:
                a = avg_of(cid)
                if a >= base_avg[ename] - 0.10:
                    rows.append((wr[cid][king], a, cid.split("|", 1)[1], ename))
        rows.sort(key=lambda x: -x[0])
        lines.append(f"\n对 {king}：")
        for w, a, tag, ename in rows[:5]:
            lines.append(f"- {ename} 变体[{tag}]：对王 {w:.3f}（自身 avg {a:.3f}）")

    report = "\n".join(lines) + "\n"
    (out_dir / "report.md").write_text(report, encoding="utf-8")
    print(f"report -> {out_dir / 'report.md'}", file=sys.stderr)


if __name__ == "__main__":
    main()
