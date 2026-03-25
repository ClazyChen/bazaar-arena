# 中型铜物品测试用例

本文档描述针对「中型铜」物品的自动化测试用例。每个用例由两方卡组（P1 vs P2）与预期行为（日志中出现的关键内容或胜负）组成。卡组定义见 `Data/Decks/item_tests/test_medium_bronze.json`。

---

## 1. 尖刺圆盾（Spiked Buckler）

- **卡组**：P1 `mb_spiked_buckler_p1`（尖刺圆盾 铜），P2 `mb_spiked_buckler_p2`（獠牙 铜）
- **预期**：日志中出现「尖刺圆盾」「伤害」与「护盾」
- **目的**：验证武器伤害与护盾效果

## 2. 临时钝器（Improvised Bludgeon）

- **卡组**：P1 `mb_improvised_bludgeon_p1`（临时钝器 铜），P2 `mb_improvised_bludgeon_p2`（獠牙 铜）
- **预期**：日志中出现「临时钝器」「伤害」与「减速」
- **目的**：验证伤害与减速多目标

## 3. 暗影斗篷（Shadowed Cloak）

- **卡组**：P1 `mb_shadowed_cloak_p1`（暗影斗篷 铜 + 獠牙 铜 置于右侧），P2 `mb_shadowed_cloak_p2`（獠牙 铜）
- **预期**：日志中出现「暗影斗篷」与「加速」（使用右侧獠牙时触发）
- **目的**：验证「使用右侧物品时加速该物品」的触发器
- **状态**：通过

## 4. 冰冻钝器（Frozen Bludgeon）

- **卡组**：P1 `mb_frozen_bludgeon_p1`（冰冻钝器 铜），P2 `mb_frozen_bludgeon_p2`（獠牙 铜）
- **预期**：日志中出现「冰冻钝器」「伤害」与「冻结」
- **目的**：验证伤害、冻结与触发冻结时的武器伤害提高逻辑

## 5. 发条刀（Clockwork Blades）

- **卡组**：P1 `mb_clockwork_blades_p1`（发条刀 铜），P2 `mb_clockwork_blades_p2`（獠牙 铜）
- **预期**：日志中出现「发条刀」与「伤害 20」
- **目的**：验证铜档 20 伤害

## 6. 大理石鳞甲（Marble Scalemail）

- **卡组**：P1 `mb_marble_scalemail_p1`（大理石鳞甲 铜），P2 `mb_marble_scalemail_p2`（獠牙 铜）
- **预期**：日志中出现「大理石鳞甲」与「护盾」
- **目的**：验证纯护盾效果

## 7. 废品场大棒（Junkyard Club）

- **卡组**：P1 `mb_junkyard_club_p1`（废品场大棒 铜），P2 `mb_junkyard_club_p2`（獠牙 铜）
- **预期**：日志中出现「废品场大棒」与「伤害 30」
- **目的**：验证铜档 30 伤害

## 8. 火箭靴（Rocket Boots）

- **卡组**：P1 `mb_rocket_boots_p1`（火箭靴 铜 + 獠牙 铜 相邻），P2 `mb_rocket_boots_p2`（獠牙 铜）
- **预期**：日志中出现「火箭靴」与「加速」
- **目的**：验证加速相邻物品

## 9. 火蜥幼兽（Salamander Pup）

- **卡组**：P1 `mb_salamander_pup_p1`（火蜥幼兽 铜），P2 `mb_salamander_pup_p2`（獠牙 铜）
- **预期**：日志中出现「火蜥幼兽」与「灼烧」
- **目的**：验证伙伴灼烧效果

## 10. 简易路障（Makeshift Barricade）

- **卡组**：P1 `mb_makeshift_barricade_p1`（简易路障 铜），P2 `mb_makeshift_barricade_p2`（獠牙 铜）
- **预期**：日志中出现「简易路障」与「减速」
- **目的**：验证减速单目标

## 11. 外骨骼（Exoskeleton）

- **卡组**：P1 `mb_exoskeleton_p1`（外骨骼 铜 + 獠牙 铜 相邻），P2 `mb_exoskeleton_p2`（獠牙 铜）
- **预期**：日志中 P1 的獠牙造成 10 点伤害（獠牙铜 5 + 外骨骼相邻 +5），即出现「獠牙」与「伤害 10」
- **目的**：验证外骨骼对相邻武器的伤害加成光环（纯光环物品名不会出现在施放/效果日志中）
- **状态**：通过

## 12. 废品场维修机器人（Junkyard Repairbot）

- **卡组**：P1 `mb_junkyard_repairbot_p1`（牵引光束 银 + 獠牙 铜 + 废品场维修机器人 铜，从左到右；牵引光束使用后摧毁右侧獠牙以创造修复目标），P2 `mb_junkyard_repairbot_p2`（獠牙 铜）
- **预期**：日志中出现「牵引光束」「摧毁」、以及「废品场维修机器人」「修复」与「治疗」
- **目的**：验证治疗与修复逻辑（牵引光束先摧毁獠牙，维修机器人后续施放时修复该物品并治疗）

---

## 运行方式

- **CLI 单次运行**：`dotnet run --project src/BazaarArena.Cli -- Data/Decks/item_tests/test_medium_bronze.json <deck1_id> <deck2_id> --log Logs/test_medium_bronze.log`
- **自动化**：在仓库根目录执行 `python scripts/item_tests/run_item_tests_medium_bronze.py`，会依次运行上述用例并检查日志与退出码。
- **CLI 与测试流程说明**：见 **docs/cli-and-testing.md**。
