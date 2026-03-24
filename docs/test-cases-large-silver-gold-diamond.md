# 大型银/金/钻物品测试用例

本文档描述针对「大型银」「大型金」「大型钻」物品的自动化测试用例。每个用例由两方卡组（P1 vs P2）与预期行为（日志中出现的关键内容）组成。卡组定义见 `Data/Decks/item_tests/test_large_silver_gold_diamond.json`。脚本：`scripts/item_tests/run_item_tests_large_silver_gold_diamond.py`。

---

## 1. 废品场弹射机（Junkyard Catapult）- 大型银

- **卡组**：P1 `ls_junkyard_catapult_p1`（废品场弹射机 银），P2 `ls_junkyard_catapult_p2`（獠牙 铜）
- **预期**：日志中出现「废品场弹射机」「伤害」「灼烧」
- **目的**：验证大型银武器伤害、灼烧与剧毒（弹药 1）
- **测试状态**：通过

## 2. 巨型冰棒（Colossal Popsicle）- 大型银

- **卡组**：P1 `ls_colossal_popsicle_p1`（巨型冰棒 银），P2 `ls_colossal_popsicle_p2`（獠牙 铜）
- **预期**：日志中出现「巨型冰棒」「伤害」「冻结」
- **目的**：验证大型银武器伤害与冻结 2 件物品

## 3. 以太能量导体（Ethergy Conduit）- 大型金

- **卡组**：P1 `lg_ethergy_conduit_p1`（以太能量导体 金 + 驯化蜘蛛 铜），P2 `lg_ethergy_conduit_p2`（獠牙 铜）
- **预期**：日志中出现「以太能量导体」「暴击率」（驯化蜘蛛施加剧毒时触发「来源非遗物」的暴击率加成）
- **目的**：验证触发剧毒且来源不是遗物时己方暴击率提高；造成暴击时为己方遗物充能可另行观察
- **测试状态**：通过

## 4. 焰形剑（Flamberge）- 大型钻

- **卡组**：P1 `ld_flamberge_p1`（焰形剑 钻），P2 `ld_flamberge_p2`（獠牙 铜）
- **预期**：日志中出现「焰形剑」与「伤害」
- **目的**：验证大型钻武器 200 伤害与四倍暴击伤害光环

---

## 运行方式

- **CLI 单次运行**：`dotnet run --project src/BazaarArena.Cli -- Data/Decks/item_tests/test_large_silver_gold_diamond.json <deck1_id> <deck2_id> --log Logs/item_tests/<用例名>.log`
- **自动化**：在仓库根目录执行 `python scripts/item_tests/run_item_tests_large_silver_gold_diamond.py`，会依次运行上述用例并检查日志与退出码。
- **CLI 与测试流程说明**：见 **docs/cli-and-testing.md**。
