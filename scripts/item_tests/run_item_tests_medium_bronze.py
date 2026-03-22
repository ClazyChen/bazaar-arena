#!/usr/bin/env python3
# 使用 CLI 运行中型铜物品测试用例，校验日志内容与退出码，并记录测试结果。
# 用法：
#   - 全量测试：在仓库根目录执行 python scripts/item_tests/run_item_tests_medium_bronze.py
#   - 仅重跑上次未通过的用例：python scripts/item_tests/run_item_tests_medium_bronze.py --failed-only

import json
import os
import subprocess
import sys

# 仓库根目录（脚本在 scripts/item_tests/ 下，上两级为根）
REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DECK_JSON = os.path.join(REPO_ROOT, "Data", "Decks", "item_tests", "test_medium_bronze.json")
LOG_DIR = os.path.join(REPO_ROOT, "Logs", "item_tests")
RESULTS_JSON = os.path.join(LOG_DIR, "results_medium_bronze.json")
CLI_PROJECT = os.path.join(REPO_ROOT, "src", "BazaarArena.Cli", "BazaarArena.Cli.csproj")

# 中型铜物品测试：deck1, deck2, 日志中必须包含的字符串列表
MEDIUM_BRONZE_TESTS = [
    {"name": "尖刺圆盾_spiked_buckler", "deck1": "mb_spiked_buckler_p1", "deck2": "mb_spiked_buckler_p2", "log_contains": ["尖刺圆盾", "伤害", "护盾"]},
    {"name": "临时钝器_improvised_bludgeon", "deck1": "mb_improvised_bludgeon_p1", "deck2": "mb_improvised_bludgeon_p2", "log_contains": ["临时钝器", "伤害", "减速"]},
    {"name": "暗影斗篷_shadowed_cloak", "deck1": "mb_shadowed_cloak_p1", "deck2": "mb_shadowed_cloak_p2", "log_contains": ["暗影斗篷", "加速"]},
    {"name": "冰冻钝器_frozen_bludgeon", "deck1": "mb_frozen_bludgeon_p1", "deck2": "mb_frozen_bludgeon_p2", "log_contains": ["冰冻钝器", "伤害", "冻结"]},
    {"name": "发条刀_clockwork_blades", "deck1": "mb_clockwork_blades_p1", "deck2": "mb_clockwork_blades_p2", "log_contains": ["发条刀", "伤害 20"]},
    {"name": "大理石鳞甲_marble_scalemail", "deck1": "mb_marble_scalemail_p1", "deck2": "mb_marble_scalemail_p2", "log_contains": ["大理石鳞甲", "护盾"]},
    {"name": "废品场大棒_junkyard_club", "deck1": "mb_junkyard_club_p1", "deck2": "mb_junkyard_club_p2", "log_contains": ["废品场大棒", "伤害 30"]},
    {"name": "火箭靴_rocket_boots", "deck1": "mb_rocket_boots_p1", "deck2": "mb_rocket_boots_p2", "log_contains": ["火箭靴", "加速"]},
    {"name": "火蜥幼兽_salamander_pup", "deck1": "mb_salamander_pup_p1", "deck2": "mb_salamander_pup_p2", "log_contains": ["火蜥幼兽", "灼烧"]},
    {"name": "简易路障_makeshift_barricade", "deck1": "mb_makeshift_barricade_p1", "deck2": "mb_makeshift_barricade_p2", "log_contains": ["简易路障", "减速"]},
    {"name": "外骨骼_exoskeleton", "deck1": "mb_exoskeleton_p1", "deck2": "mb_exoskeleton_p2", "log_contains": ["獠牙", "伤害 10"]},
    {"name": "废品场维修机器人_junkyard_repairbot", "deck1": "mb_junkyard_repairbot_p1", "deck2": "mb_junkyard_repairbot_p2", "log_contains": ["牵引光束", "摧毁", "废品场维修机器人", "修复", "治疗"]},
]


def load_previous_results() -> dict[str, dict]:
    """读取上一次测试结果（若不存在则返回空字典）。"""
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
    """将本次测试结果写入 JSON 文件。"""
    os.makedirs(os.path.dirname(RESULTS_JSON), exist_ok=True)
    with open(RESULTS_JSON, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=2, sort_keys=True)


def run_cli_batch(tests_to_run: list[dict]) -> tuple[int, str, str]:
    """批量运行 CLI 对战，返回 (returncode, stdout, stderr)。"""
    os.makedirs(LOG_DIR, exist_ok=True)
    batch_path = os.path.join(LOG_DIR, "batch_medium_bronze.json")
    battles = []
    for t in tests_to_run:
        name = t["name"]
        log_path = os.path.join(LOG_DIR, f"{name}.log")
        battles.append(
            {
                "deck1": t["deck1"],
                "deck2": t["deck2"],
                "log": log_path,
            }
        )
    batch_cfg = {"battles": battles}
    with open(batch_path, "w", encoding="utf-8") as f:
        json.dump(batch_cfg, f, ensure_ascii=False, indent=2)

    cmd = [
        "dotnet",
        "run",
        "--project",
        CLI_PROJECT,
        "--",
        DECK_JSON,
        "--batch",
        batch_path,
    ]
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

    tests_to_run = MEDIUM_BRONZE_TESTS
    if failed_only and previous:
        pending = [t for t in MEDIUM_BRONZE_TESTS if previous.get(t["name"], {}).get("status") != "ok"]
        if not pending:
            print("提示：上一次所有中型铜物品测试均已通过，本次 --failed-only 无需执行任何用例。")
            return 0
        print(f"仅重跑上次未通过的 {len(pending)} 个用例（总计 {len(MEDIUM_BRONZE_TESTS)} 个）：")
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
            print(detail[-1500:] if len(detail) > 1500 else detail)
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

        for pattern in t["log_contains"]:
            if pattern not in log_content:
                reason = f"日志中未出现: {pattern!r}"
                failed.append((name, reason, log_content[:2000]))
                results[name] = {"status": "fail", "reason": reason}
                break
        else:
            pass_min_count = True
            for sub, min_count in (t.get("log_min_count") or {}).items():
                if log_content.count(sub) < min_count:
                    reason = f"日志中 {sub!r} 出现次数 {log_content.count(sub)} < {min_count}"
                    failed.append((name, reason, log_content[:2000]))
                    results[name] = {"status": "fail", "reason": reason}
                    pass_min_count = False
                    break
            if pass_min_count:
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

    print(f"\n全部 {len(MEDIUM_BRONZE_TESTS)} 个中型铜物品测试通过。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
