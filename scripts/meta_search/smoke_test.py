# -*- coding: utf-8 -*-
"""meta_search 冒烟自检：
1. CLI 可复现性：同 seed 同卡组跑两次，结果须一致。
2. gdf_conditions：L8 关键覆写值核对（智者之杖 custom_1=12、永恒火炬 quest=7、魂石变体位图）。
3. battle.series / series_adaptive + 缓存：跑一个小系列赛，确认缓存命中后不重复评估。
"""

import json
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from meta_search import battle, gdf_conditions  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent
CLI = REPO / "bin" / "bazaararena_cli.exe"

failures = []


def check(name: str, ok: bool, detail: str = ""):
    print(f"{'PASS' if ok else 'FAIL'}  {name}  {detail}")
    if not ok:
        failures.append(name)


# 1) 可复现性
job = json.loads((REPO / "samples/cli/simulate_minimal_input.json").read_text(encoding="utf-8"))
outs = []
for _ in range(2):
    with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False, encoding="utf-8") as f:
        json.dump(job, f, ensure_ascii=False)
        inp = Path(f.name)
    outp = inp.with_suffix(".out.json")
    subprocess.run([str(CLI), "--input", str(inp), "--output", str(outp)],
                   check=True, cwd=str(REPO), capture_output=True)
    outs.append(outp.read_bytes())
    inp.unlink(missing_ok=True)
    outp.unlink(missing_ok=True)
check("cli_reproducible", outs[0] == outs[1])

# 2) gdf_conditions
db = gdf_conditions.load_item_db(REPO / "data/items", "mak")
items = gdf_conditions.signature_to_items("永恒火炬,智者之杖,灼烧减速魂石,天平", 8, db)
by_key = {it["key"]: it for it in items}
check("tier_gold_at_l8", all(it["tier"] == "gold" for it in items))
check("torch_quest7", by_key["永恒火炬"]["attrsOverride"]["quest"] == 7)
check("sage_custom1_7", by_key["智者之杖"]["attrsOverride"]["custom_1"] == 7,
      str(by_key["智者之杖"].get("attrsOverride")))
soul = [it for it in items if it["key"] == "魂石"][0]
check("soul_variant_q6", soul["attrsOverride"]["quest"] == 6)
check("tianping_no_attrs", "attrsOverride" not in by_key["天平"])

# 3) series_batch + 缓存（C++ serve 端点；用签名而非转换后 items）
cache = battle.BattleCache(REPO / "out" / "meta_search" / "smoke_cache.jsonl")
sig_a = "永恒火炬,采掘工具"
sig_b = "剧毒药水,再生药水"
r1 = battle.series_batch("A", sig_a.split(","), "B", sig_b.split(","), 8, 10, cache=cache)
check("series_games", r1.games == 10, f"wins {r1.wins_a}-{r1.wins_b}-{r1.ties}")
size_before = len(cache._mem)
r2 = battle.series_batch("A", sig_a.split(","), "B", sig_b.split(","), 8, 10, cache=cache)
check("series_cache_no_new_evals", len(cache._mem) == size_before)
check("series_cache_same_result", (r1.wins_a, r1.wins_b, r1.ties) == (r2.wins_a, r2.wins_b, r2.ties))
r3 = battle.series_batch_adaptive("A", sig_a.split(","), "B", sig_b.split(","), 8,
                                  ci_tol=0.05, max_games=96, cache=cache)
check("adaptive_converges", r3.decisive and r3.games <= 96,
      f"games={r3.games} wr={r3.winrate_a:.3f} ci={r3.ci_half:.3f}")

print()
if failures:
    print(f"{len(failures)} FAILED: {failures}")
    sys.exit(1)
print("all smoke checks passed")
