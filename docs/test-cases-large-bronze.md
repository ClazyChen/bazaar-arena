# 大型铜物品测试用例

本文档描述针对「大型铜」物品的自动化测试用例。每个用例由两方卡组（P1 vs P2）与预期行为（日志中出现的关键内容或胜负）组成。卡组定义见 `Data/Decks/test_large_bronze.json`。

---

## 1. 临时避难所（Temporary Shelter）

- **卡组**：P1 `lb_temporary_shelter_p1`（临时避难所 铜），P2 `lb_temporary_shelter_p2`（獠牙 铜）
- **预期**：日志中出现「临时避难所」与「护盾」
- **目的**：验证大型地产护盾效果

## 2. 哈库维发射器（Harkuvian Launcher）

- **卡组**：P1 `lb_harkuvian_launcher_p1`（哈库维发射器 铜），P2 `lb_harkuvian_launcher_p2`（獠牙 铜）
- **预期**：日志中出现「哈库维发射器」与「伤害 100」；有弹药时可出现「剩余弹药」相关日志
- **目的**：验证铜档 100 伤害与弹药机制

## 3. 观光缆车（Tourist Chariot）

- **卡组**：P1 `lb_tourist_chariot_p1`（观光缆车 铜），P2 `lb_tourist_chariot_p2`（獠牙 铜）
- **预期**：日志中出现「观光缆车」与「护盾」
- **目的**：验证大型载具护盾效果

## 4. 温泉（Hot Springs）

- **卡组**：P1 `lb_hot_springs_p1`（温泉 铜），P2 `lb_hot_springs_p2`（獠牙 铜）
- **预期**：日志中出现「温泉」与「治疗」
- **目的**：验证大型地产治疗效果

## 5. 废品场长枪（Junkyard Lance）

- **卡组**：P1 `lb_junkyard_lance_p1`（废品场长枪 铜 + 獠牙 铜，以触发「每拥有一件小型物品」伤害），P2 `lb_junkyard_lance_p2`（獠牙 铜）
- **预期**：日志中出现「废品场长枪」与「伤害」（伤害值随己方小型物品数及 StashParameter 计算，至少有一次伤害结算）
- **目的**：验证基于小型物品数量的伤害光环与施放伤害

---

## 运行方式

- **CLI 单次运行**：`dotnet run --project src/BazaarArena.Cli -- Data/Decks/test_large_bronze.json <deck1_id> <deck2_id> --log Logs/test_large_bronze.log`
- **自动化**：在仓库根目录执行 `python scripts/run_item_tests_large_bronze.py`，会依次运行上述用例并检查日志与退出码。
- **CLI 与测试流程说明**：见 **docs/cli-and-testing.md**。
