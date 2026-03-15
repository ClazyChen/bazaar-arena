# 中型金/钻物品测试用例

本文档描述针对「中型金」「中型钻」物品的自动化测试用例。每个用例由两方卡组（P1 vs P2）与预期行为（日志中出现的关键内容）组成。卡组定义见 `Data/Decks/test_medium_gold_diamond.json`。脚本：`scripts/run_item_tests_medium_gold_diamond.py`。

---

## 中型金（Gold）

### 1. 祖特笛（Zoot Flute）

- **卡组**：P1 `mg_zoot_flute_p1`（祖特笛 金），P2 `mg_zoot_flute_p2`（獠牙 铜）
- **预期**：日志中出现「祖特笛」「减速」
- **目的**：验证减速 2 » 3 件物品、相邻暴击率光环、造成暴击时充能

### 2. 虚空射线（Void Ray）

- **卡组**：P1 `mg_void_ray_p1`（虚空射线 金），P2 `mg_void_ray_p2`（獠牙 铜）
- **预期**：日志中出现「虚空射线」「灼烧」
- **目的**：验证灼烧、多重释放 2、获得护盾时灼烧提高

### 3. 曲速引擎（Warp Drive）

- **卡组**：P1 `mg_warp_drive_p1`（曲速引擎 金 + 獠牙 铜），P2 `mg_warp_drive_p2`（獠牙 铜）
- **预期**：日志中出现「曲速引擎」「摧毁」「充能」（使用后摧毁此物品，触发为全队充能）
- **目的**：验证摧毁此物品、被摧毁时为己方所有物品充能

### 4. 生体融合臂（Biomerge Arm）

- **卡组**：P1 `mg_biomerge_arm_p1`（生体融合臂 金 + 分解射线 金），P2 `mg_biomerge_arm_p2`（獠牙 铜）
- **预期**：日志中出现「生体融合臂」「伤害」（己方弹药物品弹药耗尽时触发生体融合臂造成伤害）
- **说明**：生体融合臂放左侧、分解射线放右侧，避免光环给分解射线 +1 弹药。当前 AmmoDepleted（WithTemplateTag(Tag.Ammo) 且 AmmoRemaining==0）在 InvokeTrigger 求值未通过，用例未通过测试，根因待查。

### 5. 分解射线（Disintegration Ray）

- **卡组**：P1 `mg_disintegration_ray_p1`（分解射线 金），P2 `mg_disintegration_ray_p2`（獠牙 铜）
- **预期**：日志中出现「分解射线」「伤害」
- **目的**：验证伤害、弹药 3、耗尽时摧毁敌方（Highest）

### 6. 凡躯之缚（Mortal Coil）

- **卡组**：P1 `mg_mortal_coil_p1`（獠牙 铜 + 凡躯之缚 金，凡躯之缚右侧），P2 `mg_mortal_coil_p2`（獠牙 铜）
- **预期**：日志中出现「凡躯之缚」「伤害」
- **目的**：验证伤害、左侧武器吸血光环、自身吸血

---

## 中型钻（Diamond）

### 7. 锡箔帽（Tinfoil Hat）

- **卡组**：P1 `md_tinfoil_hat_p1`（锡箔帽 钻），P2 `md_tinfoil_hat_p2`（獠牙 铜）
- **预期**：日志中出现「锡箔帽」「护盾」（敌方使用物品时获得 1 护盾）
- **目的**：验证敌方使用物品时获得护盾

### 8. 虚空干扰器（Void Disruptor）

- **卡组**：P1 `md_void_disruptor_p1`（虚空干扰器 钻 + 獠牙 铜 相邻），P2 `md_void_disruptor_p2`（獠牙 铜）
- **预期**：日志中出现「虚空干扰器」「摧毁」「护盾」（摧毁相邻后触发护盾）
- **目的**：验证摧毁相邻物品、摧毁时获得护盾（最大生命 25%）

### 9. 虚空护盾（Void Shield）

- **卡组**：P1 `md_void_shield_p1`（虚空护盾 钻），P2 `md_void_shield_p2`（獠牙 铜）
- **预期**：日志中出现「虚空护盾」「护盾」
- **目的**：验证护盾等量于敌人灼烧、敌人使用物品时造成 1 灼烧

---

## 运行方式

- **自动化**：在仓库根目录执行 `python scripts/run_item_tests_medium_gold_diamond.py`，会依次运行上述 9 个用例并检查日志与退出码。
- **CLI 与测试流程说明**：见 **docs/cli-and-testing.md**。
