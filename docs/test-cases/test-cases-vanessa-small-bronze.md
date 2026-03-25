# 海盗（Vanessa）小型铜物品测试用例

本文档描述针对「海盗小型铜」物品的自动化测试用例。每个用例由两方卡组（P1 vs P2）与预期行为（日志中出现的关键内容）组成。卡组定义见 `Data/Decks/item_tests/test_small_bronze.json`，卡组 ID 前缀为 `vanessa_sb_*`。

---

## 1. 舱底蠕虫（Bilge Worm）

- **卡组**：P1 `vanessa_sb_bilge_worm_p1`（舱底蠕虫 铜），P2 `vanessa_sb_bilge_worm_p2`（獠牙 铜）
- **预期**：日志中出现「舱底蠕虫」「伤害」（敌人使用最左侧物品时触发）
- **目的**：验证敌人使用最左物品时造成伤害与吸血

## 2. 藏刃匕首（Concealed Dagger）

- **卡组**：P1 `vanessa_sb_concealed_dagger_p1`，P2 `vanessa_sb_concealed_dagger_p2`
- **预期**：日志中出现「藏刃匕首」「伤害」「加速」
- **目的**：验证伤害与加速 1 件物品

## 3. 食人鱼（Piranha）

- **卡组**：P1 `vanessa_sb_piranha_p1`，P2 `vanessa_sb_piranha_p2`
- **预期**：日志中出现「食人鱼」「伤害」
- **目的**：验证伤害与使用其他伙伴/食物时充能

## 4. 三花（Calico）

- **卡组**：P1 `vanessa_sb_calico_p1`，P2 `vanessa_sb_calico_p2`
- **预期**：日志中出现「三花」「伤害」
- **目的**：验证伤害与使用其他武器时暴击率提高

## 5. 淬锋钢（Honing Steel）

- **卡组**：P1 `vanessa_sb_honing_steel_p1`（淬锋钢 铜 + 獠牙 铜），P2 `vanessa_sb_honing_steel_p2`
- **预期**：日志中出现「淬锋钢」「提高」
- **目的**：验证己方最左/最右武器伤害提高

## 6. 独角鲸（Narwhal）

- **卡组**：P1 `vanessa_sb_narwhal_p1`，P2 `vanessa_sb_narwhal_p2`
- **预期**：日志中出现「独角鲸」「伤害」
- **目的**：验证伤害

## 7. 鱼饵（Chum）

- **卡组**：P1 `vanessa_sb_chum_p1`（鱼饵 铜 + 珍珠 铜），P2 `vanessa_sb_chum_p2`
- **预期**：日志中出现「鱼饵」「提高」
- **目的**：验证使用物品时水系暴击率提高

## 8. 珊瑚（Coral）

- **卡组**：P1 `vanessa_sb_coral_p1`，P2 `vanessa_sb_coral_p2`
- **预期**：日志中出现「珊瑚」「治疗」
- **目的**：验证治疗（基础 20 + 光环）

## 9. 迷幻蝠鲼（Illuso Ray）

- **卡组**：P1 `vanessa_sb_illuso_ray_p1`，P2 `vanessa_sb_illuso_ray_p2`
- **预期**：日志中出现「迷幻蝠鲼」「减速」
- **目的**：验证减速与相邻伙伴/射线多重释放

## 10. 打火机（Lighter）

- **卡组**：P1 `vanessa_sb_lighter_p1`，P2 `vanessa_sb_lighter_p2`
- **预期**：日志中出现「打火机」「灼烧」
- **目的**：验证灼烧

## 11. 手里剑（Shuriken）

- **卡组**：P1 `vanessa_sb_shuriken_p1`，P2 `vanessa_sb_shuriken_p2`
- **预期**：日志中出现「手里剑」「伤害」
- **目的**：验证伤害与弹药、多重释放光环

## 12. 刺刀（Bayonet）

