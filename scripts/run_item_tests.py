#!/usr/bin/env python3
# 使用 CLI 运行小型铜物品测试用例，校验日志内容与退出码。
# 用法：在仓库根目录执行 python scripts/run_item_tests.py

import os
import re
import subprocess
import sys

# 仓库根目录（脚本在 scripts/ 下，上级为根）
REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DECK_JSON = os.path.join(REPO_ROOT, "Data", "Decks", "test_small_bronze.json")
LOG_DIR = os.path.join(REPO_ROOT, "Logs", "item_tests")
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


def run_cli(deck1_id: str, deck2_id: str, log_path: str) -> tuple[int, str, str]:
    """运行 CLI 对战，返回 (returncode, stdout, stderr)。"""
    os.makedirs(os.path.dirname(log_path), exist_ok=True)
    cmd = [
        "dotnet", "run", "--project", CLI_PROJECT, "--",
        DECK_JSON, deck1_id, deck2_id,
        "--log", log_path,
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

    failed = []
    for t in SMALL_BRONZE_TESTS:
        name = t["name"]
        log_path = os.path.join(LOG_DIR, f"{name}.log")
        rc, out, err = run_cli(t["deck1"], t["deck2"], log_path)

        if rc != 0:
            failed.append((name, f"CLI 退出码 {rc}", out + err))
            continue

        # 使用传入的 log_path 读取日志（与 CLI 写入路径一致）
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
            print(f"  OK  {name}")

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
