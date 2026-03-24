# 实现笔记与错误经验

本文档记录开发过程中容易踩坑的实现细节与修正经验，便于后续维护与代码审查时对照。

---

## 物品 Tags 位掩码迁移经验（2026-03-24）

### 背景与问题

- 物品定义中曾大量使用 `Tags = [Tag.Weapon, Tag.Tool]` 这类写法。
- 在当前 `ItemTemplate.Tags` 为 `IntOrByTier` 的模型下，数组语义是“按档位数值列表”，不是“标签并集”。这会导致铜档只读到第一个 tag，触发条件（如 `WithTag(Tag.Tool)`）失效。
- 典型现象是：日志里有施放与主效果，但依赖标签的联动效果不触发（如姜饼人的“使用工具时充能”）。

### 统一约定

- 物品 `Tags` 必须使用**单值位掩码**写法：`Tags = Tag.A | Tag.B | Tag.C`。
- 空标签写法统一为：`Tags = 0`。
- 禁止在物品定义中继续使用 `Tags = [ ... ]`，因为这会被解释为按档位列表而不是标签并集。

### 批量修复与验证

- 已对 `ItemDatabase` 下物品定义完成批量迁移：`Tags = [ ... ]` → `Tags = Tag... | Tag...`，`Tags = []` → `Tags = 0`。
- 回归：`python scripts/item_tests/run_item_tests_small_bronze.py` 全部通过（14/14）。

---

## Core/Sim 收敛补记（2026-03-24）

### 本轮化简落地

- 删除过渡类型：`BattleSimulator/AbilityQueueEntry.cs`。
- `BattleState` 删除反向执行字段，改为 `InvokeTrigger(..., executeImmediate)` 显式下传 Immediate 执行器。
- 能力目标队列由 `List + RemoveAt(0)` 改为 `Queue + Dequeue`（`AbilityState.InvokeTargets`）。
- `BattleSimulator` 抽取 `DrainCurrentAbilityBuckets(...)`，合并步骤 8 与 AboutToLose 后 drain 的重复逻辑。
- `BattleState` 增加 `SwapAbilityBuckets()`，对调 current/next 的写入口收敛到状态对象内部。

### 触发条件上下文修正（关键）

- `InvokeTrigger` 中 **TriggerEntry.Condition** 的求值上下文应使用：
  - `Caster` = 能力持有者；
  - `Source` = 引起触发者（`causeItem`，无则能力持有者）；
  - `Item` = 引起触发者（`causeItem`，无则能力持有者）；
  - `InvokeTarget` = 触发器指向目标（若有）。
- 若误把 `Item` 固定为能力持有者，会导致 `UseOtherItem` 下如 `LeftOfCaster/WithTag(...)` 等条件在错误对象上求值，出现“有施放但不触发效果”。

### PoisonSelf 最终约定

- `Ability.PoisonSelf` 为**独立能力语义**，不再视为过渡别名：
  - 执行入口：`Apply.PoisonSelf`；
  - 作用对象：对 `Caster` 所在侧施加剧毒（`AddPoisonToCaster`）；
  - 触发上报：与 `Poison` 一致，上报 `Trigger.Poison`。
- `PoisonSelf` 与 `Poison` 的核心差异是**施加目标**（自身 vs 对方），而非仅靠条件覆盖表达。

### 同步说明

- `BattleAuraModifiers.cs` 已删除（无运行时引用）；后续若恢复 Aura 累加入口，应直接接到 `GetItemInt`/`GetResolvedValue` 主路径，不再回引独立兼容层。

---

## ExecuteOneEffect 移除与执行入口收敛（2026-03-24）

### 目标

- 移除 `BattleSimulator.ExecuteOneEffect`，避免每次执行能力都构造局部 `BattleState`。
- 统一在主战斗状态上直接执行 `ability.Apply(ctx, ability)`。
- 明确 `CastQueue` 生命周期：每帧内使用、帧末强制清空，不跨帧携带。

### 最终约定

- 能力执行入口统一为主循环内的 `RunAbilityApply`（局部函数），由它负责：
  - 通过 `abilityId` 获取 owner 与 ability；
  - 组装 `BattleContext`（`BattleState/Item/Caster/Source/InvokeTarget`）；
  - 直接调用 `ability.Apply(ctx, ability)`。
- `BattleContext` 新增 `AllowCastQueueEnqueue` 开关；`Charge/Reload/ReduceCooldown` 等路径写 `BattleState.CastQueue` 前统一判断该开关。
- AboutToLose 阶段执行能力时 `AllowCastQueueEnqueue=false`，其“下一帧处理”仅依赖能力队列（`current/next`）而非 cast 跨帧。
- 主循环在步骤 10（对调能力桶）后执行 `battleState.CastQueue.Clear()`，确保帧末无残留。

---

## invokeCount 统一语义与 Destroy 多目标触发（2026-03-24）

### 最终约定

- `BattleState.InvokeTrigger(...)` 中 `invokeCount` 统一表示触发次数，`pendingCount = invokeCount ?? 1`（并做非负保护）。
- `InvokeTriggerMany(...)` 语义保持“每目标一次”，调用方传入 `invokeTargetItems.Count`。
- `BattleStart` 等非多播触发统一显式传 `1`，不再使用 `0` 这种依赖历史分支兜底的写法。

### Destroy 时序

- `ApplyDestroy` 先收集目标列表，再对该列表调用 `InvokeTriggerMany(Trigger.Destroy, ...)`。
- 全部 Destroy 触发完成后，才逐个将目标标记 `Destroyed = true`。
- 这保证了 Destroy 条件评估与触发日志发生在目标被标记摧毁之前，行为与 Freeze/Slow 的多目标触发路径保持一致。

---

## 触发器语义与 InvokeTrigger 重构（2026-03-24）

### 最终约定

- `Trigger.UseItem` 仅表示“使用自己”。
- `Trigger.UseOtherItem` 表示“其他物品被使用”，默认条件为 `SameSide & DifferentFromCaster`，可 override 到 `DifferentSide` 等语义。
- 同一次使用事件中，触发顺序固定为：先 `UseItem`，后 `UseOtherItem`。
- `InvokeTrigger` 不再按「causeItem/同侧/异侧」分层遍历，统一按 `SessionTables.AbilitiesByTrigger[triggerName]` 直接遍历。

### 迁移说明

- `AbilityDefinition.UseSelf` 已移除，不再用该字段区分“使用自己/使用其他”。
- 物品定义中“当其他物品被使用时”统一改用 `Trigger.UseOtherItem` 表达。
- 暴击可判定能力条件收敛为：能力含 `Trigger.UseItem` 且 `ApplyCritMultiplier=true`。

---

## ValueKey 与属性读取路径收敛（2026-03-24）

### 目标

- 移除 `BattleContext.GetResolvedValue`，统一由 `GetItemInt` 读取战斗内属性。
- 运行时不再保留 `fallbackValue` 与 `AbilityDefinition.Value` 双轨兜底。
- 暴击倍率仅在 `Apply` 的对应能力分支显式应用。

### 最终约定

- **能力主数值必须走 `ValueKey`**：`Apply` 内统一读取 `ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value)`。
- **`AbilityDefinition.Value` 已删除**：`Override` 不再接受 `value` 参数，常量值请迁移为模板属性（通常 `Custom_x`）并用 `valueKey` 指向。
- **默认值前置到注册阶段**：`ItemDatabase.EnsureDefaultAttributes` 负责补齐模板默认值；其中 `ModifyAttributeTargetCount` 默认值已调整为 `20`。
- **目标数量默认值**：`Charge/Haste/Slow/Freeze/Reload/Repair/Destroy` 等 `*TargetCount` 读取为 0 时在 `Apply` 侧按 1 处理，防止历史模板缺省导致无目标。
- **暴击处理**：`Damage/Burn/Poison/Shield/Heal`（以及 `Add/Reduce` 在 `ApplyCritMultiplier=true` 时）在 `Apply` 分支内显式乘 `ctx.CurrentCritMultiplier`；不再通过统一读取函数隐式处理。

### 迁移经验

- `StartFlying/StopFlying` 这类“实际不依赖数值”的能力，为保持 `ValueKey` 一致性可指定 `Key.Custom_0`，但效果语义仍由对应 `Apply` 分支控制（如 StopFlying 直接置 0）。
- 当去掉运行时 fallback 后，所有能力定义必须保证 `ValueKey` 可用；新增能力时若漏配 `ValueKey`，应尽早在测试中暴露而非默默回退。

### 回归记录

- 迁移后 `dotnet build` 通过。
- 小型铜脚本 `姜饼人_gingerbread` 的“未出现充能日志”问题已修复：根因是 `Tags` 被按档位列表写法误用，导致 `Tag.Tool` 条件未命中；改为位掩码后回归通过（不放松断言）。

---

## AuraDefinition 与 BattleContext 复用经验（2026-03-24）

### 1) AuraDefinition 收敛：移除 SourceCondition 与 GrantedTags

- **结论**：`AuraDefinition` 仅保留 `Attribute`、`Condition`、`Value`、`Percent`，不再维护 `SourceCondition` 与 `GrantedTags` 两套分支语义。
- **原因**：
  - `SourceCondition` 与 `Condition` 同时存在时，调用方需维护两阶段上下文，易出现漏判与字段污染；
  - `GrantedTags` 与数值光环是并行机制，维护成本高且重复。
- **迁移规则**：
  - 原 `SourceCondition = X` 迁移为 `Condition = Condition.SameAsSource & X`（或按原有目标条件与 `X` 组合）。
  - 原 `GrantedTags = [Tag.A, Tag.B]` 迁移为 `Attribute = Key.Tags`、`Value = Formula.Constant(Tag.A | Tag.B)`、`Percent = false`。

### 2) Key.Tags 光环按位或（|）而非数值加法

- **约定**：当 `aura.Attribute == Key.Tags` 时，`Value` 表示**标签掩码**，叠加方式必须是按位或 `|`。
- **实现**：`BattleContext.GetItemInt(item, Key.Tags)` 走标签专用分支：基础标签掩码与每条命中光环 `Value` 做 OR 合并；`Percent` 分支不参与标签光环。
- **边界**：`DerivedTags` 不参与该语义；自动推导标签仍由注册阶段与能力/机制规则生成。

### 3) BattleContext 复用：先对齐语义，再减少 new

- **热点**：`Formula.Count`、`BattleContext.GetItemInt`（aura 循环）、`BattleSimulator.InvokeTrigger` 条件判定。
- **复用原则**：
  - 仅复用对象，不复用语义状态；
  - 每次评估前必须显式覆写 `Item`、`Source`、`InvokeTarget`（必要时 `Caster`）；
  - 涉及 `SameSide`/`DifferentSide`/`SameAsInvokeTarget` 时，不能只改 `Item`。
- **本轮落地**：
  - `Formula.Count` 使用单实例 `BattleContext` 循环复写；
  - `GetItemInt` 在 aura 循环内复用局部 `auraCtx`；
  - `InvokeTrigger` 条件判定接入 `BattleSimulatorThreadScratch` 的 thread-static `BattleContext/BattleState`。

### 4) BattleContext 执行路径：移除 Rebind

- **结论**：`ExecuteOneEffect` 不再调用 `ctx.Rebind(...)`，改为直接写入最小字段（`BattleState/Item/Caster/Source/InvokeTarget`）。
- **收益**：减少参数散传与对象状态分叉，便于后续继续做热路径减分配。

---

## Tag/DerivedTag、Apply 统一入口与模板复用经验（2026-03-24）

### 1) Tag 与 DerivedTag 必须分离位域

- **结论**：普通标签与自动推导标签不能混在同一常量表中连续占位，否则后续扩展很快逼近 `int` 位宽上限并产生冲突风险。
- **约定**：
  - 普通标签（显式定义 + 尺寸 `Small/Medium/Large`）放 **`Tag`**，写入 **`Key.Tags`**。
  - 自动推导标签（`Damage/Shield/Ammo/Burn/Poison/Heal/Regen/Crit/Cooldown/...`）放 **`DerivedTag`**，写入 **`Key.DerivedTags`**。
  - `DerivedTag` 从 **`1 << 0`** 重新编号，独立于 `Tag`。
- **条件判定**：`Condition.WithTag` 与 `Condition.WithDerivedTag` 已按语义拆分：前者只检查 `Key.Tags`，后者只检查 `Key.DerivedTags`；两者均通过 `GetItemInt` 读取以感知光环带来的动态标签变化。

### 2) Apply 作为能力执行入口，效果逻辑挂在 BattleContext 上

- **结论**：`AbilityDefinition.Apply` 类型为 **`Action<BattleContext>?`**；**`Core/Apply.cs`** 内各委托**直接使用传入的 `BattleContext` 参数**（`ctx.GetResolvedValue`、`ctx.ApplyFreeze` 等），不再经 `IEffectApplyContext` 或已删除的 **`Core/Effect.cs`**。`Apply` 为空会导致能力“入队但不生效”。
- **落地方式**：
  - **`ExecuteOneEffect`**：`BattleSimulatorThreadScratch` 按嵌套深度复用 **`BattleContext` + `BattleState`**；写入 `side0/side1/timeMs` 后 **`ctx.Rebind(...)`**，再 **`ability.Apply(ctx)`**。
  - **多目标效果、伤害/护盾/触发队列**等实现位于 **`Core/BattleContext.EffectApply.cs`**（`partial class BattleContext`），与公式求值用的 `BattleContext`（`Item`/`Caster`/`Source`/`InvokeTarget`/`BattleState`）一致。
  - **`AttributeLogNames`** 独立为 **`Core/AttributeLogNames.cs`**。
- **效果**：能力执行与上下文对象合一，避免“Apply 忽略参数、仅从线程取另一套上下文”的分叉。

### 3) 模板不再战斗内克隆，运行时状态统一写 ItemState

- **结论**：数据库模板按引用复用；战斗实例化统一 `new ItemState(template, tier)`，卡组覆盖仅写入 `ItemState.Attributes`。不再创建 battle template clone。
- **关键点**：
  - `BuildSide`：resolver 取模板后直接构造 `ItemState(template, tier)`，再应用 slot overrides（仅允许 `OverridableAttributes` 声明的 key）。
  - 战斗内属性读写（伤害、冷却、暴击率、弹药等）优先读写 `ItemState`，避免改写共享模板。
  - `BattleSide.GetItemInt` 优先读 `ItemState` 对应 key，保证运行时变更即时生效。

### 4) 能力队列 250ms 节流存储与其它清理

- **`ItemState` 上「每能力上次触发时间」**：用 **`int[Template.Abilities.Count]`**，初值 **`int.MinValue`**，下标与 **`AbilityIndex`** 一致；较 **`Dictionary<int,int>`** 更省分配、语义更直。
- **光环接口**：删除 **`IAuraContext`**；累加逻辑为 **`BattleAuraModifiers.Accumulate`**（静态方法）。与 **`ItemTemplate.GetInt`+context** 的接线待 **`GetItemInt`/`GetResolvedValue`** 统一合并时再补。
- **`TimeUtil`**：全库无引用，已删除。

### 验证记录

- **`dotnet build`**（解决方案）通过。
- 物品测试：**`run_item_tests_small_bronze.py`** 曾出现大量日志断言失败（与效果/日志路径相关时）；以脚本与 **`Logs/item_tests/`** 为准持续回归，**不放松断言**。

---

## Ability/Aura 硬切收敛经验（2026-03-24）

### 关键收敛路径

- **先收敛 P0 编译入口，再扩散到调用侧**：优先处理 `ItemDatabase/ItemDatabase.cs`、`Formula` 缺失 API、`AuraDefinition.AttributeName` 残留，能快速把错误簇从 Core/ItemDatabase 缩到少数模块。
- **减少物品定义改动面**：对历史 API（如 `Formula.Opp/Side/SideSelect`）优先补兼容入口，再逐步迁移调用点，避免一次性改动大量物品文件引入新偏差。
- **类型收敛优先于语义优化**：先统一为 `int key + int tag bitmask + TriggerEntries`，待编译稳定后再处理行为优化（如日志、性能、上下文复用）。

### 本轮高频易错点

- **`invokeTargetCondition` 已删除**：调用点需迁移到 `condition` / `additionalCondition`（触发条件）或 `additionalTargetCondition`（效果目标条件），不可继续透传旧参数名。
- **`OverridableAttributes` 已是 `Dictionary<int, IntOrByTier>`**：GUI/Deck 序列化仍用 `Dictionary<string,int>` 时，需要通过 `Key.GetName(int)` / `Key.TryGetKey(string, out int)` 做桥接，不要回退成 string key 主路径。
- **`MainWindow` 中 `Key` 名称冲突**：存在 `System.Windows.Input.Key` 时，引用核心 key 需显式写 `BazaarArena.Core.Key`，避免 CS0104 二义性。
- **`BattleContext` 在效果路径上须带有效 `BattleState`（Side0/Side1/TimeMs）**：否则 **`Formula.Side` / `Formula.Opp` / `Formula.Count`** 在目标筛选或光环公式中求值时失真；**`BattleAuraModifiers`** 内为 Aura 公式构造子 `BattleContext` 时同样要赋 **`BattleState`**。

