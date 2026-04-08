## Mak 物品表导入临时文档（20260408-142233）

### 原始来源

- 表格：`docs/item_sheets_csv/Mak.csv`
- 说明：
  - `【局外】` 表示局外效果；若仅影响附魔/转化等且不影响局内，则忽略。
  - Bazaar Arena 不考虑附魔，忽略附魔相关效果行（如“赋予XX附魔（如适用）”）。
  - **暂时不实现（仅占位，无效果）**：`Catalyst`、`Potion Potion`。
  - 分类：战利品=Loot，原料=Reagent，药水=Potion（将扩展 `Tag` 常量）。

### 物品清单（按英文名基名聚合）

#### 小型（Small）

- [ ] BarbedClaws（pending）
- [ ] RegenerationPotion（pending）
- [ ] NoxiousPotion（pending）
- [ ] SmellingSalts（pending）
- [ ] FloorSpike（pending）
- [ ] TazidianDagger（pending）
- [ ] RainbowPotion（pending）
- [ ] Scalpel（pending）
- [ ] LetterOpener（pending）
- [ ] SleepingPotion（pending）
- [ ] Venom（pending）
- [ ] VenomousDose（pending）
- [ ] CloudWisp（pending）
- [ ] Catalyst（pending，占位）
- [ ] Hemlock（pending）
- [ ] Venomander（pending）
- [ ] Myrrh（pending）
- [ ] FirePotion（pending）
- [ ] Incense（pending）
- [ ] Orly（pending）
- [ ] BottledLightning（pending）
- [ ] FungalSpores（pending）
- [ ] Sulphur（pending）
- [ ] BrokenBottle（pending）
- [ ] IonizedLightning（pending）
- [ ] QuillAndInk（pending）
- [ ] Ruby（pending）
- [ ] Emerald（pending）
- [ ] Fireflies（pending）
- [ ] BasiliskFang（pending）
- [ ] PhilosophersStone（pending）
- [ ] Moss（pending）
- [ ] Mothmeal（pending）
- [ ] Thurible（pending）
- [ ] CrocodileTears（pending）
- [ ] ShardOfObsidian（pending）
- [ ] BlackRose（pending）

#### 中型（Medium）

- [ ] PotionPotion（pending，占位）
- [ ] SwordCane（pending）
- [ ] Peacewrought（pending）
- [ ] Cellar（pending）
- [ ] ShowGlobe（pending）
- [ ] Refractor（pending）
- [ ] SandsOfTime（pending，含任务）
- [ ] Retort（pending，含局外默认变量）
- [ ] Leeches（pending）
- [ ] EternalTorch（pending，含任务）
- [ ] Aludel（pending）
- [ ] Calcinator（pending，含局外默认变量）
- [ ] LifeConduit（pending，含任务）
- [ ] MortarAndPestle（pending）
- [ ] BlankSlate（pending，含任务）
- [ ] IdolOfDecay（pending，含任务）
- [ ] Candles（pending）
- [ ] CovetousRaven（pending，忽略附魔相关触发）
- [ ] Nightshade（pending）
- [ ] MagicCarpet（pending）

### 待确认/实现风险点

- **任务系统（Q1..）**：将实现 `Key.Quest` bitmap，并按 `【默认】任务完成情况` 把“完成数量”映射成 “前 k 个任务完成掩码”（0/1/2/3 → 0/1/3/7；0/1/3/5 → 0/1/7/31）。\n+  - 对每个 `【Qk】...` 行：用 `additionalCondition` 检查对应 bit 是否为 1。\n+- **“转化/出售/拜访商人/每天开始”**：属于局外流程，本次只在需要影响局内数值时通过 `OverridableAttributes` 约束默认值；否则忽略。\n+- **“转化为 2 件来自任意英雄的小型药水（限本场战斗）”**（Potion Potion）：按约定本次占位，不实现。\n+
