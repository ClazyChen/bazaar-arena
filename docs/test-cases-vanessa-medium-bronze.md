# 海盗（Vanessa）中型铜物品测试用例

本文档描述针对「海盗中型铜」物品的自动化测试用例。每个用例由两方卡组（P1 vs P2）与预期行为（日志中出现的关键内容）组成。卡组定义见 `Data/Decks/test_medium_bronze.json`，卡组 ID 前缀为 `vanessa_mb_*`。

---

## 1. 渔网（Fishing Net）

- **卡组**：P1 `vanessa_mb_fishing_net_p1`（渔网 铜），P2 `vanessa_mb_fishing_net_p2`（獠牙 铜）
- **预期**：日志中出现「渔网」「减速」
- **目的**：验证减速敌人物品

## 2. 救生圈（Life Preserver）

- **卡组**：P1 `vanessa_mb_life_preserver_p1`（救生圈 铜），P2 `vanessa_mb_life_preserver_p2`（獠牙 铜）
- **预期**：日志中出现「救生圈」「护盾」
- **目的**：验证使用物品时获得护盾

## 3. 双管霰弹枪（Double Barrel）

- **卡组**：P1 `vanessa_mb_double_barrel_p1`（双管霰弹枪 铜），P2 `vanessa_mb_double_barrel_p2`（獠牙 铜）
- **预期**：日志中出现「双管霰弹枪」「伤害」，且「[双管霰弹枪] 伤害」至少出现 2 次（多重释放 2）
- **目的**：验证伤害与多重释放 2

## 4. 弯刀（Cutlass）

- **卡组**：P1 `vanessa_mb_cutlass_p1`（弯刀 铜），P2 `vanessa_mb_cutlass_p2`（獠牙 铜）
- **预期**：日志中出现「弯刀」「伤害」，且「[弯刀] 伤害」至少出现 2 次（多重释放 2）
- **目的**：验证伤害与多重释放 2、双倍暴击光环

## 5. 水桶（Barrel）

- **卡组**：P1 `vanessa_mb_barrel_p1`（水桶 铜 + 獠牙 铜），P2 `vanessa_mb_barrel_p2`（獠牙 铜）
- **预期**：日志中出现「水桶」「护盾」
- **目的**：验证获得护盾及使用相邻物品时护盾提高

## 6. 步枪（Rifle）

- **卡组**：P1 `vanessa_mb_rifle_p1`（步枪 铜），P2 `vanessa_mb_rifle_p2`（獠牙 铜）
- **预期**：日志中出现「步枪」「伤害」
- **目的**：验证伤害与使用后伤害提高（限本场）、弹药 1

## 7. 武士刀（Katana）

- **卡组**：P1 `vanessa_mb_katana_p1`（武士刀 铜），P2 `vanessa_mb_katana_p2`（獠牙 铜）
- **预期**：日志中出现「武士刀」「伤害」
- **目的**：验证伤害

## 8. 狼筅（Langxian）

- **卡组**：P1 `vanessa_mb_langxian_p1`（狼筅 铜），P2 `vanessa_mb_langxian_p2`（獠牙 铜）
- **预期**：日志中出现「狼筅」「伤害」
- **目的**：验证伤害与光环伤害提高

## 9. 沙滩充气球（Beach Ball）

- **卡组**：P1 `vanessa_mb_beach_ball_p1`（沙滩充气球 铜 + 救生圈 铜），P2 `vanessa_mb_beach_ball_p2`（獠牙 铜）
- **预期**：日志中出现「沙滩充气球」「加速」
- **目的**：验证加速水系或玩具物品

## 10. 钓鱼竿（Fishing Rod）

- **卡组**：P1 `vanessa_mb_fishing_rod_p1`（钓鱼竿 铜 + 救生圈 铜，救生圈在右），P2 `vanessa_mb_fishing_rod_p2`（獠牙 铜）
- **预期**：日志中出现「钓鱼竿」「加速」
- **目的**：验证加速此物品右侧水系物品

## 11. 铲子（Shovel）

- **卡组**：P1 `vanessa_mb_shovel_p1`（铲子 铜），P2 `vanessa_mb_shovel_p2`（獠牙 铜）
- **预期**：日志中出现「铲子」「伤害」
- **目的**：验证伤害

## 12. 星图（Star Chart）

- **卡组**：P1 `vanessa_mb_star_chart_p1`（星图 铜 + 獠牙 铜 相邻），P2 `vanessa_mb_star_chart_p2`（獠牙 铜）
- **预期**：日志中出现「獠牙」「伤害」
- **目的**：验证纯光环物品（相邻暴击率、冷却缩短）下相邻武器能正常造成伤害

## 13. 火炮（Cannon）

- **卡组**：P1 `vanessa_mb_cannon_p1`（火炮 铜），P2 `vanessa_mb_cannon_p2`（獠牙 铜）
- **预期**：日志中出现「火炮」「伤害」「灼烧」
- **目的**：验证伤害与等量于伤害 10% 的灼烧、弹药 2

## 14. 海底热泉（Volcanic Vents）

- **卡组**：P1 `vanessa_mb_volcanic_vents_p1`（海底热泉 铜），P2 `vanessa_mb_volcanic_vents_p2`（獠牙 铜）
- **预期**：日志中出现「海底热泉」「灼烧」，且「[海底热泉] 灼烧」至少出现 3 次（多重释放 3）
- **目的**：验证灼烧与多重释放 3

## 15. 湿件战服（Wetware）

- **卡组**：P1 `vanessa_mb_wetware_p1`（湿件战服 铜 + 獠牙 铜），P2 `vanessa_mb_wetware_p2`（獠牙 铜）
- **预期**：日志中出现「湿件战服」「护盾」
- **目的**：验证护盾与使用武器时护盾提高

## 16. 珊瑚护甲（Coral Armor）

- **卡组**：P1 `vanessa_mb_coral_armor_p1`（珊瑚护甲 铜），P2 `vanessa_mb_coral_armor_p2`（獠牙 铜）
- **预期**：日志中出现「珊瑚护甲」「护盾」
- **目的**：验证护盾（基础 50 + 光环 Custom_0×Custom_1）

## 17. 鲨齿爪（Shark Claws）

- **卡组**：P1 `vanessa_mb_shark_claws_p1`（鲨齿爪 铜），P2 `vanessa_mb_shark_claws_p2`（獠牙 铜）
- **预期**：日志中出现「鲨齿爪」「伤害」
- **目的**：验证伤害与己方武器伤害提高

---

## 运行方式

- **CLI 单次运行**：`dotnet run --project src/BazaarArena.Cli -- Data/Decks/test_medium_bronze.json <deck1_id> <deck2_id> --log Logs/item_tests/<用例名>.log`
- **自动化**：在仓库根目录执行 `python scripts/run_item_tests_vanessa_medium_bronze.py`，会批量运行上述用例并检查日志与退出码。
- **CLI 与测试流程说明**：见 **docs/cli-and-testing.md**。
