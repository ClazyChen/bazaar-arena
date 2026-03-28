#!/usr/bin/env python3
# 海盗 Vanessa：表格批量导入的金/钻档（及部分配套）物品测试；卡组见 test_vanessa_gold_diamond_batch.json。
# 用法：在仓库根目录执行 python scripts/item_tests/run_item_tests_vanessa_gold_diamond_batch.py

import json
import os
import subprocess
import sys

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DECK_JSON = os.path.join(REPO_ROOT, "Data", "Decks", "item_tests", "test_vanessa_gold_diamond_batch.json")
LOG_DIR = os.path.join(REPO_ROOT, "Logs", "item_tests")
RESULTS_JSON = os.path.join(LOG_DIR, "results_vanessa_gold_diamond_batch.json")
CLI_PROJECT = os.path.join(REPO_ROOT, "src", "BazaarArena.Cli", "BazaarArena.Cli.csproj")

VANESSA_GOLD_DIAMOND_BATCH_TESTS = [
    {"name": "投掷飞刀_throwing_knives", "deck1": "vgd_throw_knives_p1", "deck2": "vgd_throw_knives_p2", "log_contains": ["投掷飞刀", "伤害"]},
    {"name": "吹箭枪_blowgun", "deck1": "vgd_blowgun_p1", "deck2": "vgd_blowgun_p2", "log_contains": ["吹箭枪", "伤害", "剧毒"]},
    {"name": "侦查望远镜_spyglass", "deck1": "vgd_spyglass_p1", "deck2": "vgd_spyglass_p2", "log_contains": ["侦查望远镜", "暴击率提高", "冷却时间提高"]},
    {"name": "侦查望远镜_S1_spyglass_s1", "deck1": "vgd_spyglass_s1_p1", "deck2": "vgd_spyglass_s1_p2", "log_contains": ["侦查望远镜_S1", "冷却时间提高"]},
    {"name": "潜水头盔_diving_helmet", "deck1": "vgd_diving_helmet_p1", "deck2": "vgd_diving_helmet_p2", "log_contains": ["潜水头盔", "护盾", "充能"]},
    {"name": "划艇_rowboat", "deck1": "vgd_rowboat_p1", "deck2": "vgd_rowboat_p2", "log_contains": ["划艇", "充能"]},
    {"name": "钢琴_piano", "deck1": "vgd_piano_p1", "deck2": "vgd_piano_p2", "log_contains": ["钢琴", "加速"]},
    {"name": "刺刀手枪_pistol_sword", "deck1": "vgd_pistol_sword_p1", "deck2": "vgd_pistol_sword_p2", "log_contains": ["刺刀手枪", "伤害"]},
    {"name": "绊索_tripwire", "deck1": "vgd_tripwire_p1", "deck2": "vgd_tripwire_p2", "log_contains": ["绊索", "减速"]},
    {"name": "逞威风腰带扣_swash_buckle", "deck1": "vgd_swash_buckle_p1", "deck2": "vgd_swash_buckle_p2", "log_contains": ["獠牙", "伤害"]},
    {"name": "龟壳_turtle_shell", "deck1": "vgd_turtle_shell_p1", "deck2": "vgd_turtle_shell_p2", "log_contains": ["龟壳", "护盾", "充能"]},
    {"name": "火药桶_powder_keg", "deck1": "vgd_powder_keg_p1", "deck2": "vgd_powder_keg_p2", "log_contains": ["火药桶", "伤害", "摧毁"]},
    {"name": "狙击步枪_sniper_rifle", "deck1": "vgd_sniper_rifle_p1", "deck2": "vgd_sniper_rifle_p2", "log_contains": ["狙击步枪", "伤害"]},
    {"name": "船锚_anchor", "deck1": "vgd_anchor_p1", "deck2": "vgd_anchor_p2", "log_contains": ["船锚", "伤害"]},
    {"name": "雷筒_blunderbuss", "deck1": "vgd_blunderbuss_p1", "deck2": "vgd_blunderbuss_p2", "log_contains": ["雷筒", "伤害", "灼烧"]},
    {"name": "潜行滑翔机_stealth_glider", "deck1": "vgd_stealth_glider_p1", "deck2": "vgd_stealth_glider_p2", "log_contains": ["潜行滑翔机", "无敌"]},
    {"name": "滚石_the_boulder", "deck1": "vgd_the_boulder_p1", "deck2": "vgd_the_boulder_p2", "log_contains": ["滚石", "伤害"]},
    {"name": "巨龟托图加_tortuga", "deck1": "vgd_tortuga_p1", "deck2": "vgd_tortuga_p2", "log_contains": ["巨龟托图加", "伤害", "加速"]},
    {"name": "大坝_dam", "deck1": "vgd_dam_p1", "deck2": "vgd_dam_p2", "log_contains": ["大坝", "摧毁"]},
    {"name": "弩炮_ballista", "deck1": "vgd_ballista_p1", "deck2": "vgd_ballista_p2", "log_contains": ["弩炮", "伤害"]},
    {"name": "沉眠元初体_slumbering_primordial", "deck1": "vgd_slumbering_primordial_p1", "deck2": "vgd_slumbering_primordial_p2", "log_contains": ["沉眠元初体", "伤害", "灼烧"]},
    {"name": "火炮阵列_cannonade", "deck1": "vgd_cannonade_p1", "deck2": "vgd_cannonade_p2", "log_contains": ["火炮阵列", "伤害", "充能"]},
    {"name": "电鳗_electric_eels", "deck1": "vgd_electric_eels_p1", "deck2": "vgd_electric_eels_p2", "log_contains": ["电鳗", "伤害", "减速"]},
    {"name": "灯塔_lighthouse", "deck1": "vgd_lighthouse_p1", "deck2": "vgd_lighthouse_p2", "log_contains": ["灯塔", "减速", "灼烧"]},
    {"name": "热带岛屿_tropical_island", "deck1": "vgd_tropical_island_p1", "deck2": "vgd_tropical_island_p2", "log_contains": ["热带岛屿", "生命再生"]},
    {"name": "冰山_iceberg", "deck1": "vgd_iceberg_p1", "deck2": "vgd_iceberg_p2", "log_contains": ["冰山", "冻结"]},
    {"name": "船骸_shipwreck", "deck1": "vgd_shipwreck_p1", "deck2": "vgd_shipwreck_p2", "log_contains": ["食人鱼", "伤害"], "log_min_count": {"[食人鱼] 伤害": 2}},
]


