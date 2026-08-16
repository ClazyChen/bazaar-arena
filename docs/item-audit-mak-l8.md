# Mak L8 物品数据审计报告（2026-08）

> 范围：`data/items/mak.yaml` 全部 L8 可入池物品（131 件；不含 7 件局外排除件与 2 件 Diamond 起始件）。
> 方法：结构 lint（`out/exp/item_audit.py`）+ 四路并行人工复核（Desc 逐条对照 YAML/生成物）+
> 可疑项最小 job 实测（`bin/bazaararena_cli`，detailed 帧数据）。
> 先例：蛇怪之牙（光环无作用域 → 全场泄漏）、秘密配方（主动效果写成光环）已修复；
> 本报告是其后的全量普查结果。

## 1. 确认 bug（实测验证，**已修复** 2026-08，全链路已同步）

> 修复后回归：黏液链枷 poison 事件 0→9（量=自身 Damage）；青苔相邻生命导体 Regen 6→9、Damage 不变；
> 快速注射系统非相邻回血 9→0。验证 job 见文末附录。

### 1.1 黏液链枷：剧毒效果空转

- Desc：「▶ 造成 {Damage} 伤害；**▶ 造成剧毒，等量于此物品的伤害**；多重释放：{Multicast}」
- 实现（mak.yaml:2400-2412）：只有 `Damage` 能力；自我光环 `Poison = 自身Damage` 把剧毒写进了物品属性，
  但**没有任何 Poison 能力去施加它**——光环成了无出口的死值。
- 实测：黏液链枷单卡对沙包，117 次伤害事件、**0 次剧毒事件**、对手终局 poison=0。
- 修复：`Abilities` 增加 `- type: Poison`（默认 value_key=Poison，经光环读到自身 Damage，即"等量于此物品的伤害"）。

### 1.2 青苔：增益加错属性（Regen → Damage）

- Desc：「▶ 相邻生命再生物品的**生命再生**提高 {Custom_0}（限本场战斗）」
- 实现（mak.yaml:511-520）：`AddAttribute` **漏写 `key`**，codegen 缺省回退为 Damage
  （emit_cpp.py:318-322），生成物 `a.attribute_key = Damage`。
- 实测：青苔施放后相邻生命导体 **Damage 0→3，Regen 不变**。
- 修复：能力加 `key: Regen`。

### 1.3 快速注射系统：Regen 能力漏写「相邻」条件

- Desc：「对自己触发剧毒时，造成剧毒；**使用相邻物品时**，对自己造成剧毒；**使用相邻物品时**，获得生命再生」
- 实现（mak.yaml:1727-1731）：PoisonSelf 能力正确写了 `ex_condition: AdjacentToCaster`，
  **Regen 能力漏写**——合并基础条件后为 SameSide，己方任意物品使用都回血。
- 实测：己方唯一主动物品与快速注射系统不相邻，仍观察到 9 次 regen 事件（按 Desc 应为 0）。
- 修复：Regen 能力加 `ex_condition: AdjacentToCaster`。

## 2. 高度可疑项的处理结论（2026-08 用户裁决后）

### 2.1 黑冰（已修复：Desc 正确、实现错误）

- 原实现：Poison 能力带 `ex_condition: SameAsCaster`，仅自身冻结可触发——全库唯一过窄的"触发X时"。
- 修复：删除该条件（与衔尾蛇雕像/冰霜烈焰/药房等对齐为"己方任意冻结源"）。
- 实测：冰霜之怖（3s CD，先于黑冰自身 5s 施放）冻结敌方两件物品后，黑冰在 t=3050/3300 立即放毒两次 ✓。

### 2.2 空惧巨龙（已修复：Desc 正确、实现错误）

- 原实现：StartFlying 的"伤害提高"与 StopFlying 的"灼烧提高"累加进属性，但两个输出能力读的是
  Custom 定值——成长没有出口。
- 修复：输出能力改为读自身属性（Burn 能力读 Burn 属性、Damage 能力读 Damage 属性），并补基础字段
  `Burn: [10,20,30]`（= Custom_0 各档值，保证首次起飞灼烧与 Desc 一致；Damage 基础为 0，
  首次落地伤害 = Custom_1，与 Desc 一致）。
