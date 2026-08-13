# -*- coding: utf-8 -*-
"""meta 矩阵构建：卡组集合 → 全对全自适应系列赛 →  payoff 矩阵。

产物 JSON 结构：
{
  "level": 8,
  "decks": { name: {"signature": "...", "items": [...]} },
  "payoffs": { "a|b": {"winrate_a": 0.62, "games": 40, "ci_half": 0.04, "decisive": true} }
}
"""

from __future__ import annotations

import concurrent.futures as cf
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from meta_search import battle, gdf_conditions  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent


def build_matrix(
    decks: dict[str, str],
    level: int,
    out_path: Path,
    *,
    ci_tol: float = 0.05,
    max_games: int = 96,
    batch: int = 8,
    workers: int = 12,
    cache: battle.BattleCache | None = None,
    existing: Path | None = None,
) -> dict:
    """全对全自适应循环赛（C++ 批量端点）。decks: name → 物品签名（展示名，可含变体）。

    existing 指向已有矩阵时可增量补齐缺失对局。"""
    payoffs: dict[str, dict] = {}
    if existing and Path(existing).exists():
        prev = json.loads(Path(existing).read_text(encoding="utf-8"))
        payoffs.update(prev.get("payoffs", {}))

    names = sorted(decks.keys())
    todo = []
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            if f"{a}|{b}" not in payoffs:
                todo.append((a, b))
    print(f"decks={len(names)} pairs_total={len(names) * (len(names) - 1) // 2} todo={len(todo)}")

    # 自适应波次（C++ 内卷）：整批对局一次提交，C++ 逐波加局至收敛，单行返回
    wave_chunk = 2000  # 控制单行大小
    params = {"ci_tol": ci_tol, "batch": batch, "max_games": max_games,
              "sign_margin": 0.15, "base_seed": 1000}
    server = battle.MetaServer.shared(level)
    for start in range(0, len(todo), wave_chunk):
        chunk = todo[start:start + wave_chunk]
        reqs = [(f"{a}|{b}", decks[a].split(","), decks[b].split(",")) for a, b in chunk]
        out = server.play_adaptive_wave(reqs, params, workers=workers)
        for r in out:
            payoffs[r["id"]] = {
                "winrate_a": round((r["wins_a"] + 0.5 * r["ties"]) / r["games"], 4),
                "games": r["games"],
                "ci_half": round(r["ci_half"], 4),
                "decisive": r["decisive"],
            }
            if cache:
                a, b = r["id"].split("|", 1)
                for j, res in enumerate(r["results"]):
                    # 交替换侧约定：game j 的 swap = j%2；缓存键按物理 side 顺序
                    if j % 2 == 0:
                        cache.put(a, b, 1000 + j, res)
                    else:
                        cache.put(b, a, 1000 + j, res)
        print(f"  {start + len(chunk)}/{len(todo)}")

    doc = {
        "level": level,
        "decks": {n: {"signature": decks[n]} for n in names},
        "payoffs": payoffs,
    }
    Path(out_path).parent.mkdir(parents=True, exist_ok=True)
    Path(out_path).write_text(json.dumps(doc, ensure_ascii=False), encoding="utf-8")
    print(f"wrote {out_path} ({len(payoffs)} pairs)")
    return doc


def avg_winrates(doc: dict) -> dict[str, float]:
    names = list(doc["decks"].keys())
    acc = {n: [] for n in names}
    for key, p in doc["payoffs"].items():
        a, b = key.split("|", 1)
        if a in acc and b in acc:
            acc[a].append(p["winrate_a"])
            acc[b].append(1 - p["winrate_a"])
    return {n: (sum(ws) / len(ws) if ws else 0.0) for n, ws in acc.items()}


def main() -> int:
    """从 TSV（anchor,rr,anchor_m,swiss,deck）构建矩阵：每行一个卡组，取签名去重。"""
    import argparse
    import csv

    p = argparse.ArgumentParser()
    p.add_argument("--tsv", type=Path, required=True)
    p.add_argument("--hero", default="mak")
    p.add_argument("--data-dir", type=Path, default=REPO / "data" / "items")
    p.add_argument("--level", type=int, default=8)
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--ci-tol", type=float, default=0.05)
    p.add_argument("--max-games", type=int, default=96)
    p.add_argument("--workers", type=int, default=12)
    p.add_argument("--existing", type=Path, default=None)
    args = p.parse_args()

    decks: dict[str, str] = {}
    for row in csv.DictReader(open(args.tsv, encoding="utf-8"), delimiter="\t"):
        sig = row["deck"]
        name = row["anchor"]
        if name in decks:  # 同锚点重名防御
            name = f"{name}#{sig[:8]}"
        decks[name] = sig

    cache = battle.BattleCache()
    build_matrix(decks, args.level, args.out, ci_tol=args.ci_tol,
                 max_games=args.max_games, workers=args.workers,
                 cache=cache, existing=args.existing)
    doc = json.loads(args.out.read_text(encoding="utf-8"))
    print("\n== avg winrate top 15 ==")
    for name, wr in sorted(avg_winrates(doc).items(), key=lambda kv: -kv[1])[:15]:
        print(f"  {wr:.3f}  {name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
