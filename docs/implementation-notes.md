# 实现笔记与错误经验

本文档记录开发过程中容易踩坑的实现细节与修正经验，便于后续维护与代码审查时对照。

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
- **步骤（8）结算完成后**：再将 `nextAbilityQueue` 整体移入 `currentAbilityQueue`，清空 `nextAbilityQueue`，供下一轮循环使用。

这样步骤（7）本帧加入的条目会留在 `nextAbilityQueue`，直到本帧结束才被移入 `currentAbilityQueue`，在下一帧的步骤（8）才被处理。

### 小结

双队列（current / next）时，「下一帧队列 → 当前帧队列」的移动必须发生在**本帧能力全部处理完毕之后**，不能发生在步骤（8）开头，否则会破坏帧边界语义。

---

## 物品模板：按等级属性与集合表达式

### 设计选择

- **只保留 `_intsByTier`**：扩展属性统一用 `Dictionary<string, List<int>>` 存储；单值（如 `CooldownMs = 2000`）存为长度为 1 的列表，读取时 `list.Count == 1` 则对所有 tier 返回该值。不再维护单独的 `_ints`，避免两套存储与同步问题。
- **IntOrByTier**：在对象初始器中同时支持单值（`Damage = 40`）与按等级列表（`Damage = [25, 35, 45, 55]`）。通过隐式转换（`int` → 单元素列表，`int[]`/`List<int>` → 列表）和属性 setter 写入 `_intsByTier`。
- **Key 不对外暴露**：`KeyDamage`、`KeyCooldownMs` 等改为 `private const`，不提供 GetKey 等 API。调用方（如 BattleItemState、BattleSimulator）直接用字符串字面量 `"Damage"`、`"CooldownMs"` 调用 `GetInt(key, tier)`，简化依赖。
- **未定义即默认值**：不使用 `ContainsKey`；`GetInt(key, tier, defaultValue)` 在 key 不存在时直接返回默认值。效果数值解析时若 `GetInt(key, tier)` 为 0 则用 `eff.Value` 作为 fallback。

### 集合表达式直接赋值（Damage = [25, 35, 45, 55]）

- **问题**：集合表达式 `[25, 35, 45, 55]` 无法直接赋给非 BCL 类型，会报「该类型不可构造」或需写 `(IntOrByTier)(int[])[...]`，不够简洁。
- **做法**：使用 C# 12 的 `[CollectionBuilder]`，让自定义类型成为集合表达式的目标类型：
  1. 在类型上标记 `[CollectionBuilder(typeof(IntOrByTier), "Create")]`。
  2. 实现静态方法 `Create(ReadOnlySpan<int> values)`，在方法内构造并返回该类型。
  3. **实现 `IEnumerable<int>` 并暴露 `GetEnumerator()`**：否则编译器报 CS9188「没有元素类型」。编译器通过枚举器推断集合的元素类型，非泛型自定义类型必须提供此信息。
- **依赖**：`CollectionBuilderAttribute` 在 .NET 9+ 的 `System.Runtime.CompilerServices` 中；若目标框架低于 .NET 9，需自行声明同名 attribute。

### 小结

按等级属性用单字典 + 列表长度区分单值/多值；初始器用 IntOrByTier 统一写法；对外用字符串 key、默认值 0 简化逻辑。需要 `X = [a,b,c]` 时用 CollectionBuilder + IEnumerable 让自定义类型支持集合表达式。

---

## 物品与效果扩展（战斗内修改、自定义效果）

### IntOrByTier.Add(delta) 与战斗内模板

- **IntOrByTier**：增加 `Add(int delta)`，对每个 tier 的值加 delta 并返回新的 IntOrByTier；空列表时返回 `[delta, delta, delta, delta]`。用于「本场战斗内增加某数值」类效果（如举重手套给武器加伤害）。
- **战斗内模板**：`BuildSide` 时每个卡组槽位从 resolver 取 template 后**克隆**一份（新 ItemTemplate + SetIntsByTier），再创建 `BattleItemState(clone, tier)`。因此 `BattleItemState.Template` 为当局专用，可直接修改，例如 `wi.Template.Damage = wi.Template.Damage.Add(value)`，无需额外 DamageBonus 字段。
- **Damage 的 getter**：若需对 Damage 做「整体加 delta」并写回，模板的 Damage 属性 get 需返回**完整按等级列表**（如通过 GetIntOrByTier），否则 get 只返回单值会丢失其他 tier。