- **卡组**：P1 `vanessa_sb_bayonet_p1`（獠牙 铜 左 + 刺刀 铜 右），P2 `vanessa_sb_bayonet_p2`
- **预期**：日志中出现「刺刀」「伤害」
- **目的**：验证使用左侧武器时造成伤害

## 13. 宠物石（Pet Rock）

- **卡组**：P1 `vanessa_sb_pet_rock_p1`，P2 `vanessa_sb_pet_rock_p2`
- **预期**：日志中出现「宠物石」「伤害」
- **目的**：验证伤害与唯一伙伴时暴击率光环

## 14. 左轮手枪（Revolver）

- **卡组**：P1 `vanessa_sb_revolver_p1`，P2 `vanessa_sb_revolver_p2`
- **预期**：日志中出现「左轮手枪」「伤害」
- **目的**：验证伤害与弹药 6 发

## 15. 手斧（Handaxe）

- **卡组**：P1 `vanessa_sb_handaxe_p1`，P2 `vanessa_sb_handaxe_p2`
- **预期**：日志中出现「手斧」「伤害」
- **目的**：验证伤害与己方武器伤害光环

## 16. 手雷（Grenade）

- **卡组**：P1 `vanessa_sb_grenade_p1`，P2 `vanessa_sb_grenade_p2`
- **预期**：日志中出现「手雷」「伤害」
- **目的**：验证伤害、弹药 1、暴击率

## 17. 抓钩（Grappling Hook）

- **卡组**：P1 `vanessa_sb_grappling_hook_p1`，P2 `vanessa_sb_grappling_hook_p2`
- **预期**：日志中出现「抓钩」「伤害」「减速」
- **目的**：验证伤害与减速

## 18. 水草（Seaweed）

- **卡组**：P1 `vanessa_sb_seaweed_p1`，P2 `vanessa_sb_seaweed_p2`
- **预期**：日志中出现「水草」「治疗」
- **目的**：验证治疗与使用水系时治疗提高

## 19. 流星索（Bolas）

- **卡组**：P1 `vanessa_sb_bolas_p1`，P2 `vanessa_sb_bolas_p2`
- **预期**：日志中出现「流星索」「伤害」「减速」
- **目的**：验证伤害、减速与弹药 2

## 20. 海螺壳（Sea Shell）

- **卡组**：P1 `vanessa_sb_sea_shell_p1`（海螺壳 铜 + 珍珠 铜），P2 `vanessa_sb_sea_shell_p2`
- **预期**：日志中出现「海螺壳」「护盾」
- **目的**：验证每件水系物品获得护盾

## 21. 燃烧响炮（Pop Snappers）

- **卡组**：P1 `vanessa_sb_pop_snappers_p1`，P2 `vanessa_sb_pop_snappers_p2`
- **预期**：日志中出现「燃烧响炮」「灼烧」
- **目的**：验证灼烧与弹药 4

## 22. 珍珠（Pearl）

- **卡组**：P1 `vanessa_sb_pearl_p1`（珍珠 铜 + 水草 铜），P2 `vanessa_sb_pearl_p2`
- **预期**：日志中出现「珍珠」「护盾」
- **目的**：验证护盾与使用其他水系时充能

## 23. 棉鳚（Zoarcid）

- **卡组**：P1 `vanessa_sb_zoarcid_p1`（獠牙 + 棉鳚 + 珍珠，棉鳚居中），P2 `vanessa_sb_zoarcid_p2`
- **预期**：日志中出现「棉鳚」「伤害」「加速」
- **目的**：验证伤害、加速相邻物品与触发灼烧时充能

## 24. 葡萄弹（Grapeshot）

- **卡组**：P1 `vanessa_sb_grapeshot_p1`，P2 `vanessa_sb_grapeshot_p2`
- **预期**：日志中出现「葡萄弹」「伤害」
- **目的**：验证伤害与弹药、使用其他弹药物品时装填

## 25. 迷你弯刀（Tiny Cutlass）