### 验证节奏

- 每个阶段完成后固定执行：`dotnet build src/BazaarArena/BazaarArena.csproj`。
- 中间允许失败，但要保证**错误总量单调下降**且模块聚焦；本轮收敛轨迹：33 → 23 → 19 → 18 → 11 → 3 → 0。
- 最终目标不仅是“可编译”，还要做到 **0 warning / 0 error**，避免把可空性与未使用参数债务留到下一窗口。

---

## 对战模拟器：能力队列的帧边界

### 规则要求

设计文档规定：每一帧先「对于每个物品，如果满足条件，调用使用物品的触发器」（即本帧施放进入队列），再「按照次序结算**上一帧留下的**能力效果」。因此本帧施放产生的能力应在**下一帧**才被处理。

### 错误实现

在步骤（8）处理能力队列时，若在**处理前**就把 `nextAbilityQueue` 拷贝到 `currentAbilityQueue` 并清空 `nextAbilityQueue`，会导致：

- 步骤（7）本帧刚加入的能力立刻进入 `currentAbilityQueue`；
- 同一帧内就被步骤（8）的循环处理掉，相当于「本帧施放、本帧生效」，与「上一帧留下的能力效果」不符。

### 正确做法

- **步骤（8）**：只遍历并处理**当前帧**的 `currentAbilityQueue`（即上一帧末已经移入的条目）。
- **步骤（9）**：步骤（8）结算完成后，将 `nextAbilityQueue` 整体移入 `currentAbilityQueue`，清空 `nextAbilityQueue`，供下一轮循环使用。

这样步骤（7）本帧加入的条目会留在 `nextAbilityQueue`，直到本帧结束才在步骤（9）被移入 `currentAbilityQueue`，在下一帧的步骤（8）才被处理。

### 小结

双队列（current / next）时，「下一帧队列 → 当前帧队列」的移动必须发生在**本帧能力全部处理完毕之后**，不能发生在步骤（8）开头，否则会破坏帧边界语义。

### 常见错误与教训（本轮修正）

1. **castQueue 触发的「使用物品」能力必须下一帧处理**  
   步骤 7 中，施放队列产生的能力仅通过一次 `InvokeTrigger(Trigger.UseItem, ...)` 加入 **nextAbilityQueue**，不能加入 currentAbilityQueue。「其他物品使用则触发」类能力用 `Trigger.UseItem` + `Condition = And(DifferentFromSource, SameSide)[ + 额外条件 ]` 表达。若误入 current，本帧步骤 8 就会处理，变成「本帧施放、本帧生效」，违反「上一帧留下的能力在本帧结算」的约定。

2. **步骤 8 只处理 currentAbilityQueue**  
   步骤 8 遍历、消耗的必须是 **currentAbilityQueue**（拷贝后清空 current，再逐条处理）；未到 250ms 或 PendingCount 未用完的写回 **nextAbilityQueue**。**只有步骤 9** 才能把 nextAbilityQueue 移入 currentAbilityQueue；在步骤 8 开头或循环前做「next → current」都是错误的。

3. **步骤 8 执行过程中引发的新能力**  
   ExecuteOneEffect 执行效果时可能触发新能力（如后续扩展的触发器）。这些新能力入队规则：**仅优先级为 Immediate 的加入 currentAbilityQueue**（本帧继续被步骤 8 处理），**其余一律加入 nextAbilityQueue**。因此 ExecuteOneEffect 需要接收 current/next 两个队列参数，供未来触发器调用使用。

4. **触发器调用方式与 PendingCount 合并**  
   - 所有触发器通过**统一**的 `InvokeTrigger(triggerName, causeItem?, context, ...)` 调用（causeItem 为引起触发的物品引用，BattleStart 传 null），遍历双方所有物品，构建 `ConditionContext`（Source=能力持有者、Item=causeItem）后调用 `condition.Evaluate(ctx)`；若能力有 `InvokeTargetCondition` 且 context 提供 `InvokeTargetItem`，则再以该目标为 Item 求值。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。  
   - **PendingCount 为通用机制**：新能力入队时，**先查 currentAbilityQueue** 是否存在同 (Owner, AbilityIndex) 且无 InvokeTarget 的条目，有则只加 PendingCount；**再查 nextAbilityQueue** 同上；**都没有**才新建条目。当 context 提供 InvokeTargetItem 时**不合并**，直接新建条目（该条目带 InvokeTargetSideIndex/InvokeTargetItemIndex）。250ms 节流状态存于物品（GetLastTriggerMs/SetLastTriggerMs），见「能力队列 250ms 节流状态与冷却缩短联动」。

5. **总结**  
   修改能力队列逻辑时务必对照：cast/使用物品 → 只入 next；步骤 8 只消费 current；next → current 仅步骤 9；新触发能力先合并 PendingCount（current 再 next），再按优先级决定入 current 或 next。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。

---

## AbilityDefinition 条件统一化与 UseOtherItem 移除

### 三种条件的语义

- **condition**：引起触发的物品（ConditionContext.Item，如「被使用的物品」）需满足的条件。评估时 Source=能力持有者、Item=引起触发的物品。默认：UseItem → SameAsSource，其他触发器（Freeze/Slow/Crit/Destroy/BattleStart）→ SameSide。
- **InvokeTargetCondition**：触发器所指向的物品需满足的条件（如 Slow 时「被减速的物品」、Freeze 时「被冻结的物品」）。在 `InvokeTrigger` 中，当 context 提供 InvokeTarget（Slow/Freeze 按每个目标调用一次）时，以该目标为 Candidate 求值，不通过则不入队。默认 null 表示不限制。
- **TargetCondition**：能力效果选目标时，目标需满足的条件（充能/加速/减速/冻结/修复等）。效果执行阶段使用，逻辑不变。

### 移除 Trigger.UseOtherItem

「使用其他物品时触发」等价于「使用物品」且 condition 限制为「己方、且非来源物品」：`Condition.And(Condition.DifferentFromSource, Condition.SameSide)`，再与显式条件（如 WithTag(Tag.Tool)、LeftOfSource）取与。因此删除 `Trigger.UseOtherItem`，步骤 7 仅调用一次 `InvokeTrigger(Trigger.UseItem, ...)`；原 UseOtherItem 能力改为 `Trigger.UseItem` + 上述 condition。物品迁移示例：神经毒素、断裂镣铐、姜饼人、暗影斗篷。

### Ability 工厂参数（已由 Ability / Key / Override 重构替代）

当前能力写法见下节「Ability / Key / Override 重构」；Condition/Trigger 语义不变，仅 API 改为无参属性 + Override。

### 效果施加触发器统一（EffectAppliedTriggerQueue）

冻结/减速/摧毁等「施加效果时触发」不再通过上下文注入多个回调（OnFreezeApplied / OnSlowApplied / OnDestroyApplied），改为**待处理队列**：模拟器传入 `List<(TriggerName, SideIndex, ItemIndex)>`（由 `BattleSimulatorThreadScratch` 复用列表），**`BattleContext`** 在 `ApplyFreeze`/`ApplySlow`/`ApplyDestroy` 内只追加条目，不持有委托。`ability.Apply(ctx)` 返回后，模拟器**统一**遍历该列表并 `InvokeTrigger`；**Destroy** 在触发后再设 **`target.Destroyed = true`**。扩展新「施加时触发」只需在 **`BattleContext`** 侧写队列、模拟器多处理一种 **triggerName**。详见「BattleContext 与施加触发器队列」节。

### 小结

修改能力或触发器逻辑时：condition 管「谁触发」，InvokeTargetCondition 管「触发器目标是否满足」（仅 Slow/Freeze 等有目标时），TargetCondition 管「效果选谁」。不再使用 UseOtherItem；「其他物品使用则触发」一律用 UseItem + condition。

### AbilityDefinition 多触发与 Ability.Also

- **多套触发配置**：一条 AbilityDefinition 可以拥有多套 `(TriggerName, Condition, SourceCondition, InvokeTargetCondition)` 组合，内部通过 `AbilityDefinition.TriggerEntry` 列表（`Triggers`）表示；同一条能力仍只有一份 `Priority` / `TargetCondition` / `ValueKey` / `Value` / `ApplyCritMultiplier` / `UseSelf` / `Apply`，以及一套队列节流状态（`LastTriggerMs` / PendingCount 等）。
- **共享 250ms 触发间隔**：无论由哪一套 Trigger/Condition 命中，最终都只向队列中加入同一条 ability（同一个 `AbilityIndex`）的 Pending，一并受「每 5 帧（250ms）最多触发一次」的限制，即「同一条能力的多套触发条件**共享**节流，不会互相绕过冷却」。节流状态存于物品（GetLastTriggerMs/SetLastTriggerMs），见「能力队列 250ms 节流状态与冷却缩短联动」。
- **顶层字段与主触发条目**：`TriggerName` / `Condition` / `SourceCondition` / `InvokeTargetCondition` 仍作为「主触发条目」存在，`AbilityDefinition.Override(...)` 修改这些字段时会同步更新 `Triggers[0]`；旧代码在不显式使用 `Triggers` 时行为保持不变。
- **AbilityDefinition.Also(...)**：在已构造好的能力上追加一套新的触发条件：
  - 签名为 `ability.Also(trigger, condition?, additionalCondition?, sourceCondition?, invokeTargetCondition?)`，返回 `this` 以便链式调用。
  - `trigger` 的默认 Condition 与 `EnsureTriggerCondition` 一致：UseItem → SameAsSource，Freeze/Slow/Haste/Crit/Destroy/Burn/Poison/Shield/Ammo/AboutToLose → SameSide，BattleStart → Always；若传入 `condition`/`additionalCondition` 则在此默认上做与运算。
  - `sourceCondition` / `invokeTargetCondition` 为空时沿用当前 ability 顶层对应字段。
  - `InvokeTrigger` 评估时会遍历该 ability 的所有 TriggerEntry，只要有一条条目匹配当前 triggerName 且通过条件，即视为这条 ability 命中一次并入队。
- **使用场景与约定**：
  - 当**同一条效果语义**需要在多个触发器或条件下生效、且数值/目标/优先级完全一致、需要共享 250ms 节流时，优先使用一条能力 + 多次 `Also(...)`（避免复制多条几乎相同的能力定义）。
  - 当不同触发下的效果语义或数值明显不同（例如「战斗开始加盾」和「使用时造成伤害」），仍然使用多条独立 AbilityDefinition，而不是混在一条里用多套触发条件。
  - 与对齐当前游戏版本的正式物品定义保持一致时，**不要为了使用 Also 调整原有物品行为**；如需验证多触发逻辑，应在独立的测试物品中尝试，见「物品与测试物品的约定」一节。

### 即将落败（AboutToLose）与「首次」由物品 Custom_0 保证

- **触发器**：`Trigger.AboutToLose`（即将落败）在步骤 10 胜负判定前触发；默认 Condition 为 `SameSide`（仅该方物品能力生效）。参考靴里剑：「首次」**不在触发器名中写 First**，用**物品状态**（Custom_0）区分是否已触发。
- **「首次」**：与靴里剑一致，用 **Condition.SourceCustom0IsZero** + 生效后 **AddAttribute(Key.Custom_0).Override(..., value: 1, effectLogName: "")** 置 1，每件物品每场最多生效一次。步骤 10 中当某方 `Hp <= 0` 即对该方调用 `InvokeTrigger(Trigger.AboutToLose, null, null, ..., onlyForSideIndex: 该侧)`；仅 **Immediate** 能力入队并在本帧内执行（如救生圈治疗），执行完后重算 Hp 再判胜负。
- **InvokeTrigger** 支持可选参数 **onlyForSideIndex**（0 或 1）：指定时只遍历该侧物品，用于 AboutToLose 等单侧触发。

### 表格效果与 Override 条件书写经验（救生圈等）

- **▶ 与战斗开始**：表格中 **▶** 表示**使用物品时**触发的主动效果，应实现为默认 `Trigger.UseItem` 的能力（如 `Ability.Shield`）；**只有文案明确写「战斗开始时」**才使用 `Trigger.BattleStart`。勿因效果为「获得护盾」等而误用 BattleStart。
- **「首次」实现**：与靴里剑一致，用**物品状态**（如 Custom_0）表达「首次」——触发器名不包含 First；条件用 **SourceCustom0IsZero**，生效后同 trigger 下 **AddAttribute(Key.Custom_0).Override(..., value: 1, effectLogName: "")** 置 1。
- **condition 与 additionalCondition**：Override 时**不可同时指定**二者。目标条件在默认上追加时只写 **additionalCondition**；不包含默认或需替换时只写 **condition** 并用 `&` 组合。详见 **.cursor/rules/ability-override-format.mdc**。

---

## BattleContext 与施加触发器队列（原 EffectApplyContextImpl）

### 经验总结

1. **触发器统一为待处理队列**：用 **`EffectAppliedTriggerQueue`**（`List<(TriggerName, SideIndex, ItemIndex)>`）替代 OnFreezeApplied / OnSlowApplied / OnDestroyApplied。**`BattleContext`** 只追加条目，模拟器在 **`ability.Apply(ctx)`** 后统一 **`InvokeTrigger`**；**Destroy** 再设 **`Destroyed`**。扩展新「施加时触发」只需在 **`BattleContext`** 写队列、模拟器多一种 **triggerName**。
2. **ApplyToTargets 与 effectTriggerName**：Freeze/Slow 等对每目标施加后把 **`Trigger.Freeze`/`Trigger.Slow`** 写入队列；**ApplyDestroy** 只写 **`Trigger.Destroy`**，不在此处标记 **Destroyed**。
3. **属性方法复用**：**`AddAttributeToCasterSide`** / **`SetAttributeOnCasterSide`** / **`ReduceAttributeToSide`** 共用按条件遍历与日志收集逻辑（见 **`BattleContext.EffectApply.cs`**）。
4. **已移除 `IEffectApplyContext`**：施放者用 **`BattleContext.Caster`**（与 **`Item`** 在效果执行时一致）；吸血用 **`ctx.GetResolvedValue(Key.LifeSteal, defaultValue: 0) != 0`**；飞行用 **`Caster.InFlight`**。

### 对照规则

修改效果应用或 Freeze/Slow/Destroy 触发逻辑时，见 **.cursor/rules/battle-simulator-ability-queue.mdc**；**不持有**触发器回调，仅通过 **`EffectAppliedTriggerQueue`** 上报，模拟器单点消费。

---

## 弹药消耗触发器（Trigger.Ammo）与 Condition.AmmoDepleted

- **设计**：不在「弹药耗尽」时单独设触发器，而是**每次弹药消耗**时触发 **Trigger.Ammo**（步骤 7 中在 InvokeTrigger(UseItem) 之后执行 `AmmoRemaining = max(0, AmmoRemaining - 1)`，再调用 `InvokeTrigger(Trigger.Ammo, item, ...)`）。来源（causeItem）= 消耗弹药的那个物品。默认 Condition 为 SameSide。步骤 7 顺序详见「步骤 7 施放顺序、Immediate 当场执行与效果日志抑制」。
- **「仅耗尽当次」**：用 **additionalCondition: Condition.AmmoDepleted** 限定。`Condition.AmmoDepleted` = **WithTemplateTag(Tag.Ammo)** 且 AmmoRemaining == 0（引起触发的物品模板带 Tag.Ammo 且刚扣完一发后为 0；用 WithTemplateTag 避免光环递归）。弹药物品由注册时 AmmoCap > 0 自动获得 Tag.Ammo。例如生体融合臂：`Ability.Damage.Override(trigger: Trigger.Ammo, additionalCondition: Condition.AmmoDepleted, targetCondition: Condition.DifferentSide)`。
- **弹药物品筛选**：用 **Condition.WithTag(Tag.Ammo)**（如光环「此物品左侧的弹药物品 +1 最大弹药」用 `Condition.LeftOfSource & Condition.WithTag(Tag.Ammo)`）。**左侧**若指**相邻左侧**用 **LeftOfSource**，若指**所有严格左侧**用 **StrictlyLeftOfSource**。
- **Key.AmmoRemaining**：运行时剩余弹药存于物品模板字典（与 `ItemTemplate.KeyAmmoRemaining` 一致），供 Condition 与公式使用。

