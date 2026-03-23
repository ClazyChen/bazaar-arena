# ItemState 字段盘点（重构阶段 1）

本文用于 `ItemState` 字段梳理，范围仅覆盖当前 `BattleSimulator` 热路径。

## 1) 运行时状态字段（`ItemState`）

这些字段已迁移为 `ItemState` 直接字段，不再通过 `ItemTemplate` 的 Key 字典读写：

- `Template`
- `SideIndex`
- `ItemIndex`
- `Tier`
- `CooldownElapsedMs`
- `HasteRemainingMs`
- `SlowRemainingMs`
- `FreezeRemainingMs`
- `InFlight`
- `Destroyed`
- `AmmoRemaining`
- `LastTriggerMsByAbility[]`（替代 `LastTriggerMs_{abilityIndex}`）
- `CritTimeMs`
- `IsCritThisUse`
- `CritDamagePercentThisUse`

## 2) 运行中仍通过 ItemTemplate 读取的数值字段

以下字段目前仍由模板（含光环）读取，后续可继续下沉到 RuntimeState：

- `Key.CooldownMs`
- `Key.Multicast`
- `Key.AmmoCap`
- `Key.CritRatePercent`
- `Key.CritDamagePercent`
- `Key.PercentFreezeReduction`
- `ability.ValueKey` 对应属性（如 `Damage/Shield/Heal/Burn/Poison/Custom_0`）
- `attributeName` 动态属性（`AddAttribute/ReduceAttribute` 路径）

## 3) 结论（本步重构边界）

- 本次仅完成“物品运行态字段直存化”，不处理全量属性系统改造。
- `ItemTemplate` 仍承担静态定义 + 公式/光环读值职责。
- 下一步可在不改语义前提下，把热点固定键（如 `CooldownMs`/`AmmoCap`）映射到索引化 RuntimeState。