- **卡组**：P1 `vanessa_sb_tiny_cutlass_p1`，P2 `vanessa_sb_tiny_cutlass_p2`
- **预期**：日志中出现「迷你弯刀」「伤害」，且「[迷你弯刀] 伤害」至少 2 次（多重释放 2）
- **目的**：验证伤害、多重释放 2 与双倍暴击伤害

## 26. 靴里剑（Shoe Blade）

- **卡组**：P1 `vanessa_sb_shoe_blade_p1`，P2 `vanessa_sb_shoe_blade_p2`
- **预期**：日志中出现「靴里剑」「伤害」
- **目的**：验证伤害与首次使用暴击率 +100%

## 27. 龙涎香（Ambergris）

- **卡组**：P1 `vanessa_sb_ambergris_p1`，P2 `vanessa_sb_ambergris_p2`
- **预期**：日志中出现「龙涎香」「治疗」
- **目的**：验证治疗等量于价值倍数（光环）

## 28. 弹簧刀（Switchblade）

- **卡组**：P1 `vanessa_sb_switchblade_p1`（弹簧刀 铜 + 獠牙 铜），P2 `vanessa_sb_switchblade_p2`
- **预期**：日志中出现「弹簧刀」「伤害」
- **目的**：验证伤害与使用相邻武器时使其伤害提高

## 29. 水母（Jellyfish）

- **卡组**：P1 `vanessa_sb_jellyfish_p1`（水母 铜 + 珍珠 铜），P2 `vanessa_sb_jellyfish_p2`
- **预期**：日志中出现「水母」「剧毒」
- **目的**：验证剧毒与使用相邻水系时加速此物品

## 30. 火药角（Powder Horn）

- **卡组**：P1 `vanessa_sb_powder_horn_p1`（火药角 铜 + 左轮手枪 铜，左轮在右），P2 `vanessa_sb_powder_horn_p2`
- **预期**：日志中出现「火药角」「装填」
- **目的**：验证为此物品右侧物品装填弹药

## 31. 鹦鹉皮特（Pesky Pete）

- **卡组**：P1 `vanessa_sb_pesky_pete_p1`，P2 `vanessa_sb_pesky_pete_p2`
- **预期**：日志中出现「鹦鹉皮特」「灼烧」
- **目的**：验证灼烧与相邻伙伴/地产多重释放

## 32. 毒须鲶（Catfish）

- **卡组**：P1 `vanessa_sb_catfish_p1`（毒须鲶 铜 + 藏刃匕首 铜），P2 `vanessa_sb_catfish_p2`
- **预期**：日志中出现「毒须鲶」「剧毒」
- **目的**：验证剧毒与被加速时剧毒提高

## 33. 皮皮虾（Mantis Shrimp）

- **卡组**：P1 `vanessa_sb_mantis_shrimp_p1`，P2 `vanessa_sb_mantis_shrimp_p2`
- **预期**：日志中出现「皮皮虾」「伤害」「灼烧」
- **目的**：验证伤害、灼烧、弹药与触发减速时提高

## 34. 雪怪蟹（Yeti Crab）

- **卡组**：P1 `vanessa_sb_yeti_crab_p1`，P2 `vanessa_sb_yeti_crab_p2`
- **预期**：日志中出现「雪怪蟹」「冻结」
- **目的**：验证冻结与触发冻结时相邻剧毒物品剧毒提高

---

## 运行方式

- **CLI 单次运行**：`dotnet run --project src/BazaarArena.Cli -- Data/Decks/item_tests/test_small_bronze.json <deck1_id> <deck2_id> --log Logs/item_tests/<用例名>.log`
- **自动化**：在仓库根目录执行 `python scripts/item_tests/run_item_tests_vanessa_small_bronze.py`，会批量运行上述用例并检查日志与退出码。
- **CLI 与测试流程说明**：见 **docs/cli-and-testing.md**。