### 经验：Override 换 trigger 且只传 additionalCondition 时须用新 trigger 的默认条件

若能力由 **Ability.xxx**（默认 UseItem + SameAsSource）出发，只改 **trigger** 并只传 **additionalCondition**（如 `Override(trigger: Trigger.Ammo, additionalCondition: Condition.AmmoDepleted)`），合并时 **baseCond** 必须用**新 trigger 的默认条件**（如 Ammo → SameSide），而不能用原能力上的 **Condition**（SameAsSource）。否则会得到 (SameAsSource & AmmoDepleted)，语义变成「仅当**能力持有者本人**弹药耗尽时」才触发；设计意图多为「己方**任意**弹药物品耗尽时」即 (SameSide & AmmoDepleted)。实现上在 **AbilityDefinition.Override** 中：`baseCond = condition ?? (originalTrigger != TriggerName ? defaultCond : Condition ?? defaultCond)`，保证换 trigger 且未显式传 condition 时用 defaultCond。克隆能力时无需再对 Triggers[0] 做 SyncPrimaryTriggerEntryFromTopLevel 覆盖，源模板已正确。

---

## 步骤 7 施放顺序、Immediate 当场执行与效果日志抑制

### 步骤 7 顺序与弹药消耗

- **顺序**：对 castQueue 中每件物品依次执行：**先**读取 Multicast（此时 AmmoRemaining 尚未扣减，满弹物品的 multicast 正确）、**再**调用 `InvokeTrigger(Trigger.UseItem, ..., executeImmediate: 委托)`、**再**若有弹药则 `AmmoRemaining = Math.Max(0, AmmoRemaining - 1)`、**再**`logSink.OnCast(...)`、**最后**若有弹药则 `InvokeTrigger(Trigger.Ammo, ...)`。
- **原因**：「使用一次消耗全部弹药」类物品（如手里剑）需在 UseItem 触发时用 **Immediate** 能力（ReduceAttribute AmmoRemaining by AmmoCap）把弹药减到 0，且多重释放应按**使用瞬间的满弹**计算；若先执行 `AmmoRemaining--` 再 InvokeTrigger，则 multicast 会少 1 且语义不符。故弹药扣减必须放在 InvokeTrigger(UseItem) **之后**，且扣减为 `max(0, AmmoRemaining - 1)` 保证不低于 0。

### Immediate 能力当场执行（不入队）

- **InvokeTrigger** 支持可选参数 **executeImmediate**（`Action<ItemState, int, AbilityDefinition>?`）。仅在步骤 7 调用 `InvokeTrigger(Trigger.UseItem, ...)` 时传入该委托。
- **语义**：当某能力匹配 UseItem 且 **Priority == Immediate** 时，**不入队**（不调用 AddOrMergeAbility），改为**当场**调用 `executeImmediate(owner, abilityIndex, ability)`；委托内构造 `AbilityQueueEntry` 并调用 **ExecuteOneEffect**（isCrit: false），效果立即生效（如手里剑的 ReduceAttribute 将 AmmoRemaining 减到 0）。非 Immediate 能力仍照常入 nextAbilityQueue。
- **用途**：手里剑等「使用一次消耗全部弹药」：Immediate 的 ReduceAttribute(Key.AmmoRemaining, amountKey: Key.AmmoCap)、targetCondition: SameAsSource 在 UseItem 触发时立刻执行，再执行 `AmmoRemaining = max(0, -1)` 保持为 0，最后 InvokeTrigger(Ammo)。

### 效果日志抑制

- **AddAttribute/ReduceAttribute** 的 GUI 日志名由 **EffectLogName**（Override 的 effectLogName）或默认「属性中文名+提高/降低」决定。
- **不显示某条效果日志**：在该能力上 **Override(effectLogName: "")**；实现层在 **logName / logEffectName 为空时不调用** `LogSink.OnEffect`：**ReduceAttributeToSide** 与 **AddAttributeToCasterSide** 的 `if (targetNames.Count > 0 && !string.IsNullOrEmpty(logName))` 再打日志；**ApplyToSideWithCondition** 同样在 `!string.IsNullOrEmpty(logEffectName)` 时才调用 LogEffect。例如手里剑「消耗全部弹药」、救生圈「Custom_0 置 1」设 `effectLogName: ""`，战斗日志中不显示该行。

---

## InvokeTarget 与 SameAsInvokeTarget（单目标施加）

- **场景**：部分能力由「触发器指向的单个目标」触发（如月光宝珠「敌方加速时令其减速」、Freeze/Slow 每目标一次）。效果应对**该目标**施加，而不是再按 TargetCondition 从双方选目标。
- **入队**：当 `InvokeTrigger` 的 context 提供 `InvokeTargetItem` 且能力通过 InvokeTargetCondition 时，**AddOrMergeAbility** 传入 `invokeTargetSideIndex` / `invokeTargetItemIndex`，新建条目且**不参与合并**（不查同 Owner+AbilityIndex 合并 PendingCount），保证「每目标一条」。
- **AbilityQueueEntry**：新增 **InvokeTargetSideIndex**、**InvokeTargetItemIndex**（均为可空）。非空时表示本条目应对该 (side, index) 物品施加效果；**ExecuteOneEffect** 内解析为 **`BattleContext.InvokeTarget`**（**`InvokeTargetItem`** 属性）。
- **效果层**：**InvokeTarget** 非空时，多目标类效果通过 **TargetCondition** 与 **SameAsInvokeTarget** 等收窄到该目标。**Condition.SameAsInvokeTarget** 表示「候选目标与触发器指向目标相同」。
- **Burn/Poison/Shield**：EffectAppliedTriggerQueue 中 Burn/Poison/Shield 存的是施加者（己方），故 InvokeTrigger 时 causeItem = 施加者、InvokeTargetItem = null；Freeze/Slow/Destroy 存的是目标，causeItem = 施放者、InvokeTargetItem = 目标。

### InvokeTrigger 遍历次序：先 cause 物品再其他

- **约定**：调用 `InvokeTrigger` 时，**先检查引起触发的那件物品（causeItem）上的能力，再检查其他物品**。例如 UseItem 时，先检查「被使用的那件物品」的能力，再检查同侧其余物品、再检查另一侧。
- **实现**：`BattleSimulator.InvokeTrigger` 中：(1) 侧顺序：若 causeItem 非空且未摧毁，先遍历 causeItem 所在侧，再遍历另一侧；(2) 同侧物品顺序：若本侧为 cause 所在侧，先遍历 `causeItem.ItemIndex` 该件，再按 0..Count-1 遍历其余（跳过 cause 下标）。无 causeItem 时仍按 (0, side0)、(1, side1) 与物品下标顺序。
- **用途**：保证「使用物品时」先处理被使用物品自身的 UseItem 能力（如伤害、暴击判定），再处理其他物品的「当其他物品被使用时」类能力（如弹簧刀给相邻武器加伤害），顺序可预期。

### UseItem 时传入 InvokeTargetItem

- **约定**：步骤 7 调用 `InvokeTrigger(Trigger.UseItem, item, context)` 时，**context 中传入 InvokeTargetItem = item**（被使用的那件物品），以便「当其他物品被使用时、对**被使用的那件物品**施加效果」类能力能正确选目标。
- **实现**：`new TriggerInvokeContext { Multicast = ..., UsedTemplate = ..., InvokeTargetItem = item }`。能力侧用 **additionalTargetCondition: Condition.SameAsInvokeTarget** 将 AddAttribute 等效果限定为「仅对触发时被使用的那件物品」施加（如弹簧刀：使用相邻武器时，使**该武器**伤害提高）。
- **与遍历次序的关系**：先检查 cause 物品再其他，再配合 InvokeTargetItem，可保证「被使用物品」自身能力先入队，且「对被使用物品施加」的条目带 InvokeTargetSideIndex/InvokeTargetItemIndex 正确入队。

### AddOrMergeAbility 带 InvokeTarget 时 PendingCount 必须用传入值

- **错误**：当 context 提供 InvokeTargetItem 时，新建条目**不合并**；若新建时把 **PendingCount 写死为 1**，会丢失 UseItem 的 **Multicast**，导致「多重释放：3」等物品每次使用只生效 1 次（如海底热泉只打 1 次灼烧）。
- **正确**：新建带 InvokeTargetSideIndex/InvokeTargetItemIndex 的条目时，**PendingCount = pendingCount**（即 context.Multicast），与无 InvokeTarget 分支一致。

---

## 能力队列 250ms 节流状态与冷却缩短联动

- **LastTriggerMs 挂在物品上**：250ms 触发间隔按「能力持有者 + 能力下标」维护，状态存于**物品**（ItemState）的字典（GetLastTriggerMs/SetLastTriggerMs），不再存在 AbilityQueueEntry 上。这样同一物品的多条能力各自节流，合并 PendingCount 时也不依赖条目的 LastTriggerMs。
- **AbilityQueueEntry**：移除 LastTriggerMs；保留 Owner、AbilityIndex、PendingCount，以及可选的 InvokeTargetSideIndex/InvokeTargetItemIndex。步骤 8 中「与上次触发间隔不足 250ms」用 `item.GetLastTriggerMs(entry.AbilityIndex)` 判断。
- **冷却缩短与充能联动**：ReduceAttribute（CooldownMs）时，目标隐性要求未摧毁；冷却时间有下限 **1000ms**（1 秒）。若缩短后该物品「已过冷却已满」（CooldownElapsedMs >= 新 CooldownMs），与充能满一致：加入 **ChargeInducedCastQueue** 并清零 CooldownElapsedMs（仅当该物品无弹药或弹药未耗尽时可加入施放队列）。这样「时光指针」「仿生手臂」等缩短冷却后立即可施放。

---

## Ability / Key / Override 重构

### 目标

- **Ability.xxx**：除 AddAttribute、ReduceAttribute 外均为**无参属性**（如 `Ability.Damage`、`Ability.Haste`），每次访问返回新的带默认值的 `AbilityDefinition`；定制统一通过 **Ability.xxx.Override(...)** 就地修改并返回 `this`。
- **Key**：`Core/Key.cs` 提供 `Key.Damage`、`Key.Shield`、`Key.Heal`、`Key.Burn`、`Key.Poison`、`Key.Custom_0`，与 `ItemTemplate` 字段名一致，在能力与物品定义中替代 `nameof(ItemTemplate.xxx)`，便于一处维护并与 `GetInt(key)`/`GetResolvedValue(key)` 兼容。
- **Override**：参数仅当非 null 时覆盖；Condition 与 TargetCondition 在 Override 内做「当前默认 + 传入 condition/additionalCondition、targetCondition/additionalTargetCondition」的合并；支持 SourceCondition 等全量可配置项。

### 书写约定

- **Override 格式**：`Override(` 后换行、缩进，每行一个参数；参数顺序为 trigger → (additional)condition → invokeTargetCondition → sourceCondition → (additional)targetCondition → priority；最后换行、回退缩进、`)`。详见 **.cursor/rules/ability-override-format.mdc**。
- **Override 简化**：当能力的默认 TargetCondition 已是 SameSide 或 DifferentSide 时，若只需在默认上**追加**条件，应使用 **additionalTargetCondition** 而非重写 **targetCondition**（如「己方且飞行」写 `additionalTargetCondition: Condition.InFlight`，勿写 `targetCondition: Condition.SameSide & Condition.InFlight`）。定义物品时能简化的要简化。
- **condition 简化边界**：若 `condition` 仅等于当前 trigger 的默认条件，可直接省略；若要把 `condition` 改写为 `additionalCondition`，必须先确认该 trigger 的默认条件与现有写法一致。特别是 `Trigger.UseOtherItem` 默认含 `Condition.DifferentFromCaster`，盲目改写可能改变语义。
- **AddAttribute / ReduceAttribute**：保留为方法，因需指定 attribute key，如 `Ability.AddAttribute(Key.Damage).Override(...)`、`Ability.ReduceAttribute(Key.Shield).Override(...)`。

### 小结

物品定义中能力列表写 `Ability.Damage`、`Ability.Shield` 等；需改 trigger、priority、condition、targetCondition 等时链式 `.Override(...)`，格式与顺序按规则文件统一。

---

## 物品模板：按等级属性与集合表达式

### 设计选择

- **只保留 `_intsByTier`**：扩展属性统一用 `Dictionary<string, List<int>>` 存储；单值（如 `CooldownMs = 2000`）存为长度为 1 的列表，读取时 `list.Count == 1` 则对所有 tier 返回该值。不再维护单独的 `_ints`，避免两套存储与同步问题。
- **IntOrByTier**：在对象初始器中同时支持单值（`Damage = 40`）与按等级列表（`Damage = [25, 35, 45, 55]`）。通过隐式转换（`int` → 单元素列表，`int[]`/`List<int>` → 列表）和属性 setter 写入 `_intsByTier`。
- **Key 不对外暴露**：`KeyDamage`、`KeyCooldownMs` 等改为 `private const`，不提供 GetKey 等 API。调用方（如 ItemState、BattleSimulator）直接用字符串字面量 `"Damage"`、`"CooldownMs"` 调用 `GetInt(key, tier)`，简化依赖。
- **未定义即默认值**：不使用 `ContainsKey`；`GetInt(key, tier, defaultValue)` 在 key 不存在时直接返回默认值。效果数值解析时若 `GetInt(key, tier)` 为 0 则用 `eff.Value` 作为 fallback。

### 集合表达式直接赋值（Damage = [25, 35, 45, 55]）

- **问题**：集合表达式 `[25, 35, 45, 55]` 无法直接赋给非 BCL 类型，会报「该类型不可构造」或需写 `(IntOrByTier)(int[])[...]`，不够简洁。
- **做法**：使用 C# 12 的 `[CollectionBuilder]`，让自定义类型成为集合表达式的目标类型：
  1. 在类型上标记 `[CollectionBuilder(typeof(IntOrByTier), "Create")]`。
  2. 实现静态方法 `Create(ReadOnlySpan<int> values)`，在方法内构造并返回该类型。
  3. **实现 `IEnumerable<int>` 并暴露 `GetEnumerator()`**：否则编译器报 CS9188「没有元素类型」。编译器通过枚举器推断集合的元素类型，非泛型自定义类型必须提供此信息。
- **依赖**：`CollectionBuilderAttribute` 在 .NET 9+ 的 `System.Runtime.CompilerServices` 中；若目标框架低于 .NET 9，需自行声明同名 attribute。

### IntOrByTier 单值隐式转换（ChargeTargetCount 等多目标数量）

- **现象**：物品定义中写 `ChargeTargetCount = 10`、`FreezeTargetCount = 2` 等单值赋值时，若隐式转换 `int → IntOrByTier` 产生的实例内部 `_values` 为空或未正确初始化，则 `ToList()` 返回空列表，写入 `_intsByTier` 后 `GetInt(key, tier, defaultValue)` 会因 `list.Count == 0` 返回默认值（如 1），导致效果层只对 1 个目标生效（如魔杖只充能 1 件）。
- **根因**：`implicit operator IntOrByTier(int single) => new([single])` 依赖集合表达式 `[single]`，在部分运行时/编译器路径下可能得到未正确填充的实例，进而 `_values` 为 null，`ToList()` 为 `[]`。
- **正确做法**：单值隐式转换改为显式构造**含一元素的列表**再传入私有构造，保证 `_values` 非 null、`ToList()` 恒为非空，例如 `new IntOrByTier(new List<int> { single })`。这样 `ChargeTargetCount = 10` 等写法无需在物品工厂内再写 `SetInt` 补丁即可正确写入并随克隆带到战斗。
- **读路径**：`GetInt` 对 `list.Count == 1` 直接返回 `list[0]`，与 tier/MinTier 无关，金/钻档位读取无问题；问题仅在写路径单值→列表的可靠性。

### 小结

按等级属性用单字典 + 列表长度区分单值/多值；初始器用 IntOrByTier 统一写法；对外用字符串 key、默认值 0 简化逻辑。需要 `X = [a,b,c]` 时用 CollectionBuilder + IEnumerable 让自定义类型支持集合表达式。**单值赋值**（如 `ChargeTargetCount = 10`）依赖 `IntOrByTier` 的隐式转换，实现上须保证单值→非空列表，避免读回默认值 1。

---

## 物品与测试物品的约定

