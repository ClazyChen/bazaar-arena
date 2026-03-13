#!/usr/bin/env python3
# 使用 CLI 运行大型铜物品测试用例，校验日志内容与退出码，并记录测试结果。
# 用法：
#   - 全量测试：在仓库根目录执行 python scripts/run_item_tests_large_bronze.py
#   - 仅重跑上次未通过的用例：python scripts/run_item_tests_large_bronze.py --failed-only

import json
import os
import subprocess
import sys

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DECK_JSON = os.path.join(REPO_ROOT, "Data", "Decks", "test_large_bronze.json")
LOG_DIR = os.path.join(REPO_ROOT, "Logs", "item_tests")
RESULTS_JSON = os.path.join(LOG_DIR, "results_large_bronze.json")
CLI_PROJECT = os.path.join(REPO_ROOT, "src", "BazaarArena.Cli", "BazaarArena.Cli.csproj")

LARGE_BRONZE_TESTS = [
    {"name": "临时避难所_temporary_shelter", "deck1": "lb_temporary_shelter_p1", "deck2": "lb_temporary_shelter_p2", "log_contains": ["临时避难所", "护盾"]},
    {"name": "哈库维发射器_harkuvian_launcher", "deck1": "lb_harkuvian_launcher_p1", "deck2": "lb_harkuvian_launcher_p2", "log_contains": ["哈库维发射器", "伤害 100"]},
    {"name": "观光缆车_tourist_chariot", "deck1": "lb_tourist_chariot_p1", "deck2": "lb_tourist_chariot_p2", "log_contains": ["观光缆车", "护盾"]},
    {"name": "温泉_hot_springs", "deck1": "lb_hot_springs_p1", "deck2": "lb_hot_springs_p2", "log_contains": ["温泉", "治疗"]},
    {"name": "废品场长枪_junkyard_lance", "deck1": "lb_junkyard_lance_p1", "deck2": "lb_junkyard_lance_p2", "log_contains": ["废品场长枪", "伤害"]},
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
    batch_path = os.path.join(LOG_DIR, "batch_large_bronze.json")
    battles = []
    for t in tests_to_run:
        name = t["name"]
        log_path = os.path.join(LOG_DIR, f"{name}.log")
        battles.append({"deck1": t["deck1"], "deck2": t["deck2"], "log": log_path})
    batch_cfg = {"battles": battles}
    with open(batch_path, "w", encoding="utf-8") as f:
        json.dump(batch_cfg, f, ensure_ascii=False, indent=2)

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

    tests_to_run = LARGE_BRONZE_TESTS
    if failed_only and previous:
        pending = [t for t in LARGE_BRONZE_TESTS if previous.get(t["name"], {}).get("status") != "ok"]
        if not pending:
            print("提示：上一次所有大型铜物品测试均已通过，本次 --failed-only 无需执行任何用例。")
            return 0
        print(f"仅重跑上次未通过的 {len(pending)} 个用例（总计 {len(LARGE_BRONZE_TESTS)} 个）：")
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

    print(f"\n全部 {len(LARGE_BRONZE_TESTS)} 个大型铜物品测试通过。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