- 实测（gold）：灼烧 20→40→60→80（每次落地 +20）、伤害 200→400→600（每次起飞 +200）✓。

### 2.3 魂石（确认正常，无需修改）

- `Quest: 0` 且无 overridable 是基础形态的正确数据；任务进度由使用方注入——
  GDF 池用四变体特例，Web 前端通过编辑 Quest 实现变化。

## 3. Desc / 数据小问题（**已全部处理**，2026-08）

| 物品 | 问题 | 处理 |
|---|---|---|
| 贪婪渡鸦 | Desc 承诺「使用其他遗物**或附魔物品**时」，实现只有 Relic 分支（引擎无附魔概念） | ✅ Desc 已收窄为「使用其他遗物时」 |
| 和平铸箱 | 【默认】引用 Custom_0（局外累加值）但无 `overridable` | ✅ 已加 `overridable: [Custom_0]` |
| 飞行药水 | 「加速**其他**飞行物品」未排除自身 | ✅ Haste 目标已加 `DifferentFromCaster` |
| 研钵与研杵 | 加伤目标是 `Item[LifeSteal]`（任何吸血物品）而非 Desc 的「吸血武器」 | ✅ 目标已加 `HasTag(Weapon)` 限制 |

**遗留观察（codegen 层，不影响战斗）**：和平铸箱的 GainGold 能力 YAML 写 `value: Custom_1`，
但 `_resolve_value_key_cpp` 只对 AddAttribute/ReduceAttribute 映射 `value`，GainGold 回退默认 Custom_0——
金币是局外经济、战斗内无效果，故只记录在案；若后续有战斗内 GainGold 语义，需要扩展 codegen
（或 YAML 改用 `value_key` 字段显式指定）。

## 4. 已排除的误报（复核留档）

- **无敌药水 / 力量药水**：曾被怀疑"第二次施放窗口归零"（Sub(Add(Time,dur),Custom_x) 公式）。
  推导与实测（custom_0 覆写拉长窗口后，两次施放的无敌窗口均正确生效）证明**无 bug**——
  Custom_x 是累加器，公式自我纠正。
- **手术刀 / 魂戒 / 黏液链枷式自我光环**（光环写属性→能力读属性）：惯例本身正确；黏液链枷的问题在缺能力（§1.1）。
- **永恒火炬 / 生命导体 / 腐朽圣像**的任务 Multicast/CooldownReduction 光环：正确。
- **空白石碑 {ChargeSeconds}**：前端别名表（itemTooltip.ts）支持的合法占位符。
- **回收桶**：未实现，Desc 已自注明，属已知。
- **恶臭蘑菇** `condition: Always` 响应双方使用：与 Desc「任何物品」一致，有意为之。
- **重力之石**「其他物品」用 DifferentFromCaster：仓库惯例以 caster 为锚点，两种读法均可，不报。

## 5. 防再犯建议

三轮 bug（蛇怪之牙/秘密配方/本轮 3 件）都可用 lint 结构性捕获。建议把 `out/exp/item_audit.py`
的规则沉淀为 codegen 阶段的硬检查（或 CI lint）：

1. 光环 condition 必须含物品限定词元（蛇怪之牙型）；
2. `AddAttribute`/`ReduceAttribute` 能力显式写 `key`（青苔型——缺省回退 Damage 是静默陷阱）；
3. Desc 的每个 ▶ 分句必须有 Cast 能力、每个"…时"分句必须有对应触发器（秘密配方/黏液链枷型）；
4. 同一 Desc 内重复的限定短语（如"使用相邻物品时"出现两次）对应的能力应各带条件（快速注射系统型）。

### 附：验证用 job 与产物（out/，不入库）

- `out/exp/item_audit.py`：审计 lint。
- `out/exp/audit_qt/ks/wd*.json`：青苔 / 快速注射系统 / 无敌药水 三个最小验证 job 及输出。
- `out/exp/slime_job.json` / `slime_out.json` / `slime_fixed.json`：黏液链枷验证。
- `out/exp/audit_hb*.json`：黑冰跨物品冻结触发验证；`out/exp/audit_kj.json` / `audit_kj_out.json`：空惧巨龙成长验证。