- **与游戏对齐的物品不可随意更改**：`ItemDatabase/CommonSmall.cs`、`CommonMedium.cs`、`CommonLarge.cs` 中已有的物品用于对齐当前版本的 The Bazaar，**不得为调试/实验随意修改数值、文案或能力行为**；如需重构能力或触发逻辑，应通过额外测试物品或测试脚本验证，确认无误后再考虑与正式物品对齐。
- **测试物品单独存放**：为验证新机制（如 Ability 多触发、复杂光环等），可以在 `ItemDatabase` 下新建独立文件（如 `TestItems.cs` / `CommonExperimental.cs`）中定义测试专用的 `ItemTemplate`，命名和注释中明确标注「仅测试使用」。
- **注册与清理**：测试期间可以在对应 `RegisterAll` 中**临时**调用 `db.Register(...)` 注册测试物品，用 CLI/脚本跑完用例后，须删除这些注册调用（测试物品工厂方法本身可保留或按需删掉），避免测试物品意外出现在正式卡组或被当成与游戏对齐的内容。
- **测试脚本与日志**：新增测试物品时，应优先通过现有 CLI 批量脚本（或新增脚本）构造最小覆盖用例，通过对战日志（伤害、护盾、触发次数等）验证新机制行为是否符合预期，再据此更新实现笔记与规则文件。

---

## 英雄/关卡专属物品与目录结构

- **归属**：与特定英雄或关卡绑定的物品（如海盗 Vanessa 关卡专属）**不**放在 `CommonSmall.cs` / `CommonMedium.cs` / `CommonLarge.cs`，而放在 **ItemDatabase/&lt;英雄名&gt;/&lt;尺寸&gt;** 下，例如 `Vanessa/small`、`Vanessa/medium`。
- **一物一文件**：每个物品单独一个源文件，文件名与类名对应英文名（如藏刃匕首 → `ConcealedDagger.cs`、类 `ConcealedDagger`）；同一物品的多赛季版本放在同一文件中。
- **工厂方法命名**：**Template()** 表示当前/最新版本（无后缀或铜等基础档位），**Template_Sx()** 表示第 x 赛季版本（如 `Template_S1()`、`Template_S9()`），与物品显示名 `[名称]_Sx` 对应。
- **注册**：每个英雄有对应的 **RegisterAll**（如 `VanessaSmall.RegisterAll(db)`），在其中设置 **db.DefaultHero**（如 `Hero.Vanessa`）、**db.DefaultSize**、按档位设置 **db.DefaultMinTier** 后连续 **db.Register(ClassName.Template())** / **db.Register(ClassName.Template_Sx())**。主程序/数据库初始化时需调用各英雄的 RegisterAll，使专属物品被写入数据库。
- **与公共物品的区别**：Common* 使用 **Hero.Common**（由调用方在注册公共物品前设定），不设 DefaultHero 时通常为 Common；英雄专属物品在各自 RegisterAll 中显式设 **DefaultHero**，便于筛选与 UI 展示。

---

## 从表格添加物品的约定

当用户以**表格图片**形式提供物品需求时，列顺序为：**中文名、英文名、所属、版本、minTier、Size、CD、TAG、效果**（第 2 列**英文名**，后续不再单独给出、从表格读取；第 3 列**所属**、第 4 列**版本**用于明确物品归属与赛季）。

- **列映射**：第 1 列 → 中文名（Name）；第 2 列**英文名** → 文件名与类名 PascalCase（后续不再由用户单独提供）；**所属**（第 3 列）→ 英雄名（如 Vanessa）或**公共**；**版本**（第 4 列）→ 数字对应赛季，如 5 → _S5、12 → 无后缀或 _S12 视表格约定；minTier B/S/G/D；Size S/M/L；CD → Cooldown（秒）；TAG → Tags；效果 → Desc 与 Abilities/Auras。
- **版本列与第一行**：表格**第 4 列**为**版本号（数字）**。**表格第一行**对应**最新版本**，名称**无后缀**，实现为 `Template()`、`Name = "中文名"`；同物多行时后续行为历史版本，按该列数字实现为 `Template_Sx()`、`Name = "中文名_Sx"`。勿将第一行误写成带 _Sx 的名称。
- **版本号与 minTier 区分**：**第 4 列数字**表示**版本号**（用于 _Sx 命名），**第 5 列字母 B/S/G/D** 表示**minTier**（Bronze/Silver/Gold/Diamond）。勿将数字列当作 minTier 使用；档位仅由 B/S/G/D 列决定，在 RegisterAll 中设 `db.DefaultMinTier` 后注册。
- **效果文案与实现**：**▶** 以及文案中的「**提高**」「**造成**」等主动动词 = **使用物品时触发**的 Ability（UseItem），应实现为 Ability 而非被动光环。**无 ▶ 且无触发条件**的常驻加成（如「己方武器伤害 +X」）= 光环（Aura）。若误将「▶ 某类物品暴击率提高」写成光环，应改为 **Ability.AddAttribute(Key.CritRatePercent).Override(additionalTargetCondition: ..., priority: ...)**。
- **局外成长**：表格中「购买此物品时获得 X」「购买时 Y」等**局外/商店**效果，对战模拟器不实现，仅在类注释中说明「局外成长忽略」。若效果为「某条件下此物品的某属性提高 X/Y/Z/W（局外成长）」，可用**光环 + OverridableAttributes** 描述，见下节。
- **优先级**：**仅当表格效果末尾明确标注** (I)/(Hst)/(H)/(L)/(Lst) 时在代码中写 `priority`；**未标注则视为 Medium**，不要猜测或擅自添加 priority。
- **「此物品被加速/被减速时」**：`trigger: Trigger.Haste` / `Trigger.Slow`，**condition: Condition.SameAsInvokeTarget**（被加速/被减速的是本物品），**targetCondition: Condition.SameAsSource**（效果施加给自身）。参考毒须鲶、皮皮虾。
- **「每有一个相邻的…，此物品 +1 多重释放」**：多重释放 = 相邻数量，用 **Formula.Count(...)**，**不要**写 `Formula.Constant(1) + Formula.Count(...)`。参考迷幻蝠鲼、鹦鹉皮特。
- **英文名**：从表格**第 2 列**读取，后续不再单独给出；**文件名**与**类名**取该列英文名 PascalCase；工厂方法 `Template()`、`Template_Sx()`。
- **常用目标条件**：「己方最左侧/最右侧的武器」→ **LeftMost/RightMost(WithTag(Weapon))**；「此物品右侧的武器」→ **RightOfSource & WithTag(Tag.Weapon)**；「使用其他某类物品时」须 **Condition.DifferentFromSource**。
- **归属**：由**所属**列决定——**公共**放 Common*；**英雄名**放 **ItemDatabase/&lt;英雄名&gt;/&lt;尺寸&gt;**，一物一文件。

**经验小结**：（1）表格列顺序固定为**中文名、英文名、所属、版本、minTier、Size、CD、TAG、效果**，英文名从第 2 列读取，不再单独提供。（2）局外成长「购买某类物品时此物品某属性提高 X/Y/Z/W」统一用**基础值 + 光环(Custom_0×Custom_1) + OverridableAttributes(Custom_1 默认 5/10/15/20)**，治疗型参考珊瑚、护盾型参考珊瑚护甲。

详见 **.cursor/rules/item-table-convention.mdc**。

### 表格多物品实现经验（加速、暴击率、冷却、价值公式）

- **加速目标**：加速目标必须 HasCooldown 已在 ApplyHaste 内保证。只需在默认目标上追加「某类物品」时，用 **additionalTargetCondition**（如水系或玩具：`Condition.WithTag(Tag.Aquatic) | Condition.WithTag(Tag.Toy)`），勿重写 targetCondition，以保留默认 SameSide + NotDestroyed + HasCooldown。
- **暴击率光环**：暴击率描述的「%」由展示层自带，Aura 的 **CritRatePercent** 不设 `Percent = true`，仅 `Value = Formula.Source(Key.Custom_0)` 等即可。
- **冷却与光环顺序**：使用光环读取 **CooldownMs** 时，约定**先**按 **PercentCooldownReduction** 做百分比缩减，**再**叠加 **CooldownMs** 本身的固定/百分比光环；实现见 `ItemTemplate.GetInt(key, tier, defaultValue, context)` 中 `key == Key.CooldownMs` 分支。
- **价值×倍数护盾（温馨海湾型）**：护盾 = 有效价值 × 倍数，且「出售时价值提高」影响战斗内计算时，将 **Custom_1**（如已出售数量，OverridableAttributes 默认 10/20/40/80）与每次提高量 **Custom_2**（如 1/1/1/2）加入光环公式：`Value = (Formula.Source(Key.Price) + Formula.Source(Key.Custom_1) * Formula.Source(Key.Custom_2)) * Formula.Source(Key.Custom_0)`。出售逻辑局外不实现，Desc 保留。
- **标签与表格**：表格 TAG 列若写「水系、地产」，代码用 `Tag.Aquatic`、`Tag.Property`（如温馨海湾）；以表格为准，勿与同物其他版本或俗称混用。

---

## Price、龙涎香公式与首次使用暴击率

### Price 字段与注册时按尺寸默认值

- **Key.Price**：物品价值，用于龙涎香等治疗公式。在 **Register** 时按 **DefaultSize** 自动设置默认值：Small → [1,2,4,8]、Medium → [2,4,8,16]、Large → [3,6,12,24]（见 `ItemDatabase.GetDefaultPriceBySize`）。模板中可不写 Price，由注册注入。
- **Custom_2**：已加入 `Key.Custom_2` 与 `ItemTemplate.Custom_2`；可作为 **OverridableAttributes** 的 key（如龙涎香 Custom_2 默认 5/10/15/20，卡组可覆盖）。

### 龙涎香公式（光环治疗 + 购买水系时加价值）

- **治疗公式**：`Heal = (Price + Custom_1 * Custom_2) * Custom_0`，用光环提供 Key.Heal：`Value = (Formula.Source(Key.Price) + Formula.Source(Key.Custom_1) * Formula.Source(Key.Custom_2)) * Formula.Source(Key.Custom_0)`。
- **购买水系时价值提高**：**Ability.AddAttribute(Key.Price).Override(condition: SameSide & WithTag(Tag.Aquatic) & DifferentFromSource, targetCondition: SameAsSource, valueKey: Key.Custom_1)**，即己方使用其他水系物品时，此物品 Price += Custom_1。

### 首次使用暴击率 +100%（Custom_0 标记方案）

- **需求**：首次使用此物品时暴击率 +100%，之后不再加成。
- **实现**：(1) 模板 **Custom_0 = 0**；(2) 使用物品时若 Custom_0 == 0 则 **AddAttribute(Key.Custom_0)** 对自身 +1，**effectLogName: ""** 不显示日志，**priority: Low** 使伤害先于该能力执行（保证首次使用仍享暴击加成）；(3) 光环 **CritRatePercent +100%** 且 **SourceCondition = Condition.SourceCustom0IsZero**（仅当 Custom_0 == 0 时生效）。**Condition.SourceCustom0IsZero** 已加入 `Core/Condition.cs`，表示能力持有者（Source）的 Custom_0 为 0。

---

## OverridableAttributes 与 Aura Condition 约定

### OverridableAttributes 不重复定义

- **约定**：需要卡组内可覆盖属性时，**仅在 OverridableAttributes 中写一次默认值**（如 `OverridableAttributes = new Dictionary<string, IntOrByTier> { [Key.Custom_1] = [5, 10, 15, 20] }`），**不要在模板上再写一遍**同名属性（如不再写 `Custom_1 = [5, 10, 15, 20]`）。
- **实现**：`ItemDatabase.Register` 在写入 Size/MinTier/Hero 之后、`EnsureTypeTags` 之前，会遍历 `template.OverridableAttributes`，对每个 key 调用 `template.SetIntOrByTierByKey(kv.Key, kv.Value)`，将默认值同步到模板内部字典，供战斗内公式（如 `Formula.Source(Key.Custom_1)`）与 Desc 占位符使用。
- **效果**：物品定义中只需维护一处默认值，GUI 的「覆盖属性」对话框与战斗内读取均一致。

### 局外成长与光环公式（狼筅、珊瑚、珊瑚护甲）

- **适用场景**：表格效果为「某局外条件（如购买水系、赢得战斗）时，此物品的某属性提高 X/Y/Z/W」，对战模拟器**不实现**局外触发逻辑，但可用**光环公式 + OverridableAttributes** 在战斗内正确读出「当前成长后的数值」，并由卡组 Overrides 或外部逻辑在局外更新可覆盖变量。
- **写法要点**：
  1. **基础数值**（若有）：如护盾/治疗有固定基础，在模板上写 `Shield = 50` 或 `Heal = 20`；使用时的总值 = 基础 + 光环加成。
  2. **文案数值（档位/档位列表）**：写在模板上，如 `Custom_0 = [10, 20, 30, 40]`（每次购买提高量）或 `[40, 60, 80, 100]`，用于 Desc 占位符 `{Custom_0}` 与公式中的「每档数值」。
  3. **局外变量**：**仅**写在 **OverridableAttributes** 中，如 `[Key.Custom_1] = [4, 8, 12, 16]`（赢得战斗次数阈值）或 **`[Key.Custom_1] = [5, 10, 15, 20]`**（已购买数量，**常用默认**）；局外逻辑在适当时机更新卡组槽位的 `Overrides[Key.Custom_1]`。
  4. **光环**：用公式将两者结合，如 `Value = Formula.Source(Key.Custom_0) * Formula.Source(Key.Custom_1)`；若还有基础值，则能力用模板基础、光环对该属性做**加法**（如护盾 = 50 + Custom_0×Custom_1）。
- **参考**：**珊瑚**（治疗 = 基础 20 + Custom_0×Custom_1，Custom_0 每次购买提高量，Custom_1 已购买水系数量，默认 5/10/15/20）；**珊瑚护甲**（护盾 = 基础 50 + Custom_0×Custom_1，同上模式，Custom_0 = 10 » 20 » 30 » 40）；**狼筅**（伤害提高 = Custom_0×Custom_1，Custom_0 为 40 » 60 » 80 » 100，Custom_1 可覆盖，默认赢得战斗次数阈值 4/8/12/16）。

### AuraDefinition Condition 默认 SameAsSource

- **约定**：当光环作用目标为**来源物品自身**时（即 `Condition.SameAsSource`），**不要显式写** `Condition = Condition.SameAsSource`。
- **实现**：`AuraDefinition.Condition` 的默认值即为 `Condition.SameAsSource`（见 `Core/AuraDefinition.cs`），显式写出会造成冗余且易在修改时漏改。
- **示例**：珊瑚的治疗加成光环只需写 `AttributeName = Key.Heal`、`Value = Formula.Source(Key.Custom_0) * Formula.Source(Key.Custom_1)`，无需写 `Condition = Condition.SameAsSource`。护盾型（珊瑚护甲）同理，`AttributeName = Key.Shield`，基础护盾写在模板 `Shield = 50`。

---

## 运行时变量与字典（ItemTemplate / BattleSide）

### 设计目标

游戏过程中产生的变量（阵营下标、物品下标、等级、冷却已过时间等）统一存入字典，便于用 **名字** 一致解析（公式、日志、后续扩展）。

### ItemTemplate 中的运行时变量

- **键常量**（Core/ItemTemplate）：`KeySideIndex`、`KeyItemIndex`、`KeyTier`、`KeyCooldownElapsedMs`、`KeyHasteRemainingMs`、`KeySlowRemainingMs`、`KeyFreezeRemainingMs`、`KeyInFlight`、`KeyDestroyed`、`KeyAmmoRemaining`、`KeyLastTriggerMsPrefix`（能力上次触发时间为 `LastTriggerMs_0`、`LastTriggerMs_1`…）；暴击相关 **KeyCritTimeMs**、**KeyIsCritThisUse**、**KeyCritDamagePercentThisUse**（见下节「暴击机制」）。与按等级属性（CooldownMs、Damage 等）无名称冲突。
- **ItemState**：上述 int/bool 不再作为独立字段，全部通过 **Template.GetInt(key)** / **Template.SetInt(key, value)** 与 **GetBool/SetBool** 读写；对外仍保留同名属性（如 `item.SideIndex`、`item.Tier`）委托到 Template。能力上次触发时间用 **GetLastTriggerMs(abilityIndex)** / **SetLastTriggerMs(abilityIndex, timeMs)**。暴击状态用 **CritTimeMs**、**IsCritThisUse**、**CritDamagePercentThisUse**。
- **bool**：Template 用 0/1 存储，提供 **GetBool(key)**、**SetBool(key, value)**。

### BattleSide 中的数值字段

