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
                    int ammoCap = side.GetItemInt(itemIdx, nameof(ItemTemplate.AmmoCap), 0);
                    if (ammoCap > 0)
                        item.AmmoRemaining--;
                    logSink.OnCast(sideIdx, itemIdx, item.Template.Name, timeMs, ammoCap > 0 ? item.AmmoRemaining : null);
                    int multicast = side.GetItemInt(itemIdx, nameof(ItemTemplate.Multicast), 1);
                    InvokeTrigger(Trigger.UseItem, sideIdx, itemIdx, new TriggerInvokeContext { Multicast = multicast }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                    InvokeTrigger(Trigger.UseOtherItem, sideIdx, itemIdx, new TriggerInvokeContext { UsedTemplate = item.Template }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                }

                // 8. 处理能力队列（只处理 currentAbilityQueue）；按优先级从高到低执行（Immediate/Highest/High 先于 Medium/Low/Lowest），同优先级按入队顺序
                var toProcessAbilities = new List<AbilityQueueEntry>(currentAbilityQueue);
                currentAbilityQueue.Clear();
                toProcessAbilities.Sort((a, b) =>
                {
                    var sideA = a.SideIndex == 0 ? side0 : side1;
                    var sideB = b.SideIndex == 0 ? side0 : side1;
                    int priA = (int)sideA.Items[a.ItemIndex].Template.Abilities[a.AbilityIndex].Priority;
                    int priB = (int)sideB.Items[b.ItemIndex].Template.Abilities[b.AbilityIndex].Priority;
                    if (priA != priB) return priA.CompareTo(priB);
                    if (a.SideIndex != b.SideIndex) return a.SideIndex.CompareTo(b.SideIndex);
                    if (a.ItemIndex != b.ItemIndex) return a.ItemIndex.CompareTo(b.ItemIndex);
                    return a.AbilityIndex.CompareTo(b.AbilityIndex);
                });
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
                    bool canCrit = ItemHasAnyCrittableField(item) && ability.Effects.Any(e => e.Apply != null && e.ApplyCritMultiplier);
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

    /// <summary>物品是否具备可暴击的六类数值之一；使用导入时的类型快照，避免战斗内数值被修改后误判。</summary>
    private static bool ItemHasAnyCrittableField(BattleItemState item) =>
        item.TypeSnapshot.HasAnyCrittableField;

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
                    Condition = EnsureTriggerCondition(a.TriggerName, Condition.Clone(a.Condition)),
                    TargetCondition = Condition.Clone(a.TargetCondition),
                    Effects = a.Effects.Select(e => new EffectDefinition { Value = e.Value, ValueResolver = e.ValueResolver, ValueKey = e.ValueKey, ApplyCritMultiplier = e.ApplyCritMultiplier, Apply = e.Apply }).ToList(),
                }).ToList(),
                Auras = t.Auras.Select(a => new AuraDefinition { AttributeName = a.AttributeName, Condition = Condition.Clone(a.Condition), FixedValueKey = a.FixedValueKey, PercentValueKey = a.PercentValueKey, FixedValueFormula = a.FixedValueFormula }).ToList(),
            };
            clone.SetIntsByTier(t.GetIntsByTierSnapshot());
            if (entry.Overrides != null)
            {
                foreach (var kv in entry.Overrides)
                    clone.SetInt(kv.Key, kv.Value);
            }
            var state = new BattleItemState(clone, entry.Tier)
            {
                TypeSnapshot = ItemTypeSnapshot.FromTemplate(clone, entry.Tier),
            };
            side.Items.Add(state);
        }
        return side;
    }

    /// <summary>UseItem → SameAsSource；UseOtherItem 始终叠加己方其他物品（And(DifferentFromSource, SameSide)），再与显式 Condition（如 WithTag）取与；Freeze → SameSide（己方触发冻结时）。</summary>
    private static Condition? EnsureTriggerCondition(string triggerName, Condition? condition)
    {
        if (triggerName == Trigger.UseItem) return condition ?? Condition.SameAsSource;
        if (triggerName == Trigger.UseOtherItem)
        {
            Condition baseSameSideOther = Condition.And(Condition.DifferentFromSource, Condition.SameSide);
            return condition != null ? Condition.And(baseSameSideOther, condition) : baseSameSideOther;
        }
        if (triggerName == Trigger.Freeze) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Slow) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.OnCrit) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.OnDestroy) return condition ?? Condition.SameSide;
        return condition;
    }

    private static void ProcessCooldown(BattleSide side, int sideIndex, int timeMs, List<(int, int)> castQueue)
    {
        for (int i = 0; i < side.Items.Count; i++)
        {
            var item = side.Items[i];
            if (item.Destroyed) continue;
            int cooldownMs = side.GetItemInt(i, "CooldownMs", 0);
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
                int ammoCap = side.GetItemInt(i, "AmmoCap", 0);
                if (ammoCap > 0 && item.AmmoRemaining <= 0) continue;
                item.CooldownElapsedMs = 0;
                castQueue.Add((sideIndex, i));
            }
        }
    }

    private static void SettleBurn(BattleSide victim, BattleSide opponent, int victimSideIndex, IBattleLogSink logSink, int timeMs)
    {
        if (victim.Burn <= 0) return;
        int damage = victim.Burn;
        BattleSideDamage.ApplyDamageToSide(victim, damage, isBurn: true);
        int decay = RatioUtil.PercentFloor(victim.Burn, 3); // 衰减量：当前灼烧的 3%（至少为 1），灼烧 1 时衰减 1 变为 0
        victim.Burn = Math.Max(0, victim.Burn - decay);
        logSink.OnBurnTick(victimSideIndex, damage, victim.Burn, timeMs);
    }

    private static void SettlePoison(BattleSide victim, BattleSide opponent, int victimSideIndex, IBattleLogSink logSink, int timeMs)
    {
        if (victim.Poison <= 0) return;
        int damage = victim.Poison;
        BattleSideDamage.ApplyDamageToSide(victim, damage, isBurn: false);
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
        _ = BattleSideDamage.ApplyDamageToSide(side, damage, isBurn: false);
    }

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
        int pendingCount = (triggerName == Trigger.UseItem || triggerName == Trigger.Freeze || triggerName == Trigger.Slow) && context?.Multicast is int m ? m : 1;
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
                    var triggerCtx = new ConditionContext
                    {
                        CandidateSide = sideIdx,
                        CandidateItem = itemIdx,
                        SourceSide = sourceSideIdx,
                        SourceItem = sourceItemIdx,
                        UsedTemplate = context?.UsedTemplate,
                        CandidateTemplate = item.Template,
                        DestroyedItemTemplate = triggerName == Trigger.OnDestroy ? context?.DestroyedItemTemplate : null,
                        DestroyedItemInFlight = triggerName == Trigger.OnDestroy && (context?.DestroyedItemInFlight ?? false),
                    };
                    if (ab.Condition != null && !ab.Condition.Evaluate(triggerCtx)) continue;
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
            if (eff.Apply == null) continue;
            int value = 0;
            if (eff.ValueKey != null)
            {
                int baseValue = eff.ResolveValue(item.Template, item.Tier, eff.ValueKey);
                bool applyCrit = ItemHasAnyCrittableField(item) && eff.ApplyCritMultiplier;
                value = applyCrit ? baseValue * critMultiplier : baseValue;
            }
            var ctx = new EffectApplyContextImpl
            {
                Side = side,
                Opp = opp,
                Item = item,
                Value = value,
                CritMultiplier = critMultiplier,
                IsCrit = isCrit,
                TimeMs = timeMs,
                LogSink = logSink,
                SideIndex = sideIndex,
                ItemIndex = itemIndex,
                ChargeInducedCastQueue = chargeInducedCastQueue,
                TargetCondition = ability.TargetCondition,
                OnFreezeApplied = (count) => InvokeTrigger(Trigger.Freeze, sideIndex, itemIndex, new TriggerInvokeContext { Multicast = count }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue),
                OnSlowApplied = (count) => InvokeTrigger(Trigger.Slow, sideIndex, itemIndex, new TriggerInvokeContext { Multicast = count }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue),
                OnDestroyApplied = (destroyedItemIdx) =>
                {
                    var destroyed = side.Items[destroyedItemIdx];
                    InvokeTrigger(Trigger.OnDestroy, sideIndex, itemIndex, new TriggerInvokeContext { DestroyedItemTemplate = destroyed.Template, DestroyedItemInFlight = destroyed.InFlight }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                    side.Items[destroyedItemIdx].Destroyed = true;
                },
            };
            eff.Apply(ctx);
        }
        if (isCrit)
            InvokeTrigger(Trigger.OnCrit, sideIndex, itemIndex, null, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
    }
}
