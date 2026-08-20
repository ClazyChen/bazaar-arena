# -*- coding: utf-8 -*-
"""精英认证与报告驱动（收敛闭环 + 自洽场重估 + Nash + 报告骨架）。

流程（定义与测量纪律见 docs/deck-search-pipeline.md）：
1. 候选池（--decks-file：名称 → 有序物品名，锚点 top1/DO 池/既往改良均可入池）。
2. 收敛闭环：每轮对每套卡组做邻域单步扰动（复用 neighborhood_scan.gen_candidates），
   在当前场上实测；存在 Δ ≥ +0.05 的最佳升级则**用独立 seed 确认**后替换（记录沿革），
   直到全部收敛。未收敛卡组不进精英名单；收敛到相同 multiset 的卡组合并。
3. 自洽场重估：最终名单内部循环赛（收敛轮已有数据），fictitious play 求 Nash σ。
4. 分层：精英层 = avg ≥ 0.45 或 σ 支撑；其余收敛卡组进流派层（引擎标签人工复核后定稿）。

性能设计（L14/L17 实测教训，v2）：
- **自适应波次评估**：对局胜率呈 U 型分布，多数 matchup 24 局即定型（CI≤0.10），
  仅未定型对局追加到 48/96 局——替代旧的固定 40/60/100 局。
- **升级确认**：初筛 Δ≥0.05 的"升级"必须在**独立 seed 集**上复核（Δ≥0.03 才接受），
  被拒候选拉入黑名单防止每轮复提——直接消除高等级宽平 meta 的噪声游移不收敛。
- **持久精确缓存**：对战结果对 (卡组, 对手, seed, 边) 完全确定（meta 层逐局可复现），
  按波次 seed 列表缓存到 <out-dir>/battle_cache.jsonl，跨迭代/跨运行复用。
  缓存首行存规则指纹（物品 YAML 内容哈希 + meta 二进制 mtime），规则变更自动失效。

用法（仓库根目录）：
    python scripts/meta_search/elite_report.py --level 8 --decks-file out/neighborhood/decks_r3.json \
        --out-dir out/elite_report/mak_l8
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))

from meta_search.gdf_conditions import load_item_db
from meta_search.nash import evaluate, fictitious_play
from meta_search.neighborhood_scan import (
    build_pool,
    gen_candidates,
    max_slots_for_level,
)

META_EXE = ROOT / "bin" / "bazaararena_meta.exe"

UPGRADE_DELTA = 0.05  # 初筛升级阈值
CONFIRM_DELTA = 0.03  # 独立 seed 确认阈值
ELITE_GATE = 0.45  # 精英层 avg 门槛（对最终场）
MAX_ITER = 10
CI_TOL = 0.10  # 自适应波次的 CI 半宽停止线
WAVE_SIZES = (24, 48, 96)  # 每对局的累计局数上限（逐波）
CONFIRM_SEED_BASE = 20000  # 升级确认用的独立 seed 区


def rules_fingerprint() -> str:
    h = hashlib.sha256()
    for f in sorted((ROOT / "data" / "items").glob("*.yaml")):
        h.update(f.read_bytes())
    h.update(str(META_EXE.stat().st_mtime_ns).encode())
    return h.hexdigest()[:16]


class BattleCache:
    """波次级精确缓存：key = (level, 卡组A, 卡组B, seed列表) → (wins_a, wins_b, ties)。"""

    def __init__(self, path: Path, fingerprint: str):
        self.path = path
        self.map: dict[str, list[int]] = {}
        if path.exists():
            lines = path.read_text(encoding="utf-8").splitlines()
            if lines and json.loads(lines[0]).get("fingerprint") == fingerprint:
                for line in lines[1:]:
                    d = json.loads(line)
                    self.map[d["key"]] = [d["wins_a"], d["wins_b"], d["ties"]]
            if self.map:
                print(f"[cache] {len(self.map)} 条命中候选", file=sys.stderr)
        self._fh = open(path, "a", encoding="utf-8")
        if not self.map:
            self._fh.write(json.dumps({"fingerprint": fingerprint}) + "\n")
            self._fh.flush()

    @staticmethod
    def key(level: int, a: list[str], b: list[str], seeds: list[int]) -> str:
        return json.dumps([level, a, b, seeds], ensure_ascii=False)

    def get(self, key: str) -> list[int] | None:
        return self.map.get(key)

    def put(self, key: str, wins_a: int, wins_b: int, ties: int) -> None:
        self.map[key] = [wins_a, wins_b, ties]
        self._fh.write(json.dumps(
            {"key": key, "wins_a": wins_a, "wins_b": wins_b, "ties": ties},
            ensure_ascii=False) + "\n")
        self._fh.flush()

    def close(self) -> None:
        self._fh.close()


def run_meta_batch(battles: list[dict], level: int, work_dir: Path, tag: str,
                   hero: str = "Mak") -> dict[str, list[int]]:
    """执行一批 battle（含 seeds）→ {id: [wins_a, wins_b, ties]}。"""
    if not battles:
        return {}
    job = {"data_dir": "data/items", "hero": hero, "level": level, "battles": battles}
    job_path = work_dir / f"job_{tag}.json"
    out_path = work_dir / f"out_{tag}.jsonl"
    job_path.write_text(json.dumps(job, ensure_ascii=False), encoding="utf-8")
    subprocess.run([str(META_EXE), "--input", str(job_path), "--output", str(out_path)],
                   cwd=ROOT, check=True)
    out: dict[str, list[int]] = {}
    for line in open(out_path, encoding="utf-8"):
        r = json.loads(line)
        out[r["id"]] = [r["wins_a"], r["wins_b"], r["ties"]]
    return out


def eval_adaptive(pairs: dict[str, tuple[list[str], list[str]]], level: int,
                  work_dir: Path, tag: str, cache: BattleCache,
                  seed_base: int, max_seeds: int, hero: str = "Mak") -> dict[str, float]:
    """自适应波次评估 {id: (卡组A, 卡组B)} → {id: winrate_a}。

    每波次内部 seed 列表前半不换边、后半换边（与 meta 协议一致），逐波累计，
    CI 半宽 ≤ CI_TOL 即停止；到 max_seeds 强制停止。
    """
    totals: dict[str, list[int]] = {pid: [0, 0, 0] for pid in pairs}
    pending = dict(pairs)
    start = 0
    for end in WAVE_SIZES:
        if end > max_seeds:
            end = max_seeds
        if not pending or start >= end:
            break
        wave_seeds = list(range(seed_base + start, seed_base + end))
        todo: list[dict] = []
        todo_keys: list[str] = []
        for pid, (a, b) in pending.items():
            key = BattleCache.key(level, a, b, wave_seeds)
            cached = cache.get(key)
            if cached:
                for i in range(3):
                    totals[pid][i] += cached[i]
            else:
                todo.append({"id": pid, "a": a, "b": b, "seeds": wave_seeds})
                todo_keys.append(key)
        res = run_meta_batch(todo, level, work_dir, f"{tag}_w{end}", hero)
        for battle, key in zip(todo, todo_keys):
            wa, wb, t = res[battle["id"]]
            cache.put(key, wa, wb, t)
            for i, v in enumerate((wa, wb, t)):
                totals[battle["id"]][i] += v
        # 定型判定
        still: dict[str, tuple[list[str], list[str]]] = {}
        for pid, ab in pending.items():
            wa, wb, t = totals[pid]
            n = wa + wb + t
            p = (wa + 0.5 * t) / n
            ci = 1.96 * math.sqrt(p * (1 - p) / n)
            if ci > CI_TOL and end < max_seeds:
                still[pid] = ab
        pending = still
        start = end
        if end >= max_seeds:
            break
    return {pid: (wa + 0.5 * t) / (wa + wb + t) for pid, (wa, wb, t) in totals.items()}


def field_pairs(decks: dict[str, list[str]]) -> dict[str, tuple[list[str], list[str]]]:
    return {f"@BASE|{a}||{b}": (da, db) for a, da in decks.items() for b, db in decks.items()}


def avg_against_field(wr: dict[str, float], prefix: str, names: list[str]) -> float:
    return sum(wr[f"{prefix}||{o}"] for o in names) / len(names)


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--level", type=int, required=True)
    ap.add_argument("--hero", default="mak", help="英雄（默认 mak；决定物品池与真值层 hero）")
    ap.add_argument("--decks-file", required=True)
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--seed-base", type=int, default=7000)
    ap.add_argument("--max-seeds", type=int, default=96,
                    help="每对局局数上限（自适应波次；默认 96）")
    ap.add_argument("--max-perm-classes", type=int, default=60)
    args = ap.parse_args()
    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    db = load_item_db(ROOT / "data" / "items", args.hero)
    pool = build_pool(db, args.level, args.hero)
    budget = max_slots_for_level(args.level)
    cache = BattleCache(out_dir / "battle_cache.jsonl", rules_fingerprint())

    decks = {k: list(v) for k, v in json.load(open(args.decks_file, encoding="utf-8")).items()}
    lineage: dict[str, list[str]] = {n: [", ".join(d)] for n, d in decks.items()}
    seen: dict[str, set[tuple]] = {n: {tuple(sorted(d))} for n, d in decks.items()}

    # ---- 收敛闭环 ----
    for it in range(1, MAX_ITER + 1):
        names = list(decks.keys())
        # 候选生成（含排列分层抽样，与 neighborhood_scan 一致）
        cand_owner: dict[str, tuple[str, list[str]]] = {}
        for name in names:
            cands = gen_candidates(decks[name], pool, db, budget)
            perm_ids = [k for k in cands if k.startswith("排列#")]
            if len(perm_ids) > args.max_perm_classes:
                half = args.max_perm_classes // 2
                keep = set(perm_ids[:half]) | set(perm_ids[half::max(1, len(perm_ids[half:]) // (args.max_perm_classes - half))][: args.max_perm_classes - half])
                cands = {k: v for k, v in cands.items() if not k.startswith("排列#") or k in keep}
            for tag, items in cands.items():
                if tuple(sorted(items)) in seen[name] and not tag.startswith("排列#"):
                    continue  # 黑名单/历史形态不再提议
                cand_owner[f"{name}|{tag}"] = (name, items)

        pairs = field_pairs(decks)
        for cid, (_, items) in cand_owner.items():
            for opp, od in decks.items():
                pairs[f"{cid}||{opp}"] = (items, od)
        print(f"[iter{it}] battles: {len(pairs)}（候选 {len(cand_owner)}）", file=sys.stderr)
        wr = eval_adaptive(pairs, args.level, out_dir, f"iter{it}", cache, args.seed_base,
                           args.max_seeds, args.hero)

        base_avg = {n: avg_against_field(wr, f"@BASE|{n}", names) for n in names}
        best_up: dict[str, tuple[float, str, list[str]]] = {}
        for cid, (owner, items) in cand_owner.items():
            d = avg_against_field(wr, cid, names) - base_avg[owner]
            if d >= UPGRADE_DELTA and (owner not in best_up or d > best_up[owner][0]):
                best_up[owner] = (d, cid, items)

        # 独立 seed 确认（同批复核 base，确保 Δ 口径一致）
        changed = False
        if best_up:
            cpairs: dict[str, tuple[list[str], list[str]]] = {}
            for owner, (_, cid, items) in best_up.items():
                for opp, od in decks.items():
                    cpairs[f"{cid}||{opp}"] = (items, od)
                    cpairs[f"@CF|{owner}||{opp}"] = (decks[owner], od)
            cwr = eval_adaptive(cpairs, args.level, out_dir, f"iter{it}_confirm", cache,
                                CONFIRM_SEED_BASE, args.max_seeds, args.hero)
            for owner, (d0, cid, items) in best_up.items():
                d = avg_against_field(cwr, cid, names) - avg_against_field(cwr, f"@CF|{owner}", names)
                tag = cid.split("|", 1)[1]
                is_perm = tag.startswith("排列#")
                ms = tuple(sorted(items))
                # 接受条件：确认达标；非排列候选不得是黑名单/历史形态（排列与原阵容同 multiset，不受此限）
                if d >= CONFIRM_DELTA and (is_perm or ms not in seen[owner]):
                    seen[owner].add(ms)
                    lineage[owner].append(f"{tag}（初筛Δ{d0:+.3f} 确认Δ{d:+.3f}）→ " + ", ".join(items))
                    decks[owner] = items
                    changed = True
                    print(f"[iter{it}] {owner}: {tag} 初筛Δ{d0:+.3f} 确认Δ{d:+.3f}", file=sys.stderr)
                else:
                    if not is_perm:
                        seen[owner].add(ms)  # 拒绝拉黑，防止复提（排列无法拉黑，见下）
                    print(f"[iter{it}] {owner}: {tag} 初筛Δ{d0:+.3f} 确认Δ{d:+.3f} → 拒绝", file=sys.stderr)
        if not changed:
            print(f"[iter{it}] 全部收敛", file=sys.stderr)
            break

    # 合并同 multiset（保留先出现者，沿革并入）
    seen_ms: dict[tuple, str] = {}
    merged: dict[str, list[str]] = {}
    for n, d in decks.items():
        ms = tuple(sorted(d))
        if ms in seen_ms:
            lineage[seen_ms[ms]].append(f"（合并：{n} 收敛到相同 multiset）")
            print(f"merge: {n} -> {seen_ms[ms]}", file=sys.stderr)
        else:
            seen_ms[ms] = n
            merged[n] = d
    decks = merged

    # ---- 自洽场循环赛 + Nash ----
    names = list(decks.keys())
    wr = eval_adaptive(field_pairs(decks), args.level, out_dir, "final_rr", cache,
                       args.seed_base, args.max_seeds, args.hero)
    n = len(names)
    M = [[wr[f"@BASE|{a}||{b}"] for b in names] for a in names]
    avg = {names[i]: sum(M[i]) / n for i in range(n)}
    sigma, _ = fictitious_play([row[:] for row in M])
    value, exploit, _, support = evaluate(M, sigma)
    cache.close()

    # ---- 报告骨架 ----
    lines = [f"# {args.hero} L{args.level} 精英认证报告（骨架，引擎标签待人工复核）\n"]
    lines.append(f"> 场 = {n} 套收敛卡组；自适应波次（CI≤{CI_TOL}，上限 96 局）；"
                 f"升级确认阈值 {CONFIRM_DELTA}（独立 seed）；精英门槛 avg≥{ELITE_GATE} 或 σ 支撑。\n")
    lines.append(f"Nash：value={value:.3f} exploitability={exploit:.4f}\n")
    lines.append("\n## 总表\n")
    lines.append("| 排名 | 卡组 | avg | 最差 matchup | σ | 层 |")
    lines.append("|---|---|---|---|---|---|")
    order = sorted(range(n), key=lambda i: -avg[names[i]])
    for rank, i in enumerate(order, 1):
        name = names[i]
        worst = min((M[i][j], names[j]) for j in range(n) if j != i)
        layer = "精英" if (avg[name] >= ELITE_GATE or i in support) else "流派?"
        lines.append(f"| {rank} | {name} | {avg[name]:.3f} | {worst[1]} {worst[0]:.3f} "
                     f"| {sigma[i]:.3f} | {layer} |")
    lines.append("\n## 克制矩阵（行 vs 列胜率，≥0.6 加粗）\n")
    lines.append("| |" + "|".join(names) + "|")
    lines.append("|" + "---|" * (n + 1))
    for i, a in enumerate(names):
        row = [a]
        for j, b in enumerate(names):
            v = M[i][j]
            row.append(f"**{v:.2f}**" if v >= 0.6 and i != j else f"{v:.2f}")
        lines.append("|" + "|".join(row) + "|")
    lines.append("\n## 阵容与沿革\n")
    for name in names:
        lines.append(f"### {name}")
        lines.append("最终阵容：" + ", ".join(decks[name]))
        for step in lineage[name]:
            lines.append(f"- {step}")
        lines.append("")
    lines.append("## 附录\n")
    lines.append(f"- 原始数据：{out_dir}（job/out JSONL 每轮各一份；battle_cache.jsonl 精确缓存）")
    lines.append(f"- 逐格认证明细：python scripts/meta_search/neighborhood_scan.py "
                 f"--hero {args.hero} --level {args.level} --decks-file {out_dir / 'final_decks.json'}")
    (out_dir / "elite_report.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    json.dump(decks, open(out_dir / "final_decks.json", "w", encoding="utf-8"),
              ensure_ascii=False, indent=2)
    print(f"report -> {out_dir / 'elite_report.md'}", file=sys.stderr)


if __name__ == "__main__":
    main()
