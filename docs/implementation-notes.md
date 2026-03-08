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
