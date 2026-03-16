#!/usr/bin/env python3
# 海盗中型铜物品测试：仅海底热泉（多重释放 3）一个用例，用于验证多重释放生效。
# 用法：在仓库根目录执行 python scripts/run_item_tests_vanessa_medium_bronze.py

import json
import os
import subprocess
import sys

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DECK_JSON = os.path.join(REPO_ROOT, "Data", "Decks", "test_medium_bronze.json")
LOG_DIR = os.path.join(REPO_ROOT, "Logs", "item_tests")
CLI_PROJECT = os.path.join(REPO_ROOT, "src", "BazaarArena.Cli", "BazaarArena.Cli.csproj")

# 海底热泉：多重释放 3，每次使用应打出 3 次灼烧效果
VANESSA_MEDIUM_BRONZE_TESTS = [
    {
        "name": "海底热泉_volcanic_vents",
        "deck1": "vanessa_mb_volcanic_vents_p1",
        "deck2": "vanessa_mb_volcanic_vents_p2",
        "log_contains": ["海底热泉", "灼烧"],
        "log_min_count": {"[海底热泉] 灼烧": 3},
    },
]


def run_cli_batch(tests_to_run: list) -> tuple[int, str, str]:
    os.makedirs(LOG_DIR, exist_ok=True)
    batch_path = os.path.join(LOG_DIR, "batch_vanessa_medium_bronze.json")
    battles = []
    for t in tests_to_run:
        log_path = os.path.join(LOG_DIR, f"{t['name']}.log")
        battles.append({"deck1": t["deck1"], "deck2": t["deck2"], "log": log_path})
    with open(batch_path, "w", encoding="utf-8") as f:
        json.dump({"battles": battles}, f, ensure_ascii=False, indent=2)

    result = subprocess.run(
        ["dotnet", "run", "--project", CLI_PROJECT, "--", DECK_JSON, "--batch", batch_path],
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

    tests_to_run = VANESSA_MEDIUM_BRONZE_TESTS
    rc, out, err = run_cli_batch(tests_to_run)
    if rc != 0:
        print(f"批量 CLI 退出码 {rc}\n{out}{err}", file=sys.stderr)
        return 1

    failed = []
    for t in tests_to_run:
        name = t["name"]
        log_path = os.path.join(LOG_DIR, f"{name}.log")
        try:
            with open(log_path, "r", encoding="utf-8", errors="replace") as f:
                log_content = f.read()
        except OSError as e:
            failed.append((name, f"无法读取日志: {e}", ""))
            continue

        for pattern in t["log_contains"]:
            if pattern not in log_content:
                failed.append((name, f"日志中未出现: {pattern!r}", log_content[:2000]))
                break
        else:
            for sub, min_count in (t.get("log_min_count") or {}).items():
                if log_content.count(sub) < min_count:
                    failed.append(
                        (name, f"日志中 {sub!r} 出现次数 {log_content.count(sub)} < {min_count}（预期多重释放 {min_count} 次）", log_content[:2000])
                    )
                    break
            else:
                print(f"  OK  {name}")

    if failed:
        print("\n失败用例:")
        for name, reason, detail in failed:
            print(f"  FAIL {name}: {reason}")
            if detail:
                print(detail[-1500:] if len(detail) > 1500 else detail)
        return 1

    print(f"\n海盗中型铜物品测试通过（{len(tests_to_run)} 个用例）。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
