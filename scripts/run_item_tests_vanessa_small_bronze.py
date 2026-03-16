#!/usr/bin/env python3
# 海盗小型铜物品测试：覆盖 Vanessa 小型铜（最新版）全部物品。
# 用法：在仓库根目录执行 python scripts/run_item_tests_vanessa_small_bronze.py

import json
import os
import subprocess
import sys

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DECK_JSON = os.path.join(REPO_ROOT, "Data", "Decks", "test_small_bronze.json")
LOG_DIR = os.path.join(REPO_ROOT, "Logs", "item_tests")
CLI_PROJECT = os.path.join(REPO_ROOT, "src", "BazaarArena.Cli", "BazaarArena.Cli.csproj")

VANESSA_SMALL_BRONZE_TESTS = [
    {"name": "舱底蠕虫_bilge_worm", "deck1": "vanessa_sb_bilge_worm_p1", "deck2": "vanessa_sb_bilge_worm_p2", "log_contains": ["舱底蠕虫", "伤害"]},
    {"name": "藏刃匕首_concealed_dagger", "deck1": "vanessa_sb_concealed_dagger_p1", "deck2": "vanessa_sb_concealed_dagger_p2", "log_contains": ["藏刃匕首", "伤害", "加速"]},
    {"name": "食人鱼_piranha", "deck1": "vanessa_sb_piranha_p1", "deck2": "vanessa_sb_piranha_p2", "log_contains": ["食人鱼", "伤害"]},
    {"name": "三花_calico", "deck1": "vanessa_sb_calico_p1", "deck2": "vanessa_sb_calico_p2", "log_contains": ["三花", "伤害"]},
    {"name": "淬锋钢_honing_steel", "deck1": "vanessa_sb_honing_steel_p1", "deck2": "vanessa_sb_honing_steel_p2", "log_contains": ["淬锋钢", "提高"]},
    {"name": "独角鲸_narwhal", "deck1": "vanessa_sb_narwhal_p1", "deck2": "vanessa_sb_narwhal_p2", "log_contains": ["独角鲸", "伤害"]},
    {"name": "鱼饵_chum", "deck1": "vanessa_sb_chum_p1", "deck2": "vanessa_sb_chum_p2", "log_contains": ["鱼饵", "提高"]},
    {"name": "珊瑚_coral", "deck1": "vanessa_sb_coral_p1", "deck2": "vanessa_sb_coral_p2", "log_contains": ["珊瑚", "治疗"]},
    {"name": "迷幻蝠鲼_illuso_ray", "deck1": "vanessa_sb_illuso_ray_p1", "deck2": "vanessa_sb_illuso_ray_p2", "log_contains": ["迷幻蝠鲼", "减速"]},
    {"name": "打火机_lighter", "deck1": "vanessa_sb_lighter_p1", "deck2": "vanessa_sb_lighter_p2", "log_contains": ["打火机", "灼烧"]},
    {"name": "手里剑_shuriken", "deck1": "vanessa_sb_shuriken_p1", "deck2": "vanessa_sb_shuriken_p2", "log_contains": ["手里剑", "伤害"]},
    {"name": "刺刀_bayonet", "deck1": "vanessa_sb_bayonet_p1", "deck2": "vanessa_sb_bayonet_p2", "log_contains": ["刺刀", "伤害"]},
    {"name": "宠物石_pet_rock", "deck1": "vanessa_sb_pet_rock_p1", "deck2": "vanessa_sb_pet_rock_p2", "log_contains": ["宠物石", "伤害"]},
    {"name": "左轮手枪_revolver", "deck1": "vanessa_sb_revolver_p1", "deck2": "vanessa_sb_revolver_p2", "log_contains": ["左轮手枪", "伤害"]},
    {"name": "手斧_handaxe", "deck1": "vanessa_sb_handaxe_p1", "deck2": "vanessa_sb_handaxe_p2", "log_contains": ["手斧", "伤害"]},
    {"name": "手雷_grenade", "deck1": "vanessa_sb_grenade_p1", "deck2": "vanessa_sb_grenade_p2", "log_contains": ["手雷", "伤害"]},
    {"name": "抓钩_grappling_hook", "deck1": "vanessa_sb_grappling_hook_p1", "deck2": "vanessa_sb_grappling_hook_p2", "log_contains": ["抓钩", "伤害", "减速"]},
    {"name": "水草_seaweed", "deck1": "vanessa_sb_seaweed_p1", "deck2": "vanessa_sb_seaweed_p2", "log_contains": ["水草", "治疗"]},
    {"name": "流星索_bolas", "deck1": "vanessa_sb_bolas_p1", "deck2": "vanessa_sb_bolas_p2", "log_contains": ["流星索", "伤害", "减速"]},
    {"name": "海螺壳_sea_shell", "deck1": "vanessa_sb_sea_shell_p1", "deck2": "vanessa_sb_sea_shell_p2", "log_contains": ["海螺壳", "护盾"]},
    {"name": "燃烧响炮_pop_snappers", "deck1": "vanessa_sb_pop_snappers_p1", "deck2": "vanessa_sb_pop_snappers_p2", "log_contains": ["燃烧响炮", "灼烧"]},
    {"name": "珍珠_pearl", "deck1": "vanessa_sb_pearl_p1", "deck2": "vanessa_sb_pearl_p2", "log_contains": ["珍珠", "护盾"]},
    {"name": "棉鳚_zoarcid", "deck1": "vanessa_sb_zoarcid_p1", "deck2": "vanessa_sb_zoarcid_p2", "log_contains": ["棉鳚", "伤害", "加速"]},
    {"name": "葡萄弹_grapeshot", "deck1": "vanessa_sb_grapeshot_p1", "deck2": "vanessa_sb_grapeshot_p2", "log_contains": ["葡萄弹", "伤害"]},
    {
        "name": "迷你弯刀_tiny_cutlass",
        "deck1": "vanessa_sb_tiny_cutlass_p1",
        "deck2": "vanessa_sb_tiny_cutlass_p2",
        "log_contains": ["迷你弯刀", "伤害"],
        "log_min_count": {"[迷你弯刀] 伤害": 2},
    },
    {"name": "靴里剑_shoe_blade", "deck1": "vanessa_sb_shoe_blade_p1", "deck2": "vanessa_sb_shoe_blade_p2", "log_contains": ["靴里剑", "伤害"]},
    {"name": "龙涎香_ambergris", "deck1": "vanessa_sb_ambergris_p1", "deck2": "vanessa_sb_ambergris_p2", "log_contains": ["龙涎香", "治疗"]},
    {"name": "弹簧刀_switchblade", "deck1": "vanessa_sb_switchblade_p1", "deck2": "vanessa_sb_switchblade_p2", "log_contains": ["弹簧刀", "伤害"]},
    {"name": "水母_jellyfish", "deck1": "vanessa_sb_jellyfish_p1", "deck2": "vanessa_sb_jellyfish_p2", "log_contains": ["水母", "剧毒"]},
    {"name": "火药角_powder_horn", "deck1": "vanessa_sb_powder_horn_p1", "deck2": "vanessa_sb_powder_horn_p2", "log_contains": ["火药角", "装填"]},
    {"name": "鹦鹉皮特_pesky_pete", "deck1": "vanessa_sb_pesky_pete_p1", "deck2": "vanessa_sb_pesky_pete_p2", "log_contains": ["鹦鹉皮特", "灼烧"]},
    {"name": "毒须鲶_catfish", "deck1": "vanessa_sb_catfish_p1", "deck2": "vanessa_sb_catfish_p2", "log_contains": ["毒须鲶", "剧毒"]},
    {"name": "皮皮虾_mantis_shrimp", "deck1": "vanessa_sb_mantis_shrimp_p1", "deck2": "vanessa_sb_mantis_shrimp_p2", "log_contains": ["皮皮虾", "伤害", "灼烧"]},
    {"name": "雪怪蟹_yeti_crab", "deck1": "vanessa_sb_yeti_crab_p1", "deck2": "vanessa_sb_yeti_crab_p2", "log_contains": ["雪怪蟹", "冻结"]},
]


def run_cli_batch(tests_to_run: list) -> tuple[int, str, str]:
    os.makedirs(LOG_DIR, exist_ok=True)
    batch_path = os.path.join(LOG_DIR, "batch_vanessa_small_bronze.json")
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

    tests_to_run = VANESSA_SMALL_BRONZE_TESTS
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
                        (name, f"日志中 {sub!r} 出现次数 {log_content.count(sub)} < {min_count}（预期至少 {min_count} 次）", log_content[:2000])
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

    print(f"\n海盗小型铜物品测试通过（{len(tests_to_run)} 个用例）。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