def load_previous_results() -> dict[str, dict]:
    try:
        if not os.path.isfile(RESULTS_JSON):
            return {}
        with open(RESULTS_JSON, "r", encoding="utf-8") as f:
            data = json.load(f)
        if isinstance(data, dict):
            return data
    except Exception:
        pass
    return {}


def save_results(results: dict[str, dict]) -> None:
    os.makedirs(os.path.dirname(RESULTS_JSON), exist_ok=True)
    with open(RESULTS_JSON, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=2, sort_keys=True)


def run_cli_batch(tests_to_run: list[dict]) -> tuple[int, str, str]:
    os.makedirs(LOG_DIR, exist_ok=True)
    batch_path = os.path.join(LOG_DIR, "batch_vanessa_gold_diamond_batch.json")
    battles = []
    for t in tests_to_run:
        name = t["name"]
        log_path = os.path.join(LOG_DIR, f"{name}.log")
        battles.append({"deck1": t["deck1"], "deck2": t["deck2"], "log": log_path})
    with open(batch_path, "w", encoding="utf-8") as f:
        json.dump({"battles": battles}, f, ensure_ascii=False, indent=2)

    cmd = ["dotnet", "run", "--project", CLI_PROJECT, "--", DECK_JSON, "--batch", batch_path]
    result = subprocess.run(
        cmd,
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    return result.returncode, result.stdout, result.stderr


def main() -> int:
    if not os.path.isfile(DECK_JSON):
        print(f"错误：卡组集不存在 {DECK_JSON}", file=sys.stderr)
        return 2
    if not os.path.isfile(CLI_PROJECT):
        print(f"错误：CLI 项目不存在 {CLI_PROJECT}", file=sys.stderr)
        return 2

    args = [a for a in sys.argv[1:] if not a.startswith("-psn_")]
    failed_only = "--failed-only" in args

    previous = load_previous_results()
    results: dict[str, dict] = dict(previous)

    tests_to_run = VANESSA_GOLD_DIAMOND_BATCH_TESTS
    if failed_only and previous:
        pending = [t for t in VANESSA_GOLD_DIAMOND_BATCH_TESTS if previous.get(t["name"], {}).get("status") != "ok"]
        if not pending:
            print("提示：上一次本批次测试均已通过，--failed-only 无需执行。")
            return 0
        tests_to_run = pending

    failed: list[tuple[str, str, str]] = []

    rc, out, err = run_cli_batch(tests_to_run)
    if rc != 0:
        reason = f"批量 CLI 退出码 {rc}"
        detail = out + err
        for t in tests_to_run:
            failed.append((t["name"], reason, detail))
            results[t["name"]] = {"status": "fail", "reason": reason}
        save_results(results)
        print(f"\n批量 CLI 执行失败：{reason}")
        if detail:
            print(detail[-2000:] if len(detail) > 2000 else detail)
        return 1

    for t in tests_to_run:
        name = t["name"]
        log_path = os.path.join(LOG_DIR, f"{name}.log")
        try:
            with open(log_path, "r", encoding="utf-8", errors="replace") as f:
                log_content = f.read()
        except OSError as e:
            reason = f"无法读取日志: {e}"
            failed.append((name, reason, ""))
            results[name] = {"status": "fail", "reason": reason}
            continue

        ok_patterns = True
        for pattern in t["log_contains"]:
            if pattern not in log_content:
                reason = f"日志中未出现: {pattern!r}"
                failed.append((name, reason, log_content[:2500]))
                results[name] = {"status": "fail", "reason": reason}
                ok_patterns = False
                break
        if not ok_patterns:
            continue
        pass_min_count = True
        for sub, min_count in (t.get("log_min_count") or {}).items():
            if log_content.count(sub) < min_count:
                reason = f"日志中 {sub!r} 出现次数 {log_content.count(sub)} < {min_count}"
                failed.append((name, reason, log_content[:2500]))
                results[name] = {"status": "fail", "reason": reason}
                pass_min_count = False
                break
        if not pass_min_count:
            continue
        print(f"  OK  {name}")
        results[name] = {"status": "ok", "reason": ""}

    save_results(results)

    if failed:
        print(f"\n失败 {len(failed)} 个用例:")
        for name, reason, detail in failed:
            print(f"  FAIL {name}: {reason}")
            if detail:
                print(detail[-1500:] if len(detail) > 1500 else detail)
        return 1

    print(f"\n全部 {len(VANESSA_GOLD_DIAMOND_BATCH_TESTS)} 个 Vanessa 金钻批次物品测试通过。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