- **阵营数值**（BattleSimulator/BattleSide）：`BattleSide.Attributes` 与 **Core/Key** 对齐；`Key.SideIndex=0`，`Key.MaxHp`/`Key.Hp` 分别别名 `Key.Damage`/`Key.Heal`，`Key.Gold=Regen+1`，**`SideStateAttributeCount=Gold+1`（仅 0～7）**。即将落败「首次」等由物品 **Custom_0** 等状态表达，不占用阵营数组。可选 `GetInt(string)` 解析 JSON 键名。
- **BattleSide** 持有一个 **Dictionary<string, int>**，属性 SideIndex、MaxHp、Hp、Shield、Burn、Poison、Regen 均委托到 **GetInt(key)** / **SetInt(key, value)**，便于公式（如 Formula.Side/Formula.Opp）与按名访问。

### 小结

战斗内读任意运行时变量均可通过 **template.GetInt(key)** 或 **side.GetInt(key)** 按名解析，无需为每种变量单独写分支。

---

## 能力与 Effect 合并重构

- **目标**：一个能力 = 一条效果语义；原 `AbilityDefinition.Effects`（`List<EffectDefinition>`）与 `EffectDefinition` 合并进 `AbilityDefinition`。
- **AbilityDefinition**：直接承载 `Value`、`ValueKey`、`ApplyCritMultiplier`、`Apply`（**`Action<BattleContext>?`**）、`ResolveValue(template, tier, defaultKey)`；**不再有** `Effects` 列表；**已删除** `EffectDefinition` 类。
- **Core/Apply.cs**：预定义 **`Action<BattleContext>`**（如 **`Apply.Damage`**、**`Apply.AddAttribute(key)`**）；委托内使用 **`ctx.GetResolvedValue`**、**`ctx.ResolvedValue`**（**ValueKey** 解析值）、**`ctx.TargetCondition`** 等；目标条件由 **`ExecuteOneEffect`** 在 **`ctx.Rebind`** 时注入。
- **执行**：**`ExecuteOneEffect`** 对单个能力解析 **ResolvedValue**、**`ctx.Rebind`** 后调用 **`ability.Apply(ctx)`**；暴击判定用 **`ability.Apply != null && ability.ApplyCritMultiplier`**。
- **克隆**：ItemDatabase / BattleSimulator 克隆能力时复制 `Value`、`ValueKey`、`ApplyCritMultiplier`、`Apply`，无 Effects。
- **多效果能力**：若需「一个触发下先 A 再 B」（如原 Haste + AddAttribute），拆成**两条能力**放入同一物品的 `Abilities` 列表，同优先级时按 AbilityIndex 顺序执行。

---

## 物品与效果扩展（战斗内修改、自定义效果）

### IntOrByTier.Add(delta) 与战斗内模板

- **IntOrByTier**：增加 `Add(int delta)`，对每个 tier 的值加 delta 并返回新的 IntOrByTier；空列表时返回 `[delta, delta, delta, delta]`。用于「本场战斗内增加某数值」类效果（如举重手套给武器加伤害）。
- **战斗内模板**：`BuildSide` 时每个卡组槽位从 resolver 取 template 后**克隆**一份（新 ItemTemplate + SetIntsByTier），再创建 `ItemState(clone, tier)`。因此 `ItemState.Template` 为当局专用，可直接修改，例如 `wi.Template.Damage = wi.Template.Damage.Add(value)`，无需额外 DamageBonus 字段。
- **卡组 Overrides 仅应用 OverridableAttributes 内键**：应用 `entry.Overrides` 时，**只对模板的 `OverridableAttributes` 中存在的 key** 执行 `clone.SetInt(kv.Key, kv.Value)`；若对 Overrides 全量应用，卡组中多出的键（旧版序列化、GUI 默认值等）会覆盖克隆上的 Multicast、AmmoCap、Damage 等，导致多重释放/弹药/伤害失效（如海底热泉多重释放 3 只生效 1 次）。见 BattleSimulator.BuildSide。
- **Damage 的 getter**：若需对 Damage 做「整体加 delta」并写回，模板的 Damage 属性 get 需返回**完整按等级列表**（如通过 GetIntOrByTier），否则 get 只返回单值会丢失其他 tier。

### 效果数值与 Apply 委托（当前约定）

- **AbilityDefinition**：持有一条效果的 `Value`、`ValueKey`、`ApplyCritMultiplier`、`Apply`（**`Action<BattleContext>?`**）、`ResolveValue`。只读效果在 **`Apply.*`** 内用 **`ctx.GetResolvedValue(Key.XXX, applyCritMultiplier)`**；**ValueKey** 能力由模拟器填入 **`ctx.ResolvedValue`**。**TargetCondition** 注入 **`ctx.TargetCondition`**。
- **自定义/扩展效果**：在 **`Core/Apply.cs`** 增加静态委托，或 **`new AbilityDefinition { Apply = ctx => { ... } }`**；详见 **.cursor/rules/item-design.mdc**。

### AddAttribute / ReduceAttribute 与统一属性增减

- **统一入口**：「给己方某类物品加属性」「给敌方某类物品减属性」用 **Ability.AddAttribute** / **Ability.ReduceAttribute**（绑定 **`Apply.AddAttribute` / `Apply.ReduceAttribute`**）。
- **目标条件与 Haste/Slow 一致**：目标条件写在能力的 **TargetCondition** 上，由模拟器注入 `ctx.TargetCondition`；Apply 委托内用 `ctx.TargetCondition ?? Condition.SameSide`（AddAttribute）或 `?? Condition.DifferentSide`（ReduceAttribute）。**默认**：AddAttribute 己方(SameSide)、ReduceAttribute 敌方(DifferentSide)。
- **Ability.AddAttribute(attributeName, amountKey?, targetCondition?, additionalTargetCondition?, ...)**：`targetCondition` 非空时**完全代替**默认目标；`additionalTargetCondition` 非空且在未传 targetCondition 时在默认 **SameSide** 上追加，即 `TargetCondition = SameSide & additionalTargetCondition`。例如举重手套、裂盾刀用 `additionalTargetCondition: Condition.WithTag(Tag.Weapon)` / `Condition.WithTag(Tag.Shield)`。
- **Ability.ReduceAttribute(...)**：同上，默认目标为 **DifferentSide**，`additionalTargetCondition` 在 DifferentSide 上追加。
- **简化写法**：仅需「己方全体」或「敌方全体」可不传 target 参数；需收窄目标时用 `additionalTargetCondition: Condition.WithTag(Tag.Weapon)` 等，与 Haste/Slow 的 additionalTargetCondition 用法一致。

### 「触发条件」与「效果目标」勿混淆（AddAttribute/ReduceAttribute）

- **语义区分**：**Condition** = 何时触发（引起触发的物品 Item 需满足）；**TargetCondition** = 效果施加给谁（候选目标 Item 需满足）。
- **易错**：文案为「某条件下**此物品**获得 X」时（如「相邻物品触发减速时，此物品获得剧毒」），**Condition** 应描述「谁触发了该能力」（如被减速的物品与能力持有者相邻 → `condition: Condition.AdjacentToSource`），**TargetCondition** 应为 **SameAsSource**（只有能力持有者自己享受加属性），不能把「相邻」写在 `additionalTargetCondition` 上，否则会变成「己方且与 Source 相邻的物品」获得加成（即相邻物品而非本物品）。
- **正确写法**：`Ability.AddAttribute(..., targetCondition: Condition.SameAsSource, condition: Condition.AdjacentToSource, trigger: Trigger.Slow)`。反例（已修正）：失落神祇曾误用 `additionalTargetCondition: Condition.AdjacentToSource`，导致剧毒加给了相邻物品。

---

## 属性增减与效果目标选取统一（Reduce/Add、冻结/加速/减速/充能）

本节记录「Reduce 统一为 ReduceAttribute + Override」「冻结/减速/加速/充能从双方选目标」「Apply 层强制条件」「日志与百分比」等经验。

### ReduceAttribute 统一入口（与 AddAttribute 一致）

- **统一入口**：仅保留 **Ability.ReduceAttribute(attributeName)**；对己方减属性用 `ReduceAttribute(attributeName).Override(targetCondition: Condition.SameSide)`。己方/敌方完全由 **TargetCondition** 决定：从双方按 TargetCondition 选目标后施加减少，与 Freeze/Slow/AddAttribute 一致。
- **单 Apply**：**`Apply.ReduceAttribute(key)`** 内调用 **`ctx.ReduceAttributeToSide(...)`**；**`ReduceAttributeToSide`** 内部用 **`GetTargetsFromBothSides`** 从双方选目标。
- **通用按 key 增减**：**ReduceAttributeToSide** 用 **Template.GetInt/SetInt(attributeName)** 做「当前值 − value、不低于 0」，不再对 Shield、FreezeRemainingMs 等写 if/else；新增属性时在 **AttributeLogNames** 与实现中补 key 即可。

### 效果日志名：AttributeLogNames 与 EffectLogName

- **AttributeLogNames**（**Core/AttributeLogNames.cs**）：Key → 中文名映射。默认日志为「属性中文名 + 提高/降低」。
- **AbilityDefinition.EffectLogName**：Override 时可传 **effectLogName**。**`ExecuteOneEffect`** 在 **`ctx.Rebind`** 时注入 **`EffectLogName`**；克隆能力时须复制此项。

### 冻结/减速/加速/充能：双方选目标与 Apply 强制条件

- **选取范围**：目标从**双方所有物品**中按 **TargetCondition** 筛选（**GetTargetsFromBothSides**），不再限定「冻结只选敌方、加速只选己方」；通过 TargetCondition 表达「己方」「敌方」或自定义范围（如寒冰特服「自身或相邻」）。
- **Apply 层强制条件**：  
  - **NotDestroyed**：冻结、减速、加速、充能、摧毁在 Apply 内统一 **cond = (targetCondition ?? default) & ~Condition.Destroyed**，定义时 Override targetCondition **不必再写** 未摧毁条件。  
  - **HasCooldown**：仅**减速、加速、充能**在 Apply 内再强制 **& Condition.HasCooldown**；**冻结**不强制，以便支持「冻结己方无冷却物品」（如寒冰特服冻自身或相邻服饰）。
- **定义简化**：例如寒冰特服只需 `Ability.Freeze.Override(targetCondition: Condition.SameAsSource | Condition.AdjacentToSource)`，不写 NotDestroyed/HasCooldown。

### 冻结减免与百分比数值（RatioUtil）

- **PercentFreezeReduction**：施加冻结时有效时长 = 原始时长 − 减免量。减免量用 **RatioUtil.PercentFloor(freezeMs, pct)**（value 的 percent% 向下取整；当 percent<=0 时返回 0），勿手写 `freezeMs * (100 - pct) / 100`。
- **RatioUtil**：统一使用 **PercentFloor(value, percent)** 处理百分比向下取整（包括减免量、扣除量、至少为 1 的场景）；其中 percent<=0 时固定返回 0。见 **Core/RatioUtil.cs**。

### Override 经验

- **只覆盖有变化的项**：priority、targetCondition 等与能力默认一致时不必写，避免冗余（如「解除冻结」UseItem 那条用默认 Medium 即可，无需写 `priority: AbilityPriority.Medium`）。
- **Reduce 与 Add 统一**：不再使用 reduceToCasterSide；己方减属性仅用 `ReduceAttribute(...).Override(targetCondition: Condition.SameSide)`，目标由 TargetCondition 从双方筛选，与 Freeze/AddAttribute 一致。

### 小结

修改属性增减或冻结/减速/加速/充能时：Reduce 己方用 ReduceAttribute + Override(targetCondition: Condition.SameSide)；日志用 AttributeLogNames + 可选 effectLogName；目标由 TargetCondition 在双方中筛选（Reduce 与 Add/Freeze 一致），Apply 内固定加 `~Condition.Destroyed`，减速/加速/充能固定加 HasCooldown；百分比用 RatioUtil。

---

## 光环（Aura）与属性读取

### 使用时机与集成方式

- **仅战斗内**：局外/UI 直接 **`ItemTemplate.GetInt(key, tier)`**，不参与光环。
- **累加语义（公式不变）**：若在读数时叠加光环，约定 **`最终值 = (基础 + Σ 固定) × (1 + Σ 百分比/100)`**；多光环固定与百分比加算；**被摧毁的物品不提供光环**。
- **实现位置**：已移除 **`IAuraContext`**；**`BattleSimulator/BattleAuraModifiers.cs`** 提供静态 **`BattleAuraModifiers.Accumulate(side, targetItem, opp, attributeName, out fixedSum, out percentSum)`**，在内部遍历己方未摧毁物品的 **`Template.Auras`**，对 **`AuraDefinition.Value`** 用 **`Formula.Evaluate(BattleContext)`** 求值并按 **Percent** 累加。**当前** **`ItemTemplate.GetInt` 无 aura 重载**；**`BattleSide.GetItemInt`** 读 **`item.GetAttribute`** / **`Template.GetInt`**，**尚未**调用 **`BattleAuraModifiers`**（接回时需在此处或统一读模板入口合并基础值与光环）。

### 光环数据与条件

- **AuraDefinition**（Core）：`AttributeName`（用 **Key.***）、`Condition`、`SourceCondition`、**Value**（`Formula?`）、**Percent**（bool，默认 false）。固定/百分比已统一为 **Value = Formula** + **Percent**（如 `Value = Formula.Source(Key.Custom_0)`、百分比时 `Percent = true`）。BuildSide 与 ItemDatabase 克隆模板时对 Auras 做深拷贝（复制 Value、Percent）。
- **效果数值（当前实现）**：**`BattleContext.GetResolvedValue`** 读取 **`Caster.GetAttribute(key)`**（战斗内 **`ItemState.Attributes`**），再按暴击倍率缩放。**若**需「模板基础 + 己方光环」合一读数，应在 **`GetResolvedValue` 或 `GetItemInt`** 路径显式合并 **`BattleAuraModifiers.Accumulate`** 与 **`Template.GetInt`**（待接线时对照上式）。

### 光环公式（Formula 委托类型）与 SourceCondition 优先

- **能用 SourceCondition 表达的优先用 SourceCondition**：若光环「仅当提供者满足某条件时生效」且数值来自模板字段，用 **AuraDefinition.SourceCondition** + **Value = Formula.Source(Key.xxx)**。例如「若此为唯一伙伴则暴击率 +Custom_0%」为 **SourceCondition = Condition.OnlyCompanion**、**Value = Formula.Source(Key.Custom_0)**、**Percent = true**。
- **Formula**：**`AuraDefinition.Value`** 类型为 **`Formula?`**。**`Formula.Evaluate(BattleContext)`**（**`Core/Formula.cs`**）。通过 **Formula.Source(key)**、**Formula.Side/Opp**、**Formula.Count(condition)** 等组合；**`BattleAuraModifiers`** 内对每条 Aura 构造含 **`BattleState`** 的 **`BattleContext`** 再求值。
- **克隆**：克隆 Aura 时复制 **Value**、**Percent**（Formula 引用不变）。

### 描述占位符：前缀/后缀

- **语法**：大括号内可为 `{前缀Key后缀}`，前缀/后缀为紧挨 key 两侧的「非字母、非数字、非下划线」字符，key 为字母/数字/下划线（如 `{+Custom_0%}` → key=Custom_0，前缀=+，后缀=%）。
- **替换与样式**：单 tier 替换为 `前缀+数值+后缀`；全 tier 为 `前缀+值1+后缀 » 前缀+值2+后缀 » …`。前缀与后缀随数值一起纳入 valueRanges，加粗与 tier 颜色作用于整段（如 "+3%" 整体高亮）。
- **实现**：`ItemDescHelper` 用正则解析占位符，不再维护固定 Placeholders 列表；`{Cooldown}` 仍映射为 key `CooldownMs` 并按秒格式化。

### 暴击与日志、关键词样式

- **暴击日志**：`IBattleLogSink.OnEffect(..., bool isCrit = false)`；当 `isCrit` 为 true 时，Console/TextBox/File 等 sink 在效果行末尾追加「 （暴击）」。
- **关键词着色**：`EffectKeywordFormatting` 中「伤害」「灼烧」「暴击」「暴击率」「暴击伤害」等仅修改颜色，**不加粗**；加粗仅用于物品名称和嵌入的数值区段（由 ItemDescHelper 的 valueRanges 控制）。「飞行」与护盾同色（rgb(244,207,32)），Tooltip 与战斗日志一致。

---

## 飞行机制、造成暴击时与战斗内属性统一带光环

### 飞行（In Flight）

- **飞行为属性**：运行时状态存于模板字典（**Key.InFlight**）。**开始飞行** = 对己方满足目标条件且**未飞行**的物品设为飞行：**Ability.StartFlying**（等价于 `AddAttribute(Key.InFlight).Override(value: 1, additionalTargetCondition: ~Condition.InFlight)`），效果走 AddAttributeToCasterSide，内部对 Key.InFlight 设 `wi.InFlight = (value != 0)`。**结束飞行** 已改名为 **StopFlying**（**Ability.StopFlying**），目标默认己方且 **InFlight**，效果为 **SetAttributeOnCasterSide(Key.InFlight, 0, ctx.TargetCondition)**。
- **光环条件**：`Condition.InFlight` 表示被评估对象（Item）在飞行；“不在飞行”统一写 `~Condition.InFlight`，不再维护 `Condition.NotInFlight` 常量条件。
- **日志与 UI**：「开始飞行」「结束飞行」日志与 **EffectKeywordFormatting** 中「飞行」与护盾同色。

