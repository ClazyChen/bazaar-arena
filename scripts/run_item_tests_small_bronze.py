#!/usr/bin/env python3
# 使用 CLI 运行小型铜物品测试用例，校验日志内容与退出码，并记录测试结果。
# 用法：
#   - 全量测试：在仓库根目录执行 python scripts/run_item_tests_small_bronze.py
#   - 仅重跑上次未通过的用例：python scripts/run_item_tests_small_bronze.py --failed-only

import json
import os
import re
import subprocess
import sys

# 仓库根目录（脚本在 scripts/ 下，上级为根）
REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DECK_JSON = os.path.join(REPO_ROOT, "Data", "Decks", "test_small_bronze.json")
LOG_DIR = os.path.join(REPO_ROOT, "Logs", "item_tests")
RESULTS_JSON = os.path.join(LOG_DIR, "results_small_bronze.json")
CLI_PROJECT = os.path.join(REPO_ROOT, "src", "BazaarArena.Cli", "BazaarArena.Cli.csproj")

# 小型铜物品测试：deck1, deck2, 日志中必须包含的字符串列表
SMALL_BRONZE_TESTS = [
    {"name": "獠牙_fang", "deck1": "sb_fang_p1", "deck2": "sb_fang_p2", "log_contains": ["獠牙", "伤害"]},
    {"name": "岩浆核心_lava_core", "deck1": "sb_lava_core_p1", "deck2": "sb_lava_core_p2", "log_contains": ["灼烧结算"]},
    {"name": "驯化蜘蛛_spider", "deck1": "sb_spider_p1", "deck2": "sb_spider_p2", "log_contains": ["剧毒结算"]},
    {"name": "举重手套_lifting_gloves", "deck1": "sb_lifting_gloves_p1", "deck2": "sb_lifting_gloves_p2", "log_contains": ["獠牙", "伤害 6"]},
    {"name": "符文手斧_rune_axe", "deck1": "sb_rune_axe_p1", "deck2": "sb_rune_axe_p2", "log_contains": ["符文手斧", "伤害 15"]},
    {"name": "放大镜_magnifying_glass", "deck1": "sb_magnifying_glass_p1", "deck2": "sb_magnifying_glass_p2", "log_contains": ["放大镜", "伤害 5"]},
    {"name": "古董剑_old_sword", "deck1": "sb_old_sword_p1", "deck2": "sb_old_sword_p2", "log_contains": ["古董剑", "伤害 5"]},
    {"name": "轻步靴_agility_boots", "deck1": "sb_agility_boots_p1", "deck2": "sb_agility_boots_p2", "log_contains": ["獠牙", "伤害"]},
    {"name": "利爪_claws", "deck1": "sb_claws_p1", "deck2": "sb_claws_p2", "log_contains": ["利爪", "伤害"]},
    {"name": "蓝蕉_bluenanas", "deck1": "sb_bluenanas_p1", "deck2": "sb_bluenanas_p2", "log_contains": ["蓝蕉", "治疗"]},
    {"name": "冰锥_icicle", "deck1": "sb_icicle_p1", "deck2": "sb_icicle_p2", "log_contains": ["冻结"]},
    {"name": "毒刺_stinger", "deck1": "sb_stinger_p1", "deck2": "sb_stinger_p2", "log_contains": ["毒刺", "伤害", "减速"]},
    {"name": "裂盾刀_sunderer", "deck1": "sb_sunderer_p1", "deck2": "sb_sunderer_p2", "log_contains": ["裂盾刀", "护盾"]},
    {"name": "姜饼人_gingerbread", "deck1": "sb_gingerbread_p1", "deck2": "sb_gingerbread_p2", "log_contains": ["姜饼人", "护盾", "充能"]},
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
        # 读取或解析失败时视为无历史结果
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
    batch_path = os.path.join(LOG_DIR, "batch_small_bronze.json")
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

    # 命令行选项：--failed-only 表示仅重跑上一次未通过的用例
    args = [a for a in sys.argv[1:] if not a.startswith("-psn_")]  # macOS 可执行参数噪音忽略
    failed_only = "--failed-only" in args

    previous = load_previous_results()
    # 结果结构：name -> {status: "ok"/"fail", reason: str}
    results: dict[str, dict] = dict(previous)

    # 选择本次要执行的用例列表
    tests_to_run = SMALL_BRONZE_TESTS
    if failed_only and previous:
        pending = []
        for t in SMALL_BRONZE_TESTS:
            name = t["name"]
            prev_status = previous.get(name, {}).get("status")
            if prev_status != "ok":
                pending.append(t)
        if not pending:
            print("提示：上一次所有小型铜物品测试均已通过，本次 --failed-only 无需执行任何用例。")
            return 0
        print(f"仅重跑上次未通过的 {len(pending)} 个用例（总计 {len(SMALL_BRONZE_TESTS)} 个）：")
        tests_to_run = pending

    failed: list[tuple[str, str, str]] = []

    # 先用批量模式一次性跑完需要测试的用例
    rc, out, err = run_cli_batch(tests_to_run)
    if rc != 0:
        # CLI 批量执行本身失败，视为所有本次用例失败，reason 记录统一错误
        reason = f"批量 CLI 退出码 {rc}"
        detail = out + err
        for t in tests_to_run:
            name = t["name"]
            failed.append((name, reason, detail))
            results[name] = {"status": "fail", "reason": reason}
        save_results(results)
        print(f"\n批量 CLI 执行失败：{reason}")
        if detail:
            print(detail[-1500:] if len(detail) > 1500 else detail)
        return 1

    # 再逐个用例检查对应日志文件与内容
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
            print(f"  OK  {name}")
            results[name] = {"status": "ok", "reason": ""}

    # 未执行的用例保留上一次的结果；这里不做额外处理，results 已在最开始拷贝 previous
    save_results(results)

    if failed:
        print(f"\n失败 {len(failed)} 个用例:")
        for name, reason, detail in failed:
            print(f"  FAIL {name}: {reason}")
            if detail:
                print(detail[-1500:] if len(detail) > 1500 else detail)
        return 1

    print(f"\n全部 {len(SMALL_BRONZE_TESTS)} 个小型铜物品测试通过。")
    return 0


if __name__ == "__main__":
    sys.exit(main())

