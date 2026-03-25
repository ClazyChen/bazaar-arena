#!/usr/bin/env python3
# 使用 CLI 运行中型银物品测试用例，校验日志内容与退出码，并记录测试结果。
# 用法：
#   - 全量测试：在仓库根目录执行 python scripts/item_tests/run_item_tests_medium_silver.py
#   - 仅重跑上次未通过的用例：python scripts/item_tests/run_item_tests_medium_silver.py --failed-only

import json
import os
import subprocess
import sys

# 仓库根目录（脚本在 scripts/item_tests/ 下，上两级为根）
REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DECK_JSON = os.path.join(REPO_ROOT, "Data", "Decks", "item_tests", "test_medium_silver.json")
LOG_DIR = os.path.join(REPO_ROOT, "Logs", "item_tests")
RESULTS_JSON = os.path.join(LOG_DIR, "results_medium_silver.json")
CLI_PROJECT = os.path.join(REPO_ROOT, "src", "BazaarArena.Cli", "BazaarArena.Cli.csproj")

# 中型银物品测试：deck1, deck2, 日志中必须包含的字符串列表
MEDIUM_SILVER_TESTS = [
    {"name": "宇宙炫羽_cosmic_plume", "deck1": "ms_cosmic_plume_p1", "deck2": "ms_cosmic_plume_p2", "log_contains": ["宇宙炫羽", "开始飞行"]},
    {"name": "宇宙炫羽_250ms_cosmic_plume_doll", "deck1": "ms_cosmic_plume_250ms_p1", "deck2": "ms_cosmic_plume_250ms_p2", "log_contains": ["宇宙炫羽", "友好玩偶", "充能"], "log_assert_250ms_charge": "宇宙炫羽"},
    {"name": "巨龙翼_dragon_wing", "deck1": "ms_dragon_wing_p1", "deck2": "ms_dragon_wing_p2", "log_contains": ["巨龙翼", "护盾"]},
    {"name": "巨龙翼+巨龙崽崽_dragon_wing_whelp", "deck1": "ms_dragon_wing_whelp_p1", "deck2": "ms_dragon_wing_whelp_p2", "log_contains": ["巨龙崽崽", "开始飞行", "巨龙翼", "开始飞行"]},
    {"name": "碾骨爪_crusher_claw", "deck1": "ms_crusher_claw_p1", "deck2": "ms_crusher_claw_p2", "log_contains": ["碾骨爪", "伤害"]},
    {"name": "寒冰特服_cryosleeve", "deck1": "ms_cryosleeve_p1", "deck2": "ms_cryosleeve_p2", "log_contains": ["寒冰特服", "冻结"]},
    {"name": "守护神之壳_guardian_shell", "deck1": "ms_guardian_shell_p1", "deck2": "ms_guardian_shell_p2", "log_contains": ["守护神之壳", "护盾"]},
    {"name": "破冰尖镐_icebreaker", "deck1": "ms_icebreaker_p1", "deck2": "ms_icebreaker_p2", "log_contains": ["破冰尖镐", "伤害"]},
    {"name": "仿生手臂_bionic_arm", "deck1": "ms_bionic_arm_p1", "deck2": "ms_bionic_arm_p2", "log_contains": ["仿生手臂", "伤害", "75"]},
    {"name": "时光指针_hands_of_time", "deck1": "ms_hands_of_time_p1", "deck2": "ms_hands_of_time_p2", "log_contains": ["时光指针", "冷却缩短"]},
    {"name": "赛博铁尺_cyber_sai", "deck1": "ms_cyber_sai_p1", "deck2": "ms_cyber_sai_p2", "log_contains": ["赛博铁尺", "伤害提高"]},
    {"name": "带刃悬浮板_bladed_hoverboard", "deck1": "ms_bladed_hoverboard_p1", "deck2": "ms_bladed_hoverboard_p2", "log_contains": ["带刃悬浮板", "伤害"]},
    {"name": "元素深水炸弹_elemental_depth_charge", "deck1": "ms_elemental_depth_charge_p1", "deck2": "ms_elemental_depth_charge_p2", "log_contains": ["元素深水炸弹", "灼烧", "剧毒", "冻结"]},
    {"name": "填弹杆_ramrod", "deck1": "ms_ramrod_p1", "deck2": "ms_ramrod_p2", "log_contains": ["填弹杆", "装填", "暴击率提高"]},
    {"name": "标枪_装填触发_javelin_reload", "deck1": "ms_javelin_reload_p1", "deck2": "ms_javelin_reload_p2", "log_contains": ["标枪", "装填", "暴击率提高"]},
    {"name": "深潜器_submersible", "deck1": "ms_submersible_p1", "deck2": "ms_submersible_p2", "log_contains": ["深潜器", "伤害提高"]},
    {"name": "鱼雷_torpedo", "deck1": "ms_torpedo_p1", "deck2": "ms_torpedo_p2", "log_contains": ["鱼雷", "伤害提高"]},
    {"name": "恶鬼面具_oni_mask", "deck1": "ms_oni_mask_p1", "deck2": "ms_oni_mask_p2", "log_contains": ["恶鬼面具", "灼烧提高"]},
    {"name": "连发步枪_use_this_item_repeater", "deck1": "ms_repeater_use_this_item_p1", "deck2": "ms_repeater_use_this_item_p2", "log_contains": ["连发步枪", "伤害"]},
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
    batch_path = os.path.join(LOG_DIR, "batch_medium_silver.json")
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

    tests_to_run = MEDIUM_SILVER_TESTS
    if failed_only and previous:
        pending = [t for t in MEDIUM_SILVER_TESTS if previous.get(t["name"], {}).get("status") != "ok"]
        if not pending:
            print("提示：上一次所有中型银物品测试均已通过，本次 --failed-only 无需执行任何用例。")
            return 0
        print(f"仅重跑上次未通过的 {len(pending)} 个用例（总计 {len(MEDIUM_SILVER_TESTS)} 个）：")
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
            # 宇宙炫羽+友好玩偶：充能效果须遵守 250ms 间隔（使用飞行物品+暴击同帧触发时合并为一条，第二次 250ms 后）
            if pass_min_count and t.get("log_assert_250ms_charge") == "宇宙炫羽":
                import re
                pattern_ts = re.compile(r"\[宇宙炫羽\] 充能 [^\n]+ @ (\d+)ms")
                timestamps = [int(m.group(1)) for m in pattern_ts.finditer(log_content)]
                for i in range(1, len(timestamps)):
                    if timestamps[i] - timestamps[i - 1] < 250:
                        reason = f"宇宙炫羽充能间隔不足 250ms: {timestamps[i-1]}ms 与 {timestamps[i]}ms 相差 {timestamps[i]-timestamps[i-1]}ms"
                        failed.append((name, reason, log_content[:2000]))
                        results[name] = {"status": "fail", "reason": reason}
                        pass_min_count = False
                        break
            # 巨龙翼：开始飞行应只影响 1 件物品，即日志中「开始飞行 →[」与「]」之间不得出现「、」
            if name == "巨龙翼_dragon_wing":
                import re
                for m in re.finditer(r"开始飞行 →\[([^\]]*)\]", log_content):
                    if "、" in m.group(1):
                        reason = f"巨龙翼「开始飞行」应为 1 件物品，实际为多件: …{m.group(0)[:80]}…"
                        failed.append((name, reason, log_content[:2000]))
                        results[name] = {"status": "fail", "reason": reason}
                        pass_min_count = False
                        break
            # 巨龙翼 + 巨龙崽崽：巨龙崽崽飞行后，巨龙翼的开始飞行不应再次命中已飞行的巨龙崽崽
            if pass_min_count and name == "巨龙翼+巨龙崽崽_dragon_wing_whelp":
                bad = "玩家1 [巨龙翼] 开始飞行 →[巨龙崽崽]"
                if bad in log_content:
                    reason = "巨龙翼「开始飞行」不应再次命中已飞行的巨龙崽崽"
                    failed.append((name, reason, log_content[:2000]))
                    results[name] = {"status": "fail", "reason": reason}
                    pass_min_count = False
            if pass_min_count:
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

    print(f"\n全部 {len(MEDIUM_SILVER_TESTS)} 个中型银物品测试通过。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