### 造成暴击时（Trigger.Crit）与暴击机制（按物品按帧统一）

- **暴击判定**：每个物品在**同一帧**内只做一次暴击判定。步骤 8 循环中，若某能力可暴击（`ItemHasAnyCrittableField`、`ApplyCritMultiplier`、**UseSelf**）且 `item.CritTimeMs != timeMs`，则掷骰并写入 `item.CritTimeMs`、`item.IsCritThisUse`、`item.CritDamagePercentThisUse`；若已等于 `timeMs` 则直接复用，不掷骰。
- **UseSelf**：`AbilityDefinition.UseSelf` 默认为 true；表示 Trigger 为 UseItem 且**未在 Override 中提供 condition**（仅用 additionalCondition 时仍为 true）。Override 时若传入了 `condition` 则设为 false。**仅 UseSelf 的 UseItem 能力可参与暴击判定**（「自己使用」才可暴击；「其他物品使用则触发」类能力不掷暴击、不触发 Crit）。
- **Crit 触发时机**：在**调用 ExecuteOneEffect 之前**，当该物品本帧**首次**判定为暴击（刚掷骰且 `isCrit == true`）时调用一次 `InvokeTrigger(Trigger.Crit, item, null, ...)`；复用本帧已有暴击结果时不再触发。ExecuteOneEffect 内不再调用 Crit 触发。
- **语义**：与 Freeze/Slow 统一——**任意物品造成暴击时触发**；默认 `Condition.SameSide` 表现为己方暴击时触发，可重写 Condition 实现对方暴击时触发。
- **条件**：`EnsureTriggerCondition(Trigger.Crit)` 默认 `Condition.SameSide`。

### 战斗内属性读取（BattleSide.GetItemInt）

- **原则**：依赖变量的光环在读模板/读运行时字段时都应一致；**接回光环后**宜在 **`GetItemInt`** 或 **`GetResolvedValue`** 单点合并 **`BattleAuraModifiers`**。
- **当前实现**：**`BattleSide.GetItemInt(itemIndex, key, default)`** — **`key` 为 int**；若 **`key < ItemState.Attributes.Length`** 则 **`Items[itemIndex].GetAttribute(key)`**，否则 **`Template.GetInt(key, ...)`**（**无**光环叠加）。
- **调用点**：BattleSimulator 步骤 7（Multicast、AmmoCap 等）、ProcessCooldown；**`BattleContext.EffectApply`** 内充能/选目标等。**吸血**由 **`ctx.GetResolvedValue(Key.LifeSteal, ...)`** 判断。

### 依赖变量的光环（Formula.Source）

- **用途**：如「Burn = 0 + 自身 Damage（含光环）」：**`Attribute = Key.Burn`**、**`Value = Formula.Source(Key.Damage)`**、Condition=SameAsSource。
- **接回时**：读 **Damage** 的路径须与 **`BattleAuraModifiers.Accumulate`** 合并，避免 **Burn** 公式与基础 **Damage** 不同步；**`FormulaContext`**（若仍用于非 Aura 路径）与 **`BattleContext`** 求值勿混用未文档化的假设。

---

## 避免魔法字符串（Tag / Trigger / nameof）

### 设计选择

- **标签**：`Core/Tag.cs` 提供 `Tag.Weapon`、`Tag.Tool`、`Tag.Apparel`、`Tag.Friend`、`Tag.Food`、`Tag.Tech` 等常量，对应「武器」「工具」「服饰」「伙伴」「食物」「科技」。物品的 `Tags = [Tag.Weapon]`、`Tags = [Tag.Friend, Tag.Tech]` 等，条件与效果处用 `Tags.Contains(Tag.Weapon)`，不再手写字符串。
- **触发器**：`Core/Trigger.cs` 提供 `Trigger.UseItem`、`Trigger.BattleStart`。能力定义用 `TriggerName = Trigger.UseItem`；模拟器判断用 `ab.TriggerName == Trigger.BattleStart`、`ab.TriggerName != Trigger.UseItem`。
- **属性名与 key**：AuraDefinition 的 **AttributeName** 与能力/效果中的 key 统一使用 **Key.***（`Core/Key.cs`），如 **Key.CritRatePercent**、**Key.Custom_0**、**Key.Damage**。BattleSimulator 中 `GetInt(Key.CritDamagePercent, ...)` 等同理。

### 小结

新增物品或效果时优先用 `Tag.*`、`Trigger.*`、`nameof(ItemTemplate.属性)`，避免散落中英文魔法字符串；仅 ItemTemplate 尚未暴露的属性（如 Shield、Regen）在 EffectKindKeys 等处保留字面量。

---

## 物品定义简化：DefaultSize/DefaultMinTier、Ability 工厂与 RegisterAll

本节记录物品定义与注册方式的简化经验，便于新增物品时统一风格。

### 尺寸与最低档位由 RegisterAll 按批次设置

- **不在每个物品中写 MinTier/Size**：物品工厂方法（如 `Fang()`）只设置 Name、Desc、Tags、数值、Abilities 等，**不**设置 `MinTier`、`Size`。
- **ItemDatabase**：提供 **`DefaultSize`**、**`DefaultMinTier`**；**`Register(ItemTemplate template)`** 在存入前执行 `template.Size = DefaultSize`、`template.MinTier = DefaultMinTier`。
- **RegisterAll 写法**：先设 `db.DefaultSize = ItemSize.Small`（或 Medium/Large），再按档位设 `db.DefaultMinTier` 并连续 `db.Register(...)`。例如 CommonSmall 先 `DefaultMinTier = Bronze` 注册所有铜物品，再 `DefaultMinTier = Silver` 注册所有银物品；CommonMedium/CommonLarge 仅铜则设一次 Bronze 后注册全部。

### 能力优先级默认 Medium

- **AbilityDefinition.Priority** 默认值为 **`AbilityPriority.Medium`**。仅当能力优先级非 Medium 时在定义中显式写 `Priority = AbilityPriority.High` 等。

### Ability 工厂方法（Core/Ability.cs）

- **单效果 UseItem**：优先用 **`Ability.Damage()`**、**`Shield()`**、**`Heal()`**、**`Burn()`**、**`Poison()`**（默认 trigger=UseItem，可选 priority、condition、additionalCondition、invokeTargetCondition）。
- **加速/减速/冻结**：**`Ability.Haste()`**、**`Slow()`**、**`Freeze()`**（目标默认 SameSide/DifferentSide）；可选 **targetCondition** 完全代替默认、**additionalTargetCondition** 在默认上 And 追加。
- **加属性/减属性**：**`Ability.AddAttribute()`**、**`Ability.ReduceAttribute()`**（目标默认己方 SameSide / 敌方 DifferentSide）；**additionalTargetCondition** 在默认上追加，**targetCondition** 完全代替默认。
- **多效果或特殊**：拆成多条能力，或 **`new AbilityDefinition { ..., Apply = Apply.XXX, ... }`** / 自定义 **`Action<BattleContext>`**。

### 时间属性：秒转毫秒统一为 ToMilliseconds

- **SecondsOrByTier** 仅保留 **`ToMilliseconds()`**（原 `ToFreezeMs`、`ToSlowMs`、`ToHasteMs` 已合并为同一语义）。FreezeSeconds、SlowSeconds、HasteSeconds 的 setter 均调用 `value.ToMilliseconds()` 写入模板内部毫秒列表。

### 小结

新增公共物品时：在对应 Common* 的工厂方法中不写 MinTier/Size；在 RegisterAll 中按尺寸与档位设 DefaultSize/DefaultMinTier 后注册。能力优先用 `Ability.*OnUseItem`，仅非默认优先级或非默认目标条件时传参；时间在定义中用秒，内部通过 ToMilliseconds 转毫秒。详见 **.cursor/rules/item-design.mdc**。

---

## EffectKind 集中映射与策略表（已废弃）

本节所述 EffectKind、EffectKindKeys、GetEffectApplier、CustomEffectHandlers 已在此前重构中移除，改为**委托驱动的 Condition 与 Effect**。当前约定见「Condition 与 Effect 委托化重构」一节。

---

## 暴击伤害与描述分行

### 暴击伤害（CritDamagePercent）

- **属性**：ItemTemplate 增加 `CritDamagePercent`，默认 200（表示 2 倍暴击）。暴击时最终倍率 = `CritDamagePercent / 100`（200 → 2x，400 → 4x）。作用于伤害、灼烧、剧毒、护盾、治疗、生命再生等所有可暴击效果。
- **光环**：利爪等「自身暴击伤害 +100%」使用 **AttributeName = Key.CritDamagePercent**、**Value = Formula.Source(Key.Custom_0)**、**Percent = true**，Custom_0 = 100；公式 `(基础 + Σ固定) × (1 + Σ百分比/100)` 得 200×2=400 即 4 倍。

### Desc 按分号分两行

- **显示**：物品 Desc 中可用分号（`;` 或 `；`）分段。MainWindow 的卡组内/物品池 Tooltip 对 `template.Desc` 按分号 Split 后，每段 trim 再单独做占位符替换与 BuildLineInlines，每段一个 TextBlock，实现两行或多行显示。

---

## 暴击与暴击率（UseSelf、CanCrit、add/reduce 仅对可暴击物品）

### 规则

- **可暴击**：仅「使用**本**物品时」的伤害、护盾、治疗、生命再生、灼烧、剧毒可参与暴击判定；其他触发器触发的效果、使用其他物品时触发的效果均不可暴击。
- **实现**：模拟器 `canCrit = ItemHasAnyCrittableField(item) && ability.Apply != null && ability.ApplyCritMultiplier && ability.UseSelf`。仅 **UseSelf** 为 true 的 UseItem 能力会掷暴击；Override 时若传入 condition 或改为非 UseItem 的 trigger，须将 **UseSelf = false**。

### Override 仅改 trigger 时的 UseSelf

- **漏洞**：若只传 `trigger`（如 `Ability.PoisonSelf.Override(trigger: Trigger.Crit)`）未传 condition，走 `else if (trigger != null && originalTrigger != TriggerName)` 分支，只更新了 Condition，**未**设置 UseSelf = false，导致非 UseItem 能力仍参与暴击。
- **修复**：在该分支内增加：若 `TriggerName != Trigger.UseItem` 则 `UseSelf = false`。见 **AbilityDefinition.Override**。

### add/reduce 暴击率仅对可暴击物品生效

- **问题**：原用 **HasAnyCrittableTag**（六类 Tag 之一）限制加/减暴击率目标；但如舱底蠕虫 S9 有 Poison 会补 Tag.Poison，却无「使用本物品」可暴击能力，不应获得暴击率。
- **Condition.CanCrit**：被评估对象「可参与暴击」统一读取 `ItemState.CanCrit`（运行时判定），语义为：具备可暴击类型 `DerivedTag`（Damage/Burn/Poison/Heal/Shield/Regen）且至少一条能力满足 `TriggerName == Trigger.UseItem && UseSelf && ApplyCritMultiplier`。
- **效果层**：**AddAttributeToCasterSide**、**ReduceAttributeToSide** 中当 `attributeName == Key.CritRatePercent` 时，使用 **Condition.CanCrit** 过滤目标。不修改物品定义即可保证不可暴击物品（如 S9）不会收到暴击率。

---

## 冰锥与冻结、相关修复与约定总结

本节记录「冰锥」物品加入过程中涉及的实现选择与修复，便于后续扩展类似效果（多目标、持续时间、日志与持久化）时对照。

### 冻结（Freeze）与冰锥物品

- **属性设计**：与充能（Charge）一致，**内部存毫秒**，物品定义用**秒**。`ItemTemplate` 提供 `Freeze`（`IntOrByTier` 毫秒）、`FreezeSeconds`（`SecondsOrByTier`：可赋单值或 `new[] { 3.0, 4.0, 5.0, 6.0 }`，内部转毫秒）、`FreezeTargetCount`（冻结目标数量，可单值或按等级）。
- **SecondsOrByTier**：`Core/ItemTemplate.cs` 中结构体，支持从 `double` / `double[]` 隐式转换；秒转毫秒统一用 **`ToMilliseconds()`**（原 ToFreezeMs/ToSlowMs/ToHasteMs 已合并），`FromFirstTierMs(ms)` 用于 getter。定义时写 `FreezeSeconds = new[] { 3.0, 4.0, 5.0, 6.0 }`，符合「物品定义中时间一律用秒」的约定（见 **.cursor/rules/project-conventions.mdc**）。
- **预定义效果**：`Effect.Freeze` 使用模板的 `Freeze`（毫秒）与 `FreezeTargetCount`。目标选择见下节「充能、加速、减速、冻结统一目标选择」；冻结不可暴击；触发「触发冻结」时按**实际目标数**入队（PendingCount）。
- **ItemState**：已有 `FreezeRemainingMs`；`ProcessCooldown` 中 `FreezeRemainingMs > 0` 时不推进冷却；每帧在某一步中减少 `FreezeRemainingMs`（见下）。

### 加速/减速/冻结剩余时间与冷却顺序

- **问题**：若先「减少加速、减速、冻结剩余时间」再「处理冷却」，则冻结最后一帧会少 1 帧：本帧初剩余 50ms 被减为 0，再处理冷却时已不视为冻结，冷却会推进。
- **正确顺序**：**先处理冷却**（冻结状态下不推进），**再**减少加速、减速、冻结剩余时间 50ms。这样冻结最后一帧仍在本帧内阻挡冷却，持续时间足额。设计文档「每一帧的结算顺序」与「帧的结算规则」已同步为：步骤 2 处理冷却，步骤 3 加速/减速/冻结减少。

### 多目标效果与日志

- **extraSuffix**：`IBattleLogSink.OnEffect` 增加可选参数 `string? extraSuffix = null`。冻结、减速、充能、加速、修复、摧毁等多目标效果在应用时收集目标物品名，拼成统一格式 `" →[物品名1、物品名2、物品名3]"` 传入，各 sink 在数值后追加显示。AddAttribute/ReduceAttribute 等按条件给多件物品加减属性也复用同一格式，便于快速看出一条效果命中了哪些目标。
- **时间类效果显示秒**：`EffectLogFormat.FormatEffectValue` 对「充能」「冻结」将毫秒格式化为「N 秒」或「N.F 秒」，与物品描述一致。
- **冻结关键词颜色**：`EffectKeywordFormatting` 中「冻结」使用 `Color.FromRgb(63, 200, 247)`。

### JSON 与 default.json

- **UTF-8 不转义**：`DeckManager` 的 `JsonSerializerOptions` 增加 `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`，保存卡组集时中文等字符正常写入，不再输出 `\uXXXX`。
- **default.json 不覆盖**：构建时 `Data/Decks/*.json` 与 `Data/Decks/item_tests/*.json` 复制到输出目录时**排除根目录的 default.json**（csproj 中 `Exclude="..\..\Data\Decks\default.json"`）；应用启动时若 `default.json` **不存在**才 `NewCollection()` 并 `SaveCollection(defaultPath)` 生成空集，避免每次构建或运行覆盖用户测试用文件。

### 触发器统一与 Condition 自动补全（已重构，见下节）

### 充能、加速、减速、冻结统一目标选择

- **统一逻辑**：四类效果均采用「目标数 + 时间」两属性。目标池 = **仅含冷却时间 > 0 且未销毁的物品**，再按能力的 `TargetCondition`（可选）过滤；**不放回**随机选取至多 `TargetCount` 个；若满足条件的物品数不足则全部选中。触发类（如「触发冻结」）按**实际选中的目标数**入队（PendingCount）。
- **默认 Condition**：减速、冻结默认 `Condition.DifferentSide`（敌方）；充能、加速默认 `Condition.SameSide`（己方）。物品/能力可通过 `AbilityDefinition.TargetCondition` 覆盖（如暗影斗篷用 `RightOfSource` 指定「施放者右侧物品」）。
- **实现**：**`BattleContext.GetTargetIndices` / `GetTargetsFromBothSides`**（**`BattleContext.EffectApply.cs`**）构建候选池并 Fisher-Yates 不放回选取；**`ApplyFreeze`/`ApplySlow`/`ApplyCharge`/`ApplyHaste`** 均调用该逻辑。**`TargetCondition`** 由 **`ExecuteOneEffect`** 注入 **`ctx.TargetCondition`**；**`Apply.Haste`** 读 **`HasteTargetCount`**（默认 1）并传给 **`ctx.ApplyHaste`**。

