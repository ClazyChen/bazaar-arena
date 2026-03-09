using BazaarArena.Core;
using BazaarArena.ItemDatabase;

namespace BazaarArena.BattleSimulator;

/// <summary>对战模拟器：按帧结算规则运行，输出分级日志。</summary>
public class BattleSimulator
{
    private const int FrameMs = 50;
    private const int TriggerIntervalMs = 250;
    private const int MinCooldownMs = 1000;
    private const int SandstormStartMs = 30_000;
    private const int SandstormEndMs = 120_000;
    private const int BurnTickIntervalMs = 500;
    private const int PoisonRegenTickIntervalMs = 1000;

    /// <summary>根据玩家等级返回默认生命上限（来自 levelups.json 映射，1 级 300，之后按 JSON 累加）。</summary>
    public static int DefaultMaxHpForLevel(int level) => LevelUpTable.GetMaxHp(level);

    /// <summary>运行一场对战，返回胜方（0 或 1）或 -1 表示平局。</summary>
    public int Run(
        Deck deck1,
        Deck deck2,
        IItemTemplateResolver resolver,
        IBattleLogSink logSink,
        BattleLogLevel logLevel)
    {
        var side0 = BuildSide(deck1, resolver);
        var side1 = BuildSide(deck2, resolver);
        if (side0 == null || side1 == null)
            throw new ArgumentException("卡组中包含未知物品，无法构建战斗状态。");

        int timeMs = 0;
        List<(int SideIndex, int ItemIndex)> castQueue = [];
        List<AbilityQueueEntry> currentAbilityQueue = [];
        List<AbilityQueueEntry> nextAbilityQueue = [];

        // 沙尘暴状态：设计文档 30s 开始，首次 tick 300ms，然后间隔递减 20ms 至 140ms 后改为伤害+2，120s 结束
        int sandstormNextTickMs = SandstormStartMs;
        int sandstormIntervalMs = 300;
        int sandstormDamage = 1;

        for (int frame = 0; ; frame++, timeMs += FrameMs)
        {
            if (logLevel == BattleLogLevel.Detailed)
                logSink.OnFrameStart(timeMs, frame);
            logSink.OnHpSnapshot(timeMs, side0.Hp, side1.Hp);

            // 1. 第 0 帧触发「战斗开始」：统一 invoke 入队，仅 Immediate 入 current、其余入 next，步骤 8 再执行
            if (frame == 0)
                InvokeTrigger(Trigger.BattleStart, -1, -1, null, 0, side0, side1, currentAbilityQueue, nextAbilityQueue);

            // 2. 处理冷却，充能完成则加入施放队列（冻结时冷却不推进）
            castQueue.Clear();
            ProcessCooldown(side0, 0, timeMs, castQueue);
            ProcessCooldown(side1, 1, timeMs, castQueue);

            // 3. 加速、减速、冻结剩余时间减少 50ms（放在冷却之后，保证持续时间足额）
            foreach (var side in new[] { side0, side1 })
            {
                foreach (var item in side.Items)
                {
                    if (item.HasteRemainingMs > 0) item.HasteRemainingMs = Math.Max(0, item.HasteRemainingMs - FrameMs);
                    if (item.SlowRemainingMs > 0) item.SlowRemainingMs = Math.Max(0, item.SlowRemainingMs - FrameMs);
                    if (item.FreezeRemainingMs > 0) item.FreezeRemainingMs = Math.Max(0, item.FreezeRemainingMs - FrameMs);
                }
            }

            // 4. 1000ms 倍数：剧毒、生命再生
            if (timeMs > 0 && timeMs % PoisonRegenTickIntervalMs == 0)
            {
                SettlePoison(side0, side1, 0, logSink, timeMs);
                SettlePoison(side1, side0, 1, logSink, timeMs);
                SettleRegen(side0, 0, logSink, timeMs);
                SettleRegen(side1, 1, logSink, timeMs);
            }

            // 5. 500ms 倍数：灼烧
            if (timeMs > 0 && timeMs % BurnTickIntervalMs == 0)
            {
                SettleBurn(side0, side1, 0, logSink, timeMs);
                SettleBurn(side1, side0, 1, logSink, timeMs);
            }

            // 6. 沙尘暴
            if (timeMs >= SandstormStartMs && timeMs < SandstormEndMs && timeMs >= sandstormNextTickMs)
            {
                ApplySandstorm(side0, sandstormDamage, logSink, timeMs);
                ApplySandstorm(side1, sandstormDamage, logSink, timeMs);
                logSink.OnSandstormTick(sandstormDamage, timeMs);
                if (sandstormIntervalMs > 140)
                {
                    sandstormIntervalMs -= 20;
                    if (sandstormIntervalMs < 140) sandstormIntervalMs = 140;
                }
                else
                    sandstormDamage += 2;
                sandstormNextTickMs = timeMs + sandstormIntervalMs;
            }

            // 7-8. 施放队列产生的能力加入 nextAbilityQueue（下一帧才处理）；步骤 8 只处理 currentAbilityQueue；延后/未消耗的入 nextAbilityQueue；仅步骤 11 将 nextAbilityQueue 移入 currentAbilityQueue
            do
            {
                var toProcess = new List<(int, int)>(castQueue);
                castQueue.Clear();
                // 7. 遍历本轮施放队列，用触发器调用方式加入能力队列（先合并 current 再 next，都没有则入 next；仅 Immediate 入 current）
                foreach (var (sideIdx, itemIdx) in toProcess)
                {
                    var side = sideIdx == 0 ? side0 : side1;
                    var item = side.Items[itemIdx];
                    if (item.GetAmmoCap() > 0)
                        item.AmmoRemaining--;
                    logSink.OnCast(sideIdx, itemIdx, item.Template.Name, timeMs);
                    int multicast = item.GetMulticast();
                    InvokeTrigger(Trigger.UseItem, sideIdx, itemIdx, new TriggerInvokeContext { Multicast = multicast }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                    InvokeTrigger(Trigger.UseOtherItem, sideIdx, itemIdx, new TriggerInvokeContext { UsedTemplate = item.Template }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                }

                // 8. 处理能力队列（只处理 currentAbilityQueue）；延后或未消耗的加入 nextAbilityQueue；充能导致物品满时加入 castQueue
                var toProcessAbilities = new List<AbilityQueueEntry>(currentAbilityQueue);
                currentAbilityQueue.Clear();
                foreach (var entry in toProcessAbilities)
                {
                    if (timeMs - entry.LastTriggerMs < TriggerIntervalMs)
                    {
                        nextAbilityQueue.Add(entry);
                        continue;
                    }
                    var side = entry.SideIndex == 0 ? side0 : side1;
                    var opp = entry.SideIndex == 0 ? side1 : side0;
                    var item = side.Items[entry.ItemIndex];
                    item.LastTriggerMsByAbility[entry.AbilityIndex] = timeMs;
                    entry.LastTriggerMs = timeMs;
                    var ability = item.Template.Abilities[entry.AbilityIndex];
                    bool canCrit = ability.Effects.Any(e => IsCrittableEffect(e.Kind));
                    bool isCrit = false;
                    int critDamagePercent = 200;
                    var auraContext = new BattleAuraContext(side, entry.ItemIndex);
                    int critRate = item.Template.GetInt(nameof(ItemTemplate.CritRatePercent), item.Tier, 0, auraContext);
                    if (canCrit && critRate > 0 && Random.Shared.Next(100) < critRate)
                    {
                        isCrit = true;
                        critDamagePercent = item.Template.GetInt(nameof(ItemTemplate.CritDamagePercent), item.Tier, 200, auraContext);
                    }
                    ExecuteOneEffect(entry.SideIndex, entry.ItemIndex, ability, isCrit, critDamagePercent, side0, side1, timeMs, logSink, castQueue, currentAbilityQueue, nextAbilityQueue);
                    entry.PendingCount--;
                    if (entry.PendingCount > 0)
                        nextAbilityQueue.Add(entry);
                }
            } while (castQueue.Count > 0);

            // 11. 能力队列更新到下一帧（移入 currentAbilityQueue，供下一帧步骤 8 处理）
            currentAbilityQueue.Clear();
            foreach (var e in nextAbilityQueue) currentAbilityQueue.Add(e);
            nextAbilityQueue.Clear();

            // 9. 胜负判定（先输出本帧结算后的最终生命，再通知结果）
            bool dead0 = side0.Hp <= 0;
            bool dead1 = side1.Hp <= 0;
            if (dead0 && dead1)
            {
                logSink.OnHpSnapshot(timeMs, side0.Hp, side1.Hp);
                logSink.OnResult(-1, timeMs, true);
                return -1;
            }
            if (dead1)
            {
                logSink.OnHpSnapshot(timeMs, side0.Hp, side1.Hp);
                logSink.OnResult(0, timeMs, false);
                return 0;
            }
            if (dead0)
            {
                logSink.OnHpSnapshot(timeMs, side0.Hp, side1.Hp);
                logSink.OnResult(1, timeMs, false);
                return 1;
            }

            // 10. 沙尘暴 120s 结束判平局
            if (timeMs >= SandstormEndMs)
            {
                logSink.OnHpSnapshot(timeMs, side0.Hp, side1.Hp);
                logSink.OnResult(-1, timeMs, true);
                return -1;
            }
        }
    }

    private static bool IsCrittableEffect(EffectKind k) => k != EffectKind.Other && k != EffectKind.Charge && k != EffectKind.Freeze;

    /// <summary>战斗内光环上下文：持有己方与目标物品下标，在 GetAuraModifiers 中遍历己方未摧毁物品的光环并累加。</summary>
    private sealed class BattleAuraContext(BattleSide side, int targetItemIndex) : IAuraContext
    {
        public void GetAuraModifiers(string attributeName, out int fixedSum, out int percentSum)
        {
            fixedSum = 0;
            percentSum = 0;
            for (int i = 0; i < side.Items.Count; i++)
            {
                var source = side.Items[i];
                if (source.Destroyed) continue;
                foreach (var aura in source.Template.Auras)
                {
                    if (aura.AttributeName != attributeName) continue;
                    if (!AuraConditionEvaluator.Evaluate(aura.Condition, side, i, targetItemIndex)) continue;
                    if (!string.IsNullOrEmpty(aura.FixedValueKey))
                        fixedSum += source.Template.GetInt(aura.FixedValueKey, source.Tier, 0);
                    if (!string.IsNullOrEmpty(aura.PercentValueKey))
                        percentSum += source.Template.GetInt(aura.PercentValueKey, source.Tier, 0);
                }
            }
        }
    }

    /// <summary>光环条件谓词：根据来源与目标物品下标判断是否生效。</summary>
    private static class AuraConditionEvaluator
    {
        public static bool Evaluate(Condition? condition, BattleSide side, int sourceItemIndex, int targetItemIndex)
        {
            if (condition == null) return true;
            return condition.Kind switch
            {
                ConditionKind.SameAsSource => sourceItemIndex == targetItemIndex,
                ConditionKind.DifferentFromSource => sourceItemIndex != targetItemIndex,
                ConditionKind.AdjacentToSource => Math.Abs(sourceItemIndex - targetItemIndex) == 1,
                ConditionKind.WithTag => side.Items[targetItemIndex].Template.Tags.Contains(condition.Tag ?? ""),
                _ => false,
            };
        }
    }

    /// <summary>触发器调用上下文：传入 InvokeTrigger，用于条件判断与 PendingCount。</summary>
    private sealed class TriggerInvokeContext
    {
        public int? Multicast { get; init; }
        public ItemTemplate? UsedTemplate { get; init; }
    }

    /// <summary>统一触发器条件评估：根据来源、候选与上下文判断能力是否触发。UseOtherItem 时始终排除来源物品（再叠加 WithTag 等）。</summary>
    private static class TriggerConditionEvaluator
    {
        public static bool Evaluate(string triggerName, Condition? condition, int candidateSide, int candidateItem, int sourceSide, int sourceItem, TriggerInvokeContext? context)
        {
            if (triggerName == Trigger.UseOtherItem && sourceSide >= 0 && sourceItem >= 0 && candidateSide == sourceSide && candidateItem == sourceItem)
                return false;
            if (condition == null) return true;
            return condition.Kind switch
            {
                ConditionKind.SameAsSource => sourceSide >= 0 && sourceItem >= 0 && candidateSide == sourceSide && candidateItem == sourceItem,
                ConditionKind.DifferentFromSource => sourceSide < 0 || sourceItem < 0 || candidateSide != sourceSide || candidateItem != sourceItem,
                ConditionKind.WithTag => context?.UsedTemplate?.Tags.Contains(condition.Tag ?? "") ?? false,
                ConditionKind.AdjacentToSource => sourceSide >= 0 && sourceItem >= 0 && candidateSide == sourceSide && Math.Abs(candidateItem - sourceItem) == 1,
                _ => false,
            };
        }
    }

    private static BattleSide? BuildSide(Deck deck, IItemTemplateResolver resolver)
    {
        var side = new BattleSide
        {
            MaxHp = deck.PlayerOverrides?.GetValueOrDefault("MaxHp", LevelUpTable.GetMaxHp(deck.PlayerLevel)) ?? LevelUpTable.GetMaxHp(deck.PlayerLevel),
            Shield = deck.PlayerOverrides?.GetValueOrDefault("Shield", 0) ?? 0,
            Regen = deck.PlayerOverrides?.GetValueOrDefault("Regen", 0) ?? 0,
        };
        side.Hp = side.MaxHp;
        foreach (var entry in deck.Slots)
        {
            var t = resolver.GetTemplate(entry.ItemName);
            if (t == null) return null;
            var clone = new ItemTemplate
            {
                Name = t.Name,
                Desc = t.Desc,
                MinTier = t.MinTier,
                Size = t.Size,
                Tags = [..t.Tags],
                Abilities = t.Abilities.Select(a => new AbilityDefinition
                {
                    TriggerName = a.TriggerName,
                    Priority = a.Priority,
                    Condition = EnsureTriggerCondition(a.TriggerName, CloneCondition(a.Condition)),
                    Effects = a.Effects.Select(e => new EffectDefinition { Kind = e.Kind, Value = e.Value, ValueResolver = e.ValueResolver, ValueKey = e.ValueKey, CustomEffectId = e.CustomEffectId }).ToList(),
                }).ToList(),
                Auras = t.Auras.Select(a => new AuraDefinition { AttributeName = a.AttributeName, Condition = CloneCondition(a.Condition), FixedValueKey = a.FixedValueKey, PercentValueKey = a.PercentValueKey }).ToList(),
            };
            clone.SetIntsByTier(t.GetIntsByTierSnapshot());
            if (entry.Overrides != null)
            {
                foreach (var kv in entry.Overrides)
                    clone.SetInt(kv.Key, kv.Value);
            }
            side.Items.Add(new BattleItemState(clone, entry.Tier));
        }
        return side;
    }

    private static Condition? CloneCondition(Condition? c) =>
        c == null ? null : new Condition { Kind = c.Kind, Tag = c.Tag };

    /// <summary>UseItem / UseOtherItem 生成时自动补 Condition：UseItem → SameAsSource，UseOtherItem → DifferentFromSource（仅当原 Condition 为 null 时）。</summary>
    private static Condition? EnsureTriggerCondition(string triggerName, Condition? condition)
    {
        if (condition != null) return condition;
        if (triggerName == Trigger.UseItem) return Condition.SameAsSource;
        if (triggerName == Trigger.UseOtherItem) return Condition.DifferentFromSource;
        return null;
    }

    private static void ProcessCooldown(BattleSide side, int sideIndex, int timeMs, List<(int, int)> castQueue)
    {
        for (int i = 0; i < side.Items.Count; i++)
        {
            var item = side.Items[i];
            if (item.Destroyed) continue;
            int cooldownMs = item.GetCooldownMs();
            if (cooldownMs <= 0) continue;
            if (item.FreezeRemainingMs > 0) continue; // 冰冻不推进冷却
            int advanceMs = FrameMs;
            if (item.HasteRemainingMs > 0) advanceMs *= 2;
            if (item.SlowRemainingMs > 0) advanceMs /= 2;
            int cap = Math.Max(1, cooldownMs / 20);
            advanceMs = Math.Min(advanceMs, cap);
            item.CooldownElapsedMs += advanceMs;
            if (item.CooldownElapsedMs >= cooldownMs)
            {
                if (item.GetAmmoCap() > 0 && item.AmmoRemaining <= 0) continue;
                item.CooldownElapsedMs = 0;
                castQueue.Add((sideIndex, i));
            }
        }
    }

    private static void SettleBurn(BattleSide victim, BattleSide opponent, int victimSideIndex, IBattleLogSink logSink, int timeMs)
    {
        if (victim.Burn <= 0) return;
        int damage = victim.Burn;
        ApplyDamageToSide(victim, damage, isBurn: true);
        int decay = RatioUtil.PercentFloor(victim.Burn, 3); // 衰减量：当前灼烧的 3%（至少为 1），灼烧 1 时衰减 1 变为 0
        victim.Burn = Math.Max(0, victim.Burn - decay);
        logSink.OnBurnTick(victimSideIndex, damage, victim.Burn, timeMs);
    }

    private static void SettlePoison(BattleSide victim, BattleSide opponent, int victimSideIndex, IBattleLogSink logSink, int timeMs)
    {
        if (victim.Poison <= 0) return;
        int damage = victim.Poison;
        ApplyDamageToSide(victim, damage, isBurn: false);
        logSink.OnPoisonTick(victimSideIndex, damage, timeMs);
    }

    private static void SettleRegen(BattleSide side, int sideIndex, IBattleLogSink logSink, int timeMs)
    {
        if (side.Regen <= 0) return;
        int heal = Math.Min(side.Regen, side.MaxHp - side.Hp);
        side.Hp = Math.Min(side.MaxHp, side.Hp + heal);
        logSink.OnRegenTick(sideIndex, heal, timeMs);
    }

    private static void ApplySandstorm(BattleSide side, int damage, IBattleLogSink logSink, int timeMs)
    {
        ApplyDamageToSide(side, damage, isBurn: false);
    }

    private static void ApplyDamageToSide(BattleSide side, int damage, bool isBurn)
    {
        if (isBurn)
        {
            int shieldConsume = Math.Min(side.Shield, (damage + 1) / 2);
            int shieldDamage = shieldConsume * 2;
            side.Shield -= shieldConsume;
            damage = Math.Max(0, damage - shieldDamage);
        }
        else
        {
            int shieldConsume = Math.Min(side.Shield, damage);
            side.Shield -= shieldConsume;
            damage -= shieldConsume;
        }
        side.Hp -= Math.Max(0, damage);
    }

    /// <summary>单次效果应用上下文，供策略表使用。充能导致满时通过 ChargeInducedCastQueue 加入施放队列。</summary>
    private readonly struct EffectApplyContext
    {
        public BattleSide Side { get; init; }
        public BattleSide Opp { get; init; }
        public BattleItemState Item { get; init; }
        public int Value { get; init; }
        public bool IsCrit { get; init; }
        public int TimeMs { get; init; }
        public IBattleLogSink LogSink { get; init; }
        public int SideIndex { get; init; }
        public int ItemIndex { get; init; }
        public string LogName { get; init; }
        public List<(int, int)>? ChargeInducedCastQueue { get; init; }
    }

    private delegate void EffectApplier(in EffectApplyContext ctx);

    private static void ApplyDamage(in EffectApplyContext ctx)
    {
        ApplyDamageToSide(ctx.Opp, ctx.Value, isBurn: false);
        ctx.LogSink.OnEffect(ctx.SideIndex, ctx.ItemIndex, ctx.Item.Template.Name, ctx.LogName, ctx.Value, ctx.TimeMs, ctx.IsCrit);
    }

    private static void ApplyBurn(in EffectApplyContext ctx)
    {
        ctx.Opp.Burn += ctx.Value;
        ctx.LogSink.OnEffect(ctx.SideIndex, ctx.ItemIndex, ctx.Item.Template.Name, ctx.LogName, ctx.Value, ctx.TimeMs, ctx.IsCrit);
    }

    private static void ApplyPoison(in EffectApplyContext ctx)
    {
        ctx.Opp.Poison += ctx.Value;
        ctx.LogSink.OnEffect(ctx.SideIndex, ctx.ItemIndex, ctx.Item.Template.Name, ctx.LogName, ctx.Value, ctx.TimeMs, ctx.IsCrit);
    }

    private static void ApplyShield(in EffectApplyContext ctx)
    {
        ctx.Side.Shield += ctx.Value;
        ctx.LogSink.OnEffect(ctx.SideIndex, ctx.ItemIndex, ctx.Item.Template.Name, ctx.LogName, ctx.Value, ctx.TimeMs, ctx.IsCrit);
    }

    private static void ApplyHeal(in EffectApplyContext ctx)
    {
        int heal = Math.Min(ctx.Value, ctx.Side.MaxHp - ctx.Side.Hp);
        ctx.Side.Hp += heal;
        int clear = RatioUtil.PercentFloor(heal, 5);
        ctx.Side.Burn = Math.Max(0, ctx.Side.Burn - clear);
        ctx.Side.Poison = Math.Max(0, ctx.Side.Poison - clear);
        ctx.LogSink.OnEffect(ctx.SideIndex, ctx.ItemIndex, ctx.Item.Template.Name, ctx.LogName, heal, ctx.TimeMs, ctx.IsCrit);
    }

    private static void ApplyRegen(in EffectApplyContext ctx)
    {
        ctx.Side.Regen += ctx.Value;
        ctx.LogSink.OnEffect(ctx.SideIndex, ctx.ItemIndex, ctx.Item.Template.Name, ctx.LogName, ctx.Value, ctx.TimeMs, ctx.IsCrit);
    }

    private static void ApplyCharge(in EffectApplyContext ctx)
    {
        int cooldownMs = ctx.Item.GetCooldownMs();
        if (cooldownMs <= 0) return;
        int newElapsed = Math.Min(cooldownMs, ctx.Item.CooldownElapsedMs + ctx.Value);
        int added = newElapsed - ctx.Item.CooldownElapsedMs;
        ctx.Item.CooldownElapsedMs = newElapsed;
        if (added > 0)
            ctx.LogSink.OnEffect(ctx.SideIndex, ctx.ItemIndex, ctx.Item.Template.Name, ctx.LogName, added, ctx.TimeMs, ctx.IsCrit);
        if (ctx.Item.CooldownElapsedMs >= cooldownMs && ctx.ChargeInducedCastQueue != null)
        {
            if (ctx.Item.GetAmmoCap() <= 0 || ctx.Item.AmmoRemaining > 0)
                ctx.ChargeInducedCastQueue.Add((ctx.SideIndex, ctx.ItemIndex));
            ctx.Item.CooldownElapsedMs = 0;
        }
    }

    /// <summary>冻结：根据 FreezeTargetCount 与 ctx.Value（毫秒），从敌人物品中随机选取目标；有冷却的物品优先，每次选取独立可重复。</summary>
    private static void ApplyFreeze(in EffectApplyContext ctx)
    {
        int freezeMs = ctx.Value;
        int count = ctx.Item.Template.GetInt("FreezeTargetCount", ctx.Item.Tier, 1);
        if (freezeMs <= 0 || count <= 0) return;
        var opp = ctx.Opp;
        var withCooldown = new List<int>();
        var withoutCooldown = new List<int>();
        for (int i = 0; i < opp.Items.Count; i++)
        {
            if (opp.Items[i].Destroyed) continue;
            if (opp.Items[i].GetCooldownMs() > 0)
                withCooldown.Add(i);
            else
                withoutCooldown.Add(i);
        }
        var pool = withCooldown.Count > 0 ? withCooldown : withoutCooldown;
        if (pool.Count == 0) return;
        var targetNames = new List<string>();
        for (int n = 0; n < count; n++)
        {
            int idx = Random.Shared.Next(pool.Count);
            int itemIdx = pool[idx];
            var target = opp.Items[itemIdx];
            target.FreezeRemainingMs = Math.Max(target.FreezeRemainingMs, freezeMs);
            targetNames.Add(target.Template.Name);
        }
        string extraSuffix = string.Concat(targetNames.Select(name => " →[" + name + "]"));
        ctx.LogSink.OnEffect(ctx.SideIndex, ctx.ItemIndex, ctx.Item.Template.Name, ctx.LogName, freezeMs, ctx.TimeMs, ctx.IsCrit, extraSuffix);
    }

    /// <summary>效果类型 → 应用策略；Other 无登记，由调用方走 CustomEffectHandlers。</summary>
    private static EffectApplier? GetEffectApplier(EffectKind kind) => kind switch
    {
        EffectKind.Damage => ApplyDamage,
        EffectKind.Burn => ApplyBurn,
        EffectKind.Poison => ApplyPoison,
        EffectKind.Shield => ApplyShield,
        EffectKind.Heal => ApplyHeal,
        EffectKind.Regen => ApplyRegen,
        EffectKind.Charge => ApplyCharge,
        EffectKind.Freeze => ApplyFreeze,
        _ => null,
    };

    /// <summary>将能力加入队列或合并 PendingCount：先查 currentAbilityQueue，再查 nextAbilityQueue；都没有则新建，仅 Immediate 入 current，其余入 next。</summary>
    private static void AddOrMergeAbility(int sideIdx, int itemIdx, int abilityIdx, AbilityDefinition ability, int pendingCount, int lastTriggerMs,
        List<AbilityQueueEntry> current, List<AbilityQueueEntry> next)
    {
        var existing = current.FirstOrDefault(e => e.SideIndex == sideIdx && e.ItemIndex == itemIdx && e.AbilityIndex == abilityIdx);
        if (existing != null) { existing.PendingCount += pendingCount; return; }
        existing = next.FirstOrDefault(e => e.SideIndex == sideIdx && e.ItemIndex == itemIdx && e.AbilityIndex == abilityIdx);
        if (existing != null) { existing.PendingCount += pendingCount; return; }
        var entry = new AbilityQueueEntry
        {
            SideIndex = sideIdx,
            ItemIndex = itemIdx,
            AbilityIndex = abilityIdx,
            PendingCount = pendingCount,
            LastTriggerMs = lastTriggerMs,
        };
        if (ability.Priority == AbilityPriority.Immediate)
            current.Add(entry);
        else
            next.Add(entry);
    }

    /// <summary>统一触发器调用：给定触发器名、来源物品与上下文，遍历双方所有物品，条件匹配的能力加入队列（Immediate→current，其余→next）。</summary>
    private static void InvokeTrigger(string triggerName, int sourceSideIdx, int sourceItemIdx, TriggerInvokeContext? context, int timeMs,
        BattleSide side0, BattleSide side1, List<AbilityQueueEntry> current, List<AbilityQueueEntry> next)
    {
        int pendingCount = triggerName == Trigger.UseItem && context?.Multicast is int m ? m : 1;
        int lastTriggerMsForBattleStart = -TriggerIntervalMs;

        foreach (var (sideIdx, side) in new[] { (0, side0), (1, side1) })
        {
            for (int itemIdx = 0; itemIdx < side.Items.Count; itemIdx++)
            {
                var item = side.Items[itemIdx];
                if (item.Destroyed) continue;
                for (int a = 0; a < item.Template.Abilities.Count; a++)
                {
                    var ab = item.Template.Abilities[a];
                    if (ab.TriggerName != triggerName) continue;
                    if (!TriggerConditionEvaluator.Evaluate(triggerName, ab.Condition, sideIdx, itemIdx, sourceSideIdx, sourceItemIdx, context)) continue;
                    int lastMs = triggerName == Trigger.BattleStart ? lastTriggerMsForBattleStart : item.LastTriggerMsByAbility[a];
                    AddOrMergeAbility(sideIdx, itemIdx, a, ab, pendingCount, lastMs, current, next);
                }
            }
        }
    }

    /// <summary>暴击时最终倍率 = CritDamagePercent/100，默认 200 即 2 倍；利爪翻倍为 400 即 4 倍。chargeInducedCastQueue 非 null 时充能导致满会加入该队列。执行过程中引发的新能力通过 AddOrMergeAbility/Invoke*Trigger 加入 current/next（仅 Immediate 入 current）。</summary>
    private void ExecuteOneEffect(int sideIndex, int itemIndex, AbilityDefinition ability, bool isCrit, int critDamagePercent,
        BattleSide side0, BattleSide side1, int timeMs, IBattleLogSink logSink, List<(int, int)>? chargeInducedCastQueue,
        List<AbilityQueueEntry> currentAbilityQueue, List<AbilityQueueEntry> nextAbilityQueue)
    {
        var side = sideIndex == 0 ? side0 : side1;
        var opp = sideIndex == 0 ? side1 : side0;
        var item = side.Items[itemIndex];
        int critMultiplier = isCrit ? Math.Max(1, critDamagePercent / 100) : 1;
        foreach (var eff in ability.Effects)
        {
            string key = eff.ValueKey ?? eff.Kind.GetDefaultTemplateKey();
            int baseValue = eff.ResolveValue(item.Template, item.Tier, key);
            int value = (eff.Kind == EffectKind.Charge || eff.Kind == EffectKind.Freeze) ? baseValue : baseValue * critMultiplier;
            if (GetEffectApplier(eff.Kind) is { } applier)
            {
                var ctx = new EffectApplyContext
                {
                    Side = side,
                    Opp = opp,
                    Item = item,
                    Value = value,
                    IsCrit = isCrit,
                    TimeMs = timeMs,
                    LogSink = logSink,
                    SideIndex = sideIndex,
                    ItemIndex = itemIndex,
                    LogName = eff.Kind.GetLogName(),
                    ChargeInducedCastQueue = chargeInducedCastQueue,
                };
                applier(in ctx);
            }
            else
            {
                CustomEffectHandlers.TryExecute(eff.CustomEffectId ?? "", sideIndex, itemIndex, item, ability, eff, side0, side1, timeMs, logSink);
            }
        }
    }
}
