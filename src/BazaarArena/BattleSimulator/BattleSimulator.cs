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

            // 1. 第 0 帧触发「战斗开始」
            if (frame == 0)
            {
                var battleStartEntries = new List<(int SideIndex, int ItemIndex, int AbilityIndex, AbilityDefinition Ability)>();
                foreach (var (sideIdx, side) in new[] { (0, side0), (1, side1) })
                {
                    for (int i = 0; i < side.Items.Count; i++)
                    {
                        var item = side.Items[i];
                        for (int a = 0; a < item.Template.Abilities.Count; a++)
                        {
                            var ab = item.Template.Abilities[a];
                            if (ab.TriggerName == Trigger.BattleStart)
                                battleStartEntries.Add((sideIdx, i, a, ab));
                        }
                    }
                }
                battleStartEntries.Sort((x, y) => x.Ability.Priority.CompareTo(y.Ability.Priority));
                foreach (var (sideIdx, itemIdx, abilityIdx, ability) in battleStartEntries)
                {
                    ExecuteOneEffect(sideIdx, itemIdx, ability, isCrit: false, critDamagePercent: 200, side0, side1, 0, logSink);
                }
            }

            // 2. 加速、减速、冻结剩余时间减少 50ms
            foreach (var side in new[] { side0, side1 })
            {
                foreach (var item in side.Items)
                {
                    if (item.HasteRemainingMs > 0) item.HasteRemainingMs = Math.Max(0, item.HasteRemainingMs - FrameMs);
                    if (item.SlowRemainingMs > 0) item.SlowRemainingMs = Math.Max(0, item.SlowRemainingMs - FrameMs);
                    if (item.FreezeRemainingMs > 0) item.FreezeRemainingMs = Math.Max(0, item.FreezeRemainingMs - FrameMs);
                }
            }

            // 3. 处理冷却，充能完成则加入施放队列
            castQueue.Clear();
            ProcessCooldown(side0, 0, timeMs, castQueue);
            ProcessCooldown(side1, 1, timeMs, castQueue);

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

            // 7. 遍历施放队列，加入能力队列（减少 Ammo → 暴击 → 当前物品使用；多重触发写入多次效果）
            foreach (var (sideIdx, itemIdx) in castQueue)
            {
                var side = sideIdx == 0 ? side0 : side1;
                var item = side.Items[itemIdx];
                if (item.GetAmmoCap() > 0)
                    item.AmmoRemaining--;
                logSink.OnCast(sideIdx, itemIdx, item.Template.Name, timeMs);
                int multicast = item.GetMulticast();
                for (int a = 0; a < item.Template.Abilities.Count; a++)
                {
                    var ab = item.Template.Abilities[a];
                    if (ab.TriggerName != Trigger.UseItem) continue;
                    nextAbilityQueue.Add(new AbilityQueueEntry
                    {
                        SideIndex = sideIdx,
                        ItemIndex = itemIdx,
                        AbilityIndex = a,
                        PendingCount = multicast,
                        LastTriggerMs = item.LastTriggerMsByAbility[a],
                    });
                }
            }

            // 8. 处理能力队列（当前帧的 currentAbilityQueue；下一帧的待处理在 nextAbilityQueue，本帧末再移动）
            foreach (var entry in currentAbilityQueue)
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
                ExecuteOneEffect(entry.SideIndex, entry.ItemIndex, ability, isCrit, critDamagePercent, side0, side1, timeMs, logSink);
                entry.PendingCount--;
                if (entry.PendingCount > 0)
                    nextAbilityQueue.Add(entry);
            }

            // 本帧能力处理完毕，将下一帧队列移入当前队列，供下一轮循环步骤 8 处理
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

    private static bool IsCrittableEffect(EffectKind k) => k != EffectKind.Other;

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
        public static bool Evaluate(AuraConditionKind condition, BattleSide side, int sourceItemIndex, int targetItemIndex)
        {
            return condition switch
            {
                AuraConditionKind.AdjacentToSource => Math.Abs(sourceItemIndex - targetItemIndex) == 1,
                AuraConditionKind.SameAsSource => sourceItemIndex == targetItemIndex,
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
                    Effects = a.Effects.Select(e => new EffectDefinition { Kind = e.Kind, Value = e.Value, ValueResolver = e.ValueResolver, ValueKey = e.ValueKey, CustomEffectId = e.CustomEffectId }).ToList(),
                }).ToList(),
                Auras = t.Auras.Select(a => new AuraDefinition { AttributeName = a.AttributeName, Condition = a.Condition, FixedValueKey = a.FixedValueKey, PercentValueKey = a.PercentValueKey }).ToList(),
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

    /// <summary>单次效果应用上下文，供策略表使用。</summary>
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

    /// <summary>效果类型 → 应用策略；Other 无登记，由调用方走 CustomEffectHandlers。</summary>
    private static EffectApplier? GetEffectApplier(EffectKind kind) => kind switch
    {
        EffectKind.Damage => ApplyDamage,
        EffectKind.Burn => ApplyBurn,
        EffectKind.Poison => ApplyPoison,
        EffectKind.Shield => ApplyShield,
        EffectKind.Heal => ApplyHeal,
        EffectKind.Regen => ApplyRegen,
        _ => null,
    };

    /// <summary>暴击时最终倍率 = CritDamagePercent/100，默认 200 即 2 倍；利爪翻倍为 400 即 4 倍。</summary>
    private void ExecuteOneEffect(int sideIndex, int itemIndex, AbilityDefinition ability, bool isCrit, int critDamagePercent,
        BattleSide side0, BattleSide side1, int timeMs, IBattleLogSink logSink)
    {
        var side = sideIndex == 0 ? side0 : side1;
        var opp = sideIndex == 0 ? side1 : side0;
        var item = side.Items[itemIndex];
        int critMultiplier = isCrit ? Math.Max(1, critDamagePercent / 100) : 1;
        foreach (var eff in ability.Effects)
        {
            string key = eff.ValueKey ?? eff.Kind.GetDefaultTemplateKey();
            int baseValue = eff.ResolveValue(item.Template, item.Tier, key);
            int value = baseValue * critMultiplier;
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
