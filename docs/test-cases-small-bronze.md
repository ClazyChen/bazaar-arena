# 小型铜物品测试用例

本文档描述针对「小型铜」物品的自动化测试用例。每个用例由两方卡组（P1 vs P2）与预期行为（日志中出现的关键内容或胜负）组成。卡组定义见 `Data/Decks/test_small_bronze.json`。

---

## 1. 獠牙（Fang）

- **卡组**：P1 `sb_fang_p1`（2 獠牙 铜），P2 `sb_fang_p2`（1 獠牙 铜）
- **预期**：日志中出现「獠牙」与「伤害」；战斗有明确胜负（或平局）
- **目的**：验证基础武器施放与伤害效果

## 2. 岩浆核心（Lava Core）

- **卡组**：P1 `sb_lava_core_p1`（岩浆核心），P2 `sb_lava_core_p2`（獠牙）
- **预期**：日志中出现「灼烧结算」
- **目的**：验证战斗开始触发灼烧

## 3. 驯化蜘蛛（Trained Spider）

- **卡组**：P1 `sb_spider_p1`（驯化蜘蛛），P2 `sb_spider_p2`（獠牙）
- **预期**：日志中出现「剧毒结算」
- **目的**：验证剧毒施加与结算

## 4. 举重手套（Lifting Gloves）

- **卡组**：P1 `sb_lifting_gloves_p1`（举重手套 + 獠牙），P2 `sb_lifting_gloves_p2`（獠牙）
- **预期**：日志中 P1 的獠牙造成 6 点伤害（基础 5 + 手套 1），即出现「獠牙」与「伤害 6」
- **目的**：验证武器伤害加成（同侧工具）

## 5. 符文手斧（Rune Axe）

- **卡组**：P1 `sb_rune_axe_p1`（符文手斧 铜），P2 `sb_rune_axe_p2`（獠牙）
- **预期**：日志中出现「符文手斧」与「伤害 15」
- **目的**：验证铜档 15 伤害

## 6. 放大镜（Magnifying Glass）

- **卡组**：P1 `sb_magnifying_glass_p1`（放大镜），P2 `sb_magnifying_glass_p2`（獠牙）
- **预期**：日志中出现「放大镜」与「伤害 5」
- **目的**：验证铜档伤害与工具标签

## 7. 古董剑（Old Sword）

- **卡组**：P1 `sb_old_sword_p1`（古董剑），P2 `sb_old_sword_p2`（獠牙）
- **预期**：日志中出现「古董剑」与「伤害 5」
- **目的**：验证铜档 5 伤害

## 8. 轻步靴（Agility Boots）

- **卡组**：P1 `sb_agility_boots_p1`（轻步靴 + 獠牙 相邻），P2 `sb_agility_boots_p2`（獠牙）
- **预期**：日志中出现「獠牙」施放与伤害；战斗正常结束
- **目的**：验证相邻暴击率光环不报错、战斗可完成

## 9. 利爪（Claws）

- **卡组**：P1 `sb_claws_p1`（利爪），P2 `sb_claws_p2`（獠牙）
- **预期**：日志中出现「利爪」与「伤害」（铜档 10，或暴击时更高）
- **目的**：验证伤害与暴击伤害光环

## 10. 蓝蕉（Bluenanas）

- **卡组**：P1 `sb_bluenanas_p1`（蓝蕉），P2 `sb_bluenanas_p2`（獠牙）
- **预期**：日志中出现「蓝蕉」与「治疗」
- **目的**：验证治疗效果

## 11. 冰锥（Icicle）

- **卡组**：P1 `sb_icicle_p1`（冰锥），P2 `sb_icicle_p2`（獠牙）
- **预期**：日志中出现「冻结」
- **目的**：验证战斗开始冻结敌人物品

## 12. 毒刺（Stinger）

- **卡组**：P1 `sb_stinger_p1`（毒刺），P2 `sb_stinger_p2`（獠牙）
- **预期**：日志中出现「毒刺」与「伤害」；以及「减速」
- **目的**：验证伤害与减速效果

## 13. 裂盾刀（Sunderer）

- **卡组**：P1 `sb_sunderer_p1`（裂盾刀），P2 `sb_sunderer_p2`（姜饼人）
- **预期**：日志中出现「裂盾刀」与「伤害」；以及「护盾」相关（减盾或姜饼人护盾）
- **目的**：验证对护盾物品的减盾效果或至少伤害与护盾逻辑存在

## 14. 姜饼人（Gingerbread Man）

- **卡组**：P1 `sb_gingerbread_p1`（姜饼人 + 放大镜），P2 `sb_gingerbread_p2`（獠牙）
- **预期**：日志中出现「姜饼人」与「护盾」；以及使用工具时「姜饼人」与「充能」
- **目的**：验证护盾与「使用工具时充能」触发

---

## 运行方式

- **CLI 单次运行**：`dotnet run --project src/BazaarArena.Cli -- Data/Decks/test_small_bronze.json <deck1_id> <deck2_id> --log Logs/test.log`
- **自动化**：在仓库根目录执行 `python scripts/run_item_tests.py`，会依次运行上述用例并检查日志与退出码。
- **CLI 与测试流程说明**：见 **docs/cli-and-testing.md**。