### 小结

新增「持续时间 + 多目标」类效果时：时间在模板中用秒、内部毫秒；多目标可复用 extraSuffix 与 FormatEffectValue；若有「剩余时间」且影响冷却，须保证**先处理冷却、再减少剩余时间**。目标选择统一为「有冷却 + Condition + 不放回」。触发器与条件见下节「Condition 与 Effect 委托化重构」。

---

## 摧毁（Destroy）与「施加摧毁时」触发器

- **Trigger.Destroy**：`Core/Trigger.cs` 中 `Destroy = "摧毁物品时"`。语义：**任意物品施加摧毁时触发**，实现同 Slow：Condition 判定施加摧毁的物品，InvokeTargetCondition 判定被摧毁的物品；`EnsureTriggerCondition` 默认 `Condition.SameSide`，能力上常用 `SameAsSource` 表示「仅施加摧毁的物品自身」触发。
- **统一摧毁接口**：使用 **Ability.Destroy** + **Effect.DestroyApply**，接口与 Slow 类似；目标要求**未摧毁**（`Condition.NotDestroyed`），**不**要求有冷却。目标默认己方(SameSide)；当 **targetCondition** 限定为敌方（如 **Condition.DifferentSide**）时，**ApplyDestroy** 从敌方阵营选目标并施加摧毁（入队时用敌方 SideIndex）。`targetCondition` / `additionalTargetCondition` 与其余多目标能力一致。`ItemTemplate.DestroyTargetCount` 默认 1，可省略。多目标选取**一律随机**（不放回）；「右侧下一件」等语义用**条件**限定候选池，例如 **Condition.FirstNonDestroyedRightOfSource**（右侧第一个未摧毁物品，可能隔多格），池中至多一个，随机选 1 即得。
- **Condition.FirstNonDestroyedRightOfSource**：被评估对象是能力持有者右侧第一个未摧毁的物品（同侧、ItemIndex > Source.ItemIndex，且中间槽位均已摧毁）。牵引光束用 `Ability.Destroy(additionalTargetCondition: Condition.FirstNonDestroyedRightOfSource)`，不再使用特化 Effect。
- **执行顺序**：「施加摧毁」必须在**将目标标记为 Destroyed 之前**调用 `InvokeTrigger`。实现：`ApplyDestroy` 选目标后对每个目标调用 `OnDestroyApplied(i)`；回调内先 `InvokeTrigger(Trigger.Destroy, ...)`，再 `target.Destroyed = true`。
- **ConditionContext**：与 Slow 相同。被摧毁目标为大型或飞行时用 **`InvokeTargetCondition = Condition.WithTag(Tag.Large) | Condition.InFlight`**。
- **已移除**：`Effect.DestroyNextItemToRightOfCasterApply`、`ChargeSelfApply`（充能统一用 ChargeApply + targetCondition/SameAsSource 与默认 ChargeTargetCount=1）。
- **日志与颜色**：`EffectLogFormat.FormatEffectValue("摧毁"|"修复", value)` 返回空串；`EffectKeywordFormatting` 中「摧毁」rgb(255,50,120)，「修复」rgb(143,252,188)。见 **.cursor/rules/data-and-logging.mdc**。

---

## 物品定义经验：Trigger.Ammo「此物品」、Highest 优先级、左侧语义、动态数值与公式

本节记录近期物品扩展中的易错点与约定，便于与规则文件一致。

### Trigger.Ammo「此物品」弹药耗尽

- **语义**：文案「**此物品**弹药耗尽时」表示仅当**能力持有者自身**消耗弹药且当次耗尽时触发，不是「己方任意弹药物品耗尽」。
- **写法**：除 **additionalCondition: Condition.AmmoDepleted** 外，须显式 **condition: Condition.SameAsSource**（引起触发的物品 = 能力持有者）。否则 Trigger.Ammo 默认 Condition 为 SameSide，会误触发于己方其他弹药物品耗尽。

### 物品文案中「Highest」/「Lowest」与目标选取

- **优先级**：物品描述里的「（Highest）」「（Lowest）」通常指**能力执行优先级**（**AbilityPriority.Highest** / **AbilityPriority.Lowest**），不是「选取优先级最高的目标」。目标选取当前一律随机（不放回），不另做「按优先级排序选目标」。
- **写法**：需要「先于/晚于其他能力执行」时在能力上设 **priority: AbilityPriority.Highest** 等，**不要**新增「按某顺序选目标」的 Condition（如误用「最高位」等条件表达「Highest」会与文案语义不符）。

### 左侧/右侧：相邻与严格一侧

- **约定**：**LeftOfSource** / **RightOfSource** = 仅**相邻**一格（ItemIndex ± 1）；**StrictlyLeftOfSource** / **StrictlyRightOfSource** = **所有**严格在左侧/右侧的槽位。文案「此物品**左侧**的武器」若无「所有」「每有一件」等字样，一般指**相邻左侧**，用 **LeftOfSource**；「此物品左侧**每有一件**」等用 **StrictlyLeftOfSource**。

### 护盾/伤害等「等量于 X」：用光环提供数值，不新增专用 Ability

- **原则**：当效果数值依赖运行时数据（如「护盾等量于己方最大生命值 25%」「护盾等量于敌人灼烧」）时，**不**新增 ShieldPercentOfMaxHp 等专用 Ability/Effect；应**用光环为该属性提供公式值**，配合原有 **Ability.Shield** / **Ability.Damage** 等。
- **做法**：在物品上增加 **AuraDefinition**：**AttributeName** = Key.Shield（或 Key.Damage 等），**Condition** = SameAsSource（作用在自身），**Value** = Formula（如 **Formula.Opp(Key.Burn)**、**RatioUtil.PercentFloor(Formula.Side(Key.MaxHp), 25)**）。能力仍用 **Ability.Shield**（或对应能力），执行时 **GetResolvedValue(Key.Shield)** 会带上光环，得到公式结果。阵营侧读数用 **`Formula.Side(Key.xxx)`** / **`Formula.Opp(Key.xxx)`**（int 下标）。

### RatioUtil 与 Formula 的百分比重载

- **RatioUtil.PercentFloor**：支持 **PercentFloor(Formula valueFormula, int percent)** 与 **PercentFloor(Formula valueFormula, Formula percentFormula)**，后者用于百分比来自字段或公式（如 **Formula.Source(Key.Custom_0)**）。
- **Formula.Apply**：**Formula.Apply(Formula a, Formula b, Func<int,int,int> combine)** 对两公式在同一上下文中求值后合并，供 **RatioUtil.PercentFloor(valueFormula, percentFormula)** 等使用。

---

## 修复（Repair）机制

- **语义**：修复将**已摧毁**的己方物品恢复为未摧毁，并重置其 `CooldownElapsedMs = 0`，使该物品重新进入冷却循环。无时长参数。
- **目标选择**：与充能/加速不同，目标池为**已摧毁**（`it.Destroyed == true`）且满足能力 `TargetCondition` 的物品，**不**要求有冷却时间；默认 `TargetCondition` 为 SameSide。不放回随机选取至多 `RepairTargetCount` 个。
- **实现**：**`BattleContext.ApplyRepair`** 使用 **`GetTargetIndices`**，池子为 **`Destroyed`** + **targetCondition**。**`ApplyRepair`** 对选中目标 **`Destroyed = false`**、**`CooldownElapsedMs = 0`**，并记日志「修复」。
- **模板与效果**：`ItemTemplate.RepairTargetCount`（可单值或按等级，默认 1）；`Effect.Repair` 读该值并调用 `ctx.ApplyRepair(count, ctx.TargetCondition)`。占位符 `{RepairTargetCount}` 由 ItemDescHelper 解析。
- **标签**：新增 `Tag.Tech = "科技"`（`Core/Tag.cs`），用于如废品场维修机器人等科技类物品。
- **日志着色**：`EffectKeywordFormatting` 中「修复」使用 rgb(143,252,188)。

---

## ConditionContext 重构与触发器统一

本节记录 ConditionContext 收敛为四字段、触发器语义与命名统一的重构经验。

### 设计原则与四字段

- **语义**：Condition 表示「战斗时**一个物品**需满足的条件」，计算时可能涉及其他物品；上下文只提供己方/敌方状态与被评估（及参考）物品，**不**为具体场景（如 OnDestroy、UsedTemplate）增加字段。
- **ConditionContext 仅四字段**：`MySide`、`EnemySide`、`Item?`（被评估对象：Condition 时=引起触发的物品，InvokeTargetCondition 时=触发器指向的物品，TargetCondition 时=候选目标；可为 null）、`Source`（能力所属物品，恒非空）。同一方/相邻/右侧等由 Item 与 Source 的 SideIndex/ItemIndex 推导。
- **索引**：`BattleSide.SideIndex`、`ItemState.SideIndex`/`ItemIndex` 在 `BattleSimulator.Run` 中、`BuildSide` 之后统一写入，供 Condition 与调用方使用。
- **扩展性**：新需求通过「调用方传入不同的 Item/Source」或组合 Condition（如 `WithTag(Tag.Large) | InFlight`）满足，避免在 Context 上堆场景专用字段。

### 触发器统一语义与命名

- **统一语义**：Freeze、Slow、Crit、Destroy 均为「**任意物品**施加/造成 xx 时触发」；默认 `Condition.SameSide` 表现为己方，重写 Condition（如 `DifferentSide`）可实现对方触发。与 UseItem（仅己方施放）、BattleStart 一致由 `InvokeTrigger` 统一处理，Condition 评估时 Source=能力持有者、Item=引起触发的物品。
- **命名统一**：触发器常量与 UseItem/Freeze/Slow 保持一致风格：`Trigger.Crit`、`Trigger.Destroy`（不再使用 OnCrit、OnDestroy），便于后续扩展新触发器时命名一致。
- **Destroy 与 Slow 同构**：施加摧毁时用 Condition 判定施加者、InvokeTargetCondition 判定被摧毁物品，context 传 `InvokeTargetItem`（ItemState 引用），无需 DestroyedItemTemplate 等专用字段；被毁目标为大型或飞行用 `InvokeTargetCondition = Condition.WithTag(Tag.Large) | Condition.InFlight`。

---

## Condition 与 Effect 委托化重构

本节记录 Condition / Effect 脱离枚举与 switch、改为委托驱动的重构经验，便于后续扩展触发器条件与效果时对照。

### Condition：委托 + ConditionContext，支持 And 组合

- **移除**：`ConditionKind` 枚举与 `Tag` 字段；原基于 switch 的 `TriggerConditionEvaluator` / `AuraConditionEvaluator`。
- **Core/Condition.cs**：`Condition` 类仅持有一个 `Func<ConditionContext, bool>?` 委托；`ConditionContext` 为只读结构体，仅含 `MySide`、`EnemySide`、`Item?`（被评估对象，可为 null）、`Source`（能力所属物品，非空）；同一方/相邻等由 Item 与 Source 的 SideIndex/ItemIndex 推导。评估时调用 `condition.Evaluate(ctx)`。
- **静态工厂**：`SameAsSource`、`DifferentFromSource`、`SameSide`、`AdjacentToSource`、`RightOfSource`、`LeftOfSource`、`WithTag(tag)`（被评估对象带 tag）、`InFlight`（被评估对象在飞行）；可用 `&` / `|` 组合。能力持有者需满足某条件时在 **AbilityDefinition.SourceCondition** 中填写，评估时 Item=Source=能力持有者，故用 `WithTag(tag)`、`InFlight` 即可表达「本物品带 tag/在飞行」。尺寸 Tag（`Tag.Small`/`Medium`/`Large`）由注册时按 `template.Size` 自动添加。
- **Condition 收敛与 SourceCondition**：Condition 只描述「被评估对象（Item）是否满足」，不区分场景；`WithTag(tag)`、`InFlight` 均仅看 Item，避免 ItemWithTag/SourceWithTag、SourceInFlight 等重复定义。「能力持有者/光环提供者需满足」统一用 **SourceCondition**：**AbilityDefinition.SourceCondition** 与 **AuraDefinition.SourceCondition** 在评估时构造上下文 Item=Source=能力持有者/提供者，复用同一套 Condition（如 `WithTag(Tag.Weapon)`、`InFlight`）。克隆模板时须同时克隆 Condition 与 SourceCondition。
- **克隆**：`Condition.Clone(c)` 在 Core 中提供，复制委托引用，供 BuildSide / ItemDatabase 克隆能力时使用。

### UseOtherItem 默认与显式 Condition：始终叠加己方

- **问题**：若 UseOtherItem 仅在有显式 Condition（如姜饼人 `WithTag(Tag.Tool)`）时直接返回该 Condition，则不会限制「己方」，对方使用工具也会触发己方能力。
- **正确做法**：UseOtherItem **始终**先叠加「己方其他物品」基础条件 `baseSameSideOther = DifferentFromSource & SameSide`。若原 Condition 为 null，则 `return baseSameSideOther`；若非 null，则 `return baseSameSideOther & condition`。这样「使用工具时充能」等显式条件与「仅己方」同时满足。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。

### Effect：脱离 EffectKind，委托驱动（后已合并为 BattleContext + Apply）

- **移除**：`EffectKind`、`GetEffectApplier`、`CustomEffectHandlers`；**EffectDefinition** 与 **`AbilityDefinition.Effects`**（已合并进 **AbilityDefinition**，见「能力与 Effect 合并重构」）。
- **Core**：**`AbilityDefinition.Apply`** 为 **`Action<BattleContext>?`**；预定义在 **`Core/Apply.cs`**。**`BattleContext`**（**`BattleContext.cs` + `BattleContext.EffectApply.cs`**）提供 **GetResolvedValue**、**TargetCondition**、**ApplyFreeze** 等。
- **暴击**：由 **`ability.ApplyCritMultiplier`** 等决定；模拟器在 **ExecuteOneEffect** 前掷骰并传入 **`ctx.IsCrit`** / **`CritMultiplier`**。
- **BattleSimulator**：**`ExecuteOneEffect`**：**`ctx.Rebind`** 后 **`ability.Apply(ctx)`**；**ValueKey** 非空时填入 **`ctx.ResolvedValue`**。

### 暴击日志：按效果区分是否显示「（暴击）」

- **问题**：多效果能力（如毒刺：伤害+减速）暴击时，若整条能力共用 `isCrit`，则不可暴击的减速也会显示「（暴击）」。
- **做法**：`LogEffect(effectName, value, extraSuffix, showCrit)` 增加 `showCrit` 参数；可暴击效果（Damage/Burn/Poison/Shield/Heal/Regen）在委托内传 `showCrit: ctx.IsCrit`，不可暴击效果（Charge/Freeze/Slow/WeaponDamageBonus）传 false 或不传（默认 false）。上下文内部打日志的方法（如 ApplyFreeze/ApplySlow/ChargeCasterItem）固定传 `isCrit: false`。

### 小结

扩展新触发器条件时在 Core 增加 Condition 静态工厂或 `And` 组合；扩展新效果时在 **`Core/Apply.cs`** 增加委托或 **`AbilityDefinition { Apply = ... }`**。历史上 **UseOtherItem** 的 EnsureTriggerCondition 须始终叠加 **SameSide**（已并入 **UseItem + condition** 写法）。

---

## 能力优先级与步骤 8 执行顺序

### 问题

同一帧内可能有多条能力入队（如己方使用裂盾刀、对方姜饼人 UseOtherItem 等）。若步骤 8 按入队顺序遍历 `currentAbilityQueue`，则执行顺序取决于 InvokeTrigger 遍历双方物品的顺序，**与能力的 Priority 无关**，会导致例如裂盾（High）晚于护盾（Low）触发。

### 正确做法

- 步骤 8 在**处理能力队列前**，对 `toProcessAbilities`（current 的拷贝）按**能力优先级**排序。
- 排序主键：`AbilityPriority` 枚举值升序（Immediate → Highest → High → Medium → Low → Lowest），数字小的先执行。
- 同优先级时按 (Owner.SideIndex, Owner.ItemIndex, AbilityIndex) 作为次键，保证顺序稳定、可复现（AbilityQueueEntry.Owner 为 ItemState）。

这样同一帧内触发的多条能力会严格按「高优先级先于低优先级」执行，与设计意图一致。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。

---

## 物品类型 Tag 与机制 Tag（EnsureTypeTags）