### 自定义效果（EffectKind.Other）与 ValueKey

- **EffectDefinition**：`ValueKey`（如 `"Custom_0"`）表示「未设 ValueResolver 时用 template.GetInt(ValueKey, tier) 取值」；`ResolveValue(template, tier, defaultKey)` 统一顺序：ValueResolver → ValueKey → defaultKey → Value。新增物品时优先写 `ValueKey = "Custom_0"`，避免手写 lambda。
- **CustomEffectHandlers**：自定义效果实现集中在 `BattleSimulator/CustomEffectHandlers.cs`，按 `CustomEffectId` 注册；BattleSimulator 仅调用 `CustomEffectHandlers.TryExecute(...)`，不在此文件内堆具体逻辑。
- **预定义**：无专用属性的自定义效果可放在 `Core/Effect.cs`，如 `Effect.WeaponDamageBonus(ValueKey = "Custom_0")`，物品侧写 `Effects = [Effect.WeaponDamageBonus(ValueKey: "Custom_0")]`。详见 **.cursor/rules/item-design.mdc**。

---

## 光环（Aura）与属性读取

### 使用时机与集成方式

- **仅战斗内**：光环在「读取属性」时生效；局外/UI（ItemDescHelper、MainWindow）直接调用 `ItemTemplate.GetInt(key, tier)`，不传上下文，不参与光环。
- **集成到 GetInt**：`ItemTemplate.GetInt(key, tier, defaultValue, IAuraContext? context)`：当 `context != null` 时，先取基础值再按公式叠加光环：`最终值 = (基础 + Σ 固定) × (1 + Σ 百分比/100)`，多光环的固定与百分比均为加算；被摧毁的物品不提供光环。
- **IAuraContext**：在 Core 中定义，仅提供 `GetAuraModifiers(attributeName, out fixedSum, out percentSum)`。BattleSimulator 实现为 `BattleAuraContext(side, targetItemIndex)`，在 `GetAuraModifiers` 内遍历己方未摧毁物品的 `Template.Auras`，按条件谓词与属性名累加。

### 光环数据与条件

- **AuraDefinition**（Core）：`AttributeName`、`Condition`（`AuraConditionKind`）、`FixedValueKey`、`PercentValueKey`。条件首期支持 `AdjacentToSource`（`|sourceIndex - targetItemIndex| == 1`）；BuildSide 与 ItemDatabase 克隆模板时对 Auras 做深拷贝（逐个 `new AuraDefinition`）。
- **战斗内读属性**：需要光环时传入 context，例如暴击率：`item.Template.GetInt("CritRatePercent", item.Tier, 0, auraContext)`；模拟器在能力队列处理处按当前 (side, itemIndex) 创建 `BattleAuraContext` 并传入。

### 描述占位符：前缀/后缀

- **语法**：大括号内可为 `{前缀Key后缀}`，前缀/后缀为紧挨 key 两侧的「非字母、非数字、非下划线」字符，key 为字母/数字/下划线（如 `{+Custom_0%}` → key=Custom_0，前缀=+，后缀=%）。
- **替换与样式**：单 tier 替换为 `前缀+数值+后缀`；全 tier 为 `前缀+值1+后缀 » 前缀+值2+后缀 » …`。前缀与后缀随数值一起纳入 valueRanges，加粗与 tier 颜色作用于整段（如 "+3%" 整体高亮）。
- **实现**：`ItemDescHelper` 用正则解析占位符，不再维护固定 Placeholders 列表；`{Cooldown}` 仍映射为 key `CooldownMs` 并按秒格式化。

### 暴击与日志、关键词样式

- **暴击日志**：`IBattleLogSink.OnEffect(..., bool isCrit = false)`；当 `isCrit` 为 true 时，Console/TextBox/File 等 sink 在效果行末尾追加「 （暴击）」。
- **关键词着色**：`EffectKeywordFormatting` 中「伤害」「灼烧」「暴击」「暴击率」等仅修改颜色，**不加粗**；加粗仅用于物品名称和嵌入的数值区段（由 ItemDescHelper 的 valueRanges 控制）。