- **类型 Tag**：`Core/Tag.cs` 提供 `Tag.Shield`、`Tag.Damage`、`Tag.Burn`、`Tag.Poison`、`Tag.Heal`、`Tag.Regen`，用于判断物品类型及是否可暴击。
- **注册时自动补充**：`ItemDatabase.Register` 在写入模板前调用 **EnsureTypeTags**，规则如下（**不**再根据模板数值属性直接打标）：
  1. **按 AbilityType 打类型 Tag**：遍历 **`template.Abilities`**，用 **`AbilityTypeToTypeTag(a.AbilityType)`** 映射 **DerivedTag**（Damage/Burn/Poison/Heal/Shield/Regen 等）；不再按 **`Apply` 引用与 `Effect.*Apply` 比对**。
  2. **SameAsSource 光环补充**：若模板有光环且条件为 **SameAsSource**，按光环 **AttributeName** 若为上述六类之一也补充对应 Tag（如废品场长枪 Damage=0 由光环提供仍得 Tag.Damage）。
  3. **Tag.Crit**：若模板已具备六类可暴击 Tag 之一，且存在至少一条能力满足 `TriggerName == Trigger.UseItem`、`UseSelf == true`、`ApplyCritMultiplier == true`，则打 `Tag.Crit`。
  4. **Tag.Cooldown**：若任意档位 `CooldownMs > 0`，则打 `Tag.Cooldown`。
  5. **机制 Tag**：根据 **`AbilityType`**（Charge/Freeze/Slow/Haste/Reload/Repair/Destroy 等）打 **DerivedTag**；**StartFlying** 通过 **`effectLogName == "开始飞行"`** 识别。**AddAttribute/ReduceAttribute**、**GainGold** 不参与自动打标。
  6. **保留**：Size→Small/Medium/Large、`AmmoCap > 0`→Tag.Ammo。
- **判断时使用 Tag**：模拟器判断「是否可暴击」用 `ItemHasAnyCrittableField(item)`（看六类 Tag）；裂盾等用 `Condition.WithTag(Tag.Shield)`。类型由 Tag 决定，不受战斗内数值修改影响。

### 经验总结

- **为何用 Apply 而非数值打类型 Tag**：统一以「实际效果类型」为准，与 MechanicTagger 语义一致；避免仅改数值未改能力时类型漂移。
- **为何要按光环补 Tag**：部分物品某属性为 0、完全由光环提供，仅看 Apply 会漏打，故 SameAsSource + AttributeName 仍补六类 Tag。
- **UI**：`ItemUiHelper` 的 `hiddenAutoTags` 包含六类类型 Tag 与 `Tag.Crit`、`Tag.Cooldown`，以及机制 Tag（Charge/Freeze/Slow/Haste/Reload/Repair/Destroy/StartFlying/StopFlying），展示时隐藏以免冗余。

---

## ~~协同先验（Synergy Prior）~~（已移除）

`ItemTemplate` 曾支持 **UpstreamRequirements**、**DownstreamRequirements**、**NeighborPreference**（配合图/队形启发式）；卡组搜索改为贪心流程后已不再使用，相关字段、`Core/Synergy.cs` / `SynergyPrior.cs`、物品中的显式声明及 `docs/vanessa-synergy-prior.txt`、`.cursor/rules/synergy-prior.mdc` 均已删除。物品配合关系由 **Ability / Condition / Tag** 与 **EnsureTypeTags** 表达即可。

---

## BattleSimulator 文件拆分与代码结构

### 约定

除 **ItemDatabase/CommonSmall.cs**、**CommonMedium.cs** 等以数据定义为主的文件外，单文件不宜过长；可将嵌套类、静态辅助拆到同命名空间下的独立源文件。

### BattleSimulator 已拆分出的文件

| 文件 | 职责 |
|------|------|
| **BattleSideDamage.cs** | 静态方法 `ApplyDamageToSide(BattleSide, int, bool)`，护盾吸收与伤害结算。 |
| **BattleContext.EffectApply.cs**（Core） | **`partial BattleContext`**：效果应用（伤害/护盾/冻结/减速/充能/摧毁、属性增减、`EffectAppliedTriggerQueue` 上报等）。 |
| **BattleAuraModifiers.cs** | 静态 **`Accumulate`**，战斗内光环固定/百分比累加（**无** `IAuraContext`）。 |
| **TriggerInvokeContext.cs** | 触发器调用上下文（Multicast、UsedTemplate、InvokeTargetItem）。 |

以上类型均为 `internal`，仅本程序集使用。主逻辑与帧循环保留在 **BattleSimulator.cs**，便于阅读与遵守 **.cursor/rules/battle-simulator-ability-queue.mdc**。

---

## 战斗期模板只读 Name：索引化重构经验（2026-03）

### 目标与落地

- **约束**：战斗过程中仅允许读取 `ItemTemplate.Name`（日志），其他战斗读数全部走 `ItemState`/`BattleContext.GetItemInt`。
- **触发索引**：`Trigger` 改为 **0 基连续编号**，并提供 `Trigger.Count`；Run 初始化构建 `triggerId -> abilities` 索引。
- **光环索引**：Run 初始化构建 `attributeKey -> auras`（及 `AllAuras`），`BattleContext.GetItemInt(item,key)` 在运行时统一叠加光环。
- **Apply 形态**：`AbilityDefinition.Apply` 从 `Action<BattleContext>` 改为 `Action<BattleContext, AbilityDefinition>`，减少把能力字段搬运到 `BattleContext`。
- **触发调用链**：移除 `EffectAppliedTriggerQueue` 绕行；效果施加时通过 `BattleContext.TriggerInvoker` 直接调用 `InvokeTrigger`。

### 250ms 节流与队列：AbilityState 的正确建模

- **全局 AbilityId**：`AbilityDefinition` 在构造时分配全局 `AbilityId`（`Override/Also` 不变）。
- **关键坑**：当 `ItemState.Abilities` 直接引用模板能力（不 clone）时，同一 `AbilityDefinition` 可能被多个物品共享；若 `AbilityId -> AbilityState` 一对一，会发生 owner 覆盖，表现为“有施放日志、无效果日志”。
- **修复原则**：运行态必须按**能力持有者维度**拆开，不能仅按 `AbilityId` 聚合。
  - 实现上使用 `RuntimeAbilityRef(Owner, Ability)`；
  - 队列桶存 `RuntimeAbilityRef`（非 `AbilityQueueEntry`）；
  - `AbilityState` 按 `RuntimeAbilityRef.Id` 维护 `LastTriggerMs/PendingCount/InvokeTargets`。
- **收益**：保留“能力定义不可变且不 clone”的前提，同时避免共享状态串扰。

### 默认值回退语义不可丢

- 从 `Template.GetInt(key, default)` 切到 `ItemState.Attributes[key]` 后，若直接返回属性值，会丢失大量“0 表示未配置”的默认语义（如 `Multicast`、`*TargetCount` 默认应为 1）。
- **必须补回退**：
  - `BattleSide.GetItemInt(item,key,defaultValue)`：当读取值为 0 且 `defaultValue != 0` 时返回默认值；
  - `BattleContext.GetItemInt` 保持同语义；
  - `Apply.*` 在读取能力主值时，需支持 `ability.Value` 作为 `ValueKey` 之外的回退来源（否则常量能力值会丢失）。

### 验收建议

- 先用小铜回归脚本验证主链路（UseItem/伤害/护盾/冻结）再扩展档位。
- 若出现“只施放不生效”，优先检查：
  1) `PendingCount` 是否因默认值回退缺失变为 0；  
  2) `AbilityState` key 是否误用共享 `AbilityId` 而未包含 owner 维度。

---

## 伤害报表与效果类型（吸血、护盾降低、伤害提高）

### 问题：吸血伤害未统计

- **现象**：毒刺等带 **`LifeSteal`** 的物品造成伤害时，**`Apply.Damage`** 为区分展示会以 **「吸血」** 调用 **`LogEffect`**（由 **`ctx.GetResolvedValue(Key.LifeSteal, defaultValue: 0) != 0`** 判断），而 **StatsCollectingSink** 的 **`OnEffect`** 仅对 **`effectKind == "伤害"`** 累加伤害，导致吸血未进报表。
- **修复**：在 **StatsCollectingSink** 的 switch 中，将 **「吸血」** 与 **「伤害」** 同等处理：`case "伤害": case "吸血":` 均执行 `a.Damage += value` 与 `AddSide(caster.SideIndex, damage: value)`。吸血与伤害为同一数值，仅展示不同，统计时均计入伤害。OnEffect 签名为 `(ItemState caster, ...)`。

### 定向效果日志格式统一

- **约定**：定向/多目标效果统一为「[物品名] 效果名 数值 →[目标]」形式，便于报表与日志解析一致。
- **护盾降低**：裂盾刀等减少对方护盾物品 Shield 时，日志用 **「护盾降低」**（不再用「裂盾」），`extraSuffix = " →[" + string.Join("、", targetNames) + "]"`。
- **伤害提高**：举重手套、暗影斗篷等增加武器伤害时，日志用 **「伤害提高」**（不再用「武器伤害提升」），由 `AddWeaponDamageBonusToCasterSide` / `AddWeaponDamageBonusToCasterSideItem` 内建 targetNames 并调用 `LogEffect("伤害提高", value, extraSuffix)`。报表统计不依赖 effectKind 为「伤害提高」（该效果不进入 Damage 累加），仅伤害类效果（伤害/吸血）计入 Damage。

### 小结

新增或修改 `effectKind` 时，若该效果**实质为造成伤害**（如吸血），需在 **StatsCollectingSink.OnEffect** 中与「伤害」一并计入 `a.Damage` 与 `AddSide(damage)`；定向效果命名与格式见 **data-and-logging.mdc**。

---

## UseOtherItem 右侧条件与加速效果

本节记录「暗影斗篷」等「使用此物品右侧物品时」触发、以及加速（Haste）效果的实现经验。

### Condition.LeftOfSource 与 RightOfSource

- **RightOfSource**（Item 在 Source 右侧，即 `Item.ItemIndex == Source.ItemIndex + 1`）。UseItem 触发时 Source=能力持有者、Item=被使用物品，故「被使用物品在能力持有者右侧」= SameSide & RightOfSource；目标选择时「施放者右侧物品」亦用 RightOfSource（Source=施放者、Item=候选）。
- **LeftOfSource**（Item 在 Source 左侧），用于目标选择等。
- **用途**：暗影斗篷等 `TriggerName = Trigger.UseItem`、`Condition = DifferentFromSource & SameSide & RightOfSource`，能力内用 `Effect.Haste` 并设 `TargetCondition = Condition.RightOfSource`、`HasteTargetCount = 1`，对右侧有冷却物品施加加速。

### 加速（Haste）与 Effect.Haste

- **与减速对称**：`ItemState` 已有 `HasteRemainingMs`；每帧先处理冷却（加速时 `advanceMs *= 2`），再在步骤 3 中 `HasteRemainingMs = Math.Max(0, HasteRemainingMs - FrameMs)`。
- **模板**：`ItemTemplate` 提供 `Haste`（毫秒）、`HasteSeconds`（秒，可单值或按等级）、`HasteTargetCount`（目标数，默认 1）；物品定义用 `HasteSeconds = new[] { 1.0, 2.0, 3.0, 4.0 }`。
- **效果**：**Effect.Haste**（已移除 Effect.Accelerate）从模板读 `Haste`、`HasteTargetCount`（默认 1），调用 `ctx.ApplyHaste(hasteMs, count, ctx.TargetCondition)`；目标由统一逻辑选取（己方有冷却 + 满足能力 `TargetCondition`，默认 SameSide）。暗影斗篷在能力上设 `TargetCondition = Condition.RightOfSource`、`HasteTargetCount = 1`，不在 Effect 内写死。
- **日志与 UI**：`EffectLogFormat` 对「加速」将毫秒格式化为「N 秒」；`EffectKeywordFormatting` 中「加速」颜色 `rgb(0,236,195)`；`ItemDescHelper` 支持 `{HasteSeconds}` → `Haste`。

### 单目标武器伤害提高

- **AddWeaponDamageBonusToCasterSideItem(value, targetItemIndexOnCasterSide)**：仅对己方指定下标物品生效；若该物品带 `Tag.Weapon` 则 `Damage.Add(value)` 并 `LogEffect("伤害提高", value, " →[目标名]")`，非武器则不操作、不记日志。
- **Effect.WeaponDamageBonusToRightItem(ValueKey)**：从 ValueKey 取值，对 `ctx.Item.ItemIndex + 1` 调用上述方法，用于暗影斗篷「若右侧为武器则伤害提高」。

---

## 多版本物品与 GUI 版本切换

### 命名与数据约定

- **最新版本**：模板名不带后缀（如 `獠牙`），注册在数据库中。
- **历史版本**：模板名 `[物品名]_Sx`（如 `獠牙_S10`、`獠牙_S7`），x 为赛季号；同一基名可有多条，通过多次 `Register` 不同名称的模板即可。`GetAllNames()` 仍返回全部名称，不影响现有调用方。
- **图片**：所有版本（含 `_Sx`）均用**基名**（去掉 `_Sx` 后缀）定位图片，即 `pictures/png/<基名>.png`。`ItemImageHelper.GetBaseNameForImage` 与 `ItemDatabase.GetBaseName` 规则一致。
- **版本循环顺序**：最新 → 较高赛季 → 较低赛季 → 最新（例如最新、S10、S7 → 最新→S10→S7→最新）。

### ItemDatabase API

- **GetBaseName(string name)**：去掉末尾 `_S\d+` 得到基名；无后缀则返回原名。
- **IsHistoricalVersion(string name)**：匹配 `_S\d+$` 视为历史版本。
- **GetLatestOnlyNames()**：返回所有非历史版本名称，供 GUI 默认物品池使用（筛选后再按尺寸/档位/英雄过滤）。
- **GetVersionCycle(string baseName)**：返回该基名对应的所有模板名，顺序为无后缀（若存在）在前，其余按 `_S` 后数字降序；单版本时返回单元素列表。

### GUI 行为

- **物品池**：绑定到 `ItemPoolEntryViewModel`（BaseName + DisplayName）。默认 DisplayName = BaseName；右键循环 DisplayName 到下一版本，左键拖拽 payload 为 DisplayName（可历史版本）；左键点击任何其他位置且未发生「从池拖入卡组」的 Drop 时，将所有池项 DisplayName 重置为 BaseName（通过 `_poolDropInThisGesture` 在 `DeckGrid_Drop` 收到来自池的 Text 时置 true，`EditorPanel_PreviewMouseLeftButtonUp` 中若为 false 则重置并清标志）。
- **卡组内**：右键物品 Border 循环 `row.ItemName` 到下一版本，循环后调用 `ApplyOverridableDefaultsForTier` 与 `UpdateSlotSummary`。卡组保存/加载仍用 `DeckSlotEntry.ItemName`，历史版本名会写入 JSON；对战解析用 `entry.ItemName` 查库即可。

### 卡组内版本切换与 MinTier

- **问题**：同一基名可有不同 MinTier 的版本（如舱底蠕虫最新=铜、_S10/_S9=银）。若卡组内右键切换时在所有版本间循环，铜槽会切到银版本，出现「铜槽里显示银物品」等错误。
- **约定**：卡组内版本切换**仅在与当前槽位 MinTier 一致的版本间**循环。即先 `GetVersionCycle(baseName)`，再过滤为 `template.MinTier == row.Tier` 的版本列表；仅当过滤后多于 1 个版本时才做 (idx+1)%count 切换。物品池内右键仍为全版本循环（池不绑定槽位档位）。

### 经验小结

- 新增多版本时只需按命名规则 Register，无需改 Deck/模拟器结构。GUI 默认只显示最新、池与卡组支持右键循环，图片统一用基名可避免重复资源。卡组内切换须按 MinTier 过滤版本列表。

---

## GUI 启动时上次打开的卡组集

- **存储**：路径写入 **Data/last-collection.txt**（一行、完整路径）。App 提供 **GetLastCollectionPath()** / **SetLastCollectionPath(path)**。
- **启动**：MainWindow 构造时先尝试打开 last-collection 中的路径；若无记录或文件不存在或打开失败，再尝试 **default.json**；若 default 也不存在则新建空卡组集并保存为 default。成功打开后调用 SetLastCollectionPath 更新记录。
- **更新时机**：打开卡组集、新建并另存为、保存卡组集时弹出另存为并保存成功时，均调用 SetLastCollectionPath。

---

## 开发与提交

- **Git 提交信息**：须符合 **.cursor/rules/git-commit-format.mdc**。格式为 `<type>(<scope>): <subject>`，主题用中文、句末无句号；type 取 `feat` / `fix` / `refactor` / `docs` / `chore`，scope 可选（如 `gui`、`sim`、`item-db`）。提交前可在 `docs/changelog.md` 中补充分条说明。
