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

        side0.SideIndex = 0;
        side1.SideIndex = 1;
        for (int i = 0; i < side0.Items.Count; i++)
        {
            side0.Items[i].SideIndex = 0;
            side0.Items[i].ItemIndex = i;
        }
        for (int i = 0; i < side1.Items.Count; i++)
        {
            side1.Items[i].SideIndex = 1;
            side1.Items[i].ItemIndex = i;
        }

        int timeMs = 0;
        List<BattleItemState> castQueue = [];
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
                InvokeTrigger(Trigger.BattleStart, null, null, 0, side0, side1, currentAbilityQueue, nextAbilityQueue);

            // 2. 处理冷却，充能完成则加入施放队列（冻结时冷却不推进）
            castQueue.Clear();
            ProcessCooldown(side0, timeMs, castQueue);
            ProcessCooldown(side1, timeMs, castQueue);

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
                SettlePoison(side0, side1, logSink, timeMs);
                SettlePoison(side1, side0, logSink, timeMs);
                SettleRegen(side0, logSink, timeMs);
                SettleRegen(side1, logSink, timeMs);
            }

            // 5. 500ms 倍数：灼烧
            if (timeMs > 0 && timeMs % BurnTickIntervalMs == 0)
            {
                SettleBurn(side0, side1, logSink, timeMs);
                SettleBurn(side1, side0, logSink, timeMs);
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
                var toProcess = new List<BattleItemState>(castQueue);
                castQueue.Clear();
                // 7. 遍历本轮施放队列，用触发器调用方式加入能力队列（先合并 current 再 next，都没有则入 next；仅 Immediate 入 current）
                foreach (var item in toProcess)
                {
                    var side = item.SideIndex == 0 ? side0 : side1;
                    int ammoCap = side.GetItemInt(item.ItemIndex, nameof(ItemTemplate.AmmoCap), 0);
                    if (ammoCap > 0)
                        item.AmmoRemaining--;
                    logSink.OnCast(item, item.Template.Name, timeMs, ammoCap > 0 ? item.AmmoRemaining : null);
                    int multicast = side.GetItemInt(item.ItemIndex, nameof(ItemTemplate.Multicast), 1);
                    InvokeTrigger(Trigger.UseItem, item, new TriggerInvokeContext { Multicast = multicast, UsedTemplate = item.Template }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                }

                // 8. 处理能力队列（只处理 currentAbilityQueue）；按优先级从高到低执行（Immediate/Highest/High 先于 Medium/Low/Lowest），同优先级按入队顺序
                var toProcessAbilities = new List<AbilityQueueEntry>(currentAbilityQueue);
                currentAbilityQueue.Clear();
                toProcessAbilities.Sort((a, b) =>
                {
                    int priA = (int)a.Owner.Template.Abilities[a.AbilityIndex].Priority;
                    int priB = (int)b.Owner.Template.Abilities[b.AbilityIndex].Priority;
                    if (priA != priB) return priA.CompareTo(priB);
                    if (a.Owner.SideIndex != b.Owner.SideIndex) return a.Owner.SideIndex.CompareTo(b.Owner.SideIndex);
                    if (a.Owner.ItemIndex != b.Owner.ItemIndex) return a.Owner.ItemIndex.CompareTo(b.Owner.ItemIndex);
                    return a.AbilityIndex.CompareTo(b.AbilityIndex);
                });
                foreach (var entry in toProcessAbilities)
                {
                    if (timeMs - entry.LastTriggerMs < TriggerIntervalMs)
                    {
                        nextAbilityQueue.Add(entry);
                        continue;
                    }
                    var item = entry.Owner;
                    var side = item.SideIndex == 0 ? side0 : side1;
                    var opp = item.SideIndex == 0 ? side1 : side0;
                    item.SetLastTriggerMs(entry.AbilityIndex, timeMs);
                    entry.LastTriggerMs = timeMs;
                    var ability = item.Template.Abilities[entry.AbilityIndex];
                    bool canCrit = ItemHasAnyCrittableField(item) && ability.Apply != null && ability.ApplyCritMultiplier;
                    bool isCrit = false;
                    int critDamagePercent = 200;
                    var auraContext = new BattleAuraContext(side, item, opp);
                    int critRate = item.Template.GetInt(nameof(ItemTemplate.CritRatePercent), item.Tier, 0, auraContext);
                    if (canCrit && critRate > 0 && Random.Shared.Next(100) < critRate)
                    {
                        isCrit = true;
                        critDamagePercent = item.Template.GetInt(nameof(ItemTemplate.CritDamagePercent), item.Tier, 200, auraContext);
                    }
                    ExecuteOneEffect(item, ability, isCrit, critDamagePercent, side0, side1, timeMs, logSink, castQueue, currentAbilityQueue, nextAbilityQueue);
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

    /// <summary>物品是否具备可暴击的六类数值之一；依据模板的 Tag（护盾/伤害/灼烧/剧毒/治疗/再生）。</summary>
    private static bool ItemHasAnyCrittableField(BattleItemState item)
    {
        var tags = item.Template.Tags;
        if (tags == null) return false;
        return tags.Contains(Tag.Damage) || tags.Contains(Tag.Burn) || tags.Contains(Tag.Poison)
            || tags.Contains(Tag.Heal) || tags.Contains(Tag.Shield) || tags.Contains(Tag.Regen);
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
                    Condition = EnsureTriggerCondition(a.TriggerName, Condition.Clone(a.Condition)),
                    SourceCondition = Condition.Clone(a.SourceCondition),
                    InvokeTargetCondition = Condition.Clone(a.InvokeTargetCondition),
                    TargetCondition = Condition.Clone(a.TargetCondition),
                    Value = a.Value,
                    ValueKey = a.ValueKey,
                    ApplyCritMultiplier = a.ApplyCritMultiplier,
                    Apply = a.Apply,
                }).ToList(),
                Auras = t.Auras.Select(a => new AuraDefinition { AttributeName = a.AttributeName, Condition = Condition.Clone(a.Condition), FixedValueKey = a.FixedValueKey, PercentValueKey = a.PercentValueKey, FixedValueFormula = a.FixedValueFormula }).ToList(),
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

    /// <summary>condition ?? default：UseItem → SameAsSource，其他触发器（Freeze/Slow/Crit/Destroy/BattleStart）→ SameSide。</summary>
    private static Condition? EnsureTriggerCondition(string triggerName, Condition? condition)
    {
        if (triggerName == Trigger.UseItem) return condition ?? Condition.SameAsSource;
        if (triggerName == Trigger.Freeze) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Slow) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Crit) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Destroy) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.BattleStart) return condition ?? Condition.SameSide;
        return condition;
    }

    private static void ProcessCooldown(BattleSide side, int timeMs, List<BattleItemState> castQueue)
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
                castQueue.Add(item);
            }
        }
    }

    private static void SettleBurn(BattleSide victim, BattleSide opponent, IBattleLogSink logSink, int timeMs)
    {
        if (victim.Burn <= 0) return;
        int damage = victim.Burn;
        BattleSideDamage.ApplyDamageToSide(victim, damage, isBurn: true);
        int decay = RatioUtil.PercentFloor(victim.Burn, 3); // 衰减量：当前灼烧的 3%（至少为 1），灼烧 1 时衰减 1 变为 0
        victim.Burn = Math.Max(0, victim.Burn - decay);
        logSink.OnBurnTick(victim, damage, victim.Burn, timeMs);
    }

    private static void SettlePoison(BattleSide victim, BattleSide opponent, IBattleLogSink logSink, int timeMs)
    {
        if (victim.Poison <= 0) return;
        int damage = victim.Poison;
        BattleSideDamage.ApplyDamageToSide(victim, damage, isBurn: false);
        logSink.OnPoisonTick(victim, damage, timeMs);
    }

    private static void SettleRegen(BattleSide side, IBattleLogSink logSink, int timeMs)
    {
        if (side.Regen <= 0) return;
        int heal = Math.Min(side.Regen, side.MaxHp - side.Hp);
        side.Hp = Math.Min(side.MaxHp, side.Hp + heal);
        logSink.OnRegenTick(side, heal, timeMs);
    }

    private static void ApplySandstorm(BattleSide side, int damage, IBattleLogSink logSink, int timeMs)
    {
        _ = BattleSideDamage.ApplyDamageToSide(side, damage, isBurn: false);
    }

    /// <summary>将能力加入队列或合并 PendingCount：先查 currentAbilityQueue，再查 nextAbilityQueue；都没有则新建，仅 Immediate 入 current，其余入 next。</summary>
    private static void AddOrMergeAbility(BattleItemState owner, int abilityIdx, AbilityDefinition ability, int pendingCount, int lastTriggerMs,
        List<AbilityQueueEntry> current, List<AbilityQueueEntry> next)
    {
        var existing = current.FirstOrDefault(e => e.Owner == owner && e.AbilityIndex == abilityIdx);
        if (existing != null) { existing.PendingCount += pendingCount; return; }
        existing = next.FirstOrDefault(e => e.Owner == owner && e.AbilityIndex == abilityIdx);
        if (existing != null) { existing.PendingCount += pendingCount; return; }
        var entry = new AbilityQueueEntry
        {
            Owner = owner,
            AbilityIndex = abilityIdx,
            PendingCount = pendingCount,
            LastTriggerMs = lastTriggerMs,
        };
        if (ability.Priority == AbilityPriority.Immediate)
            current.Add(entry);
        else
            next.Add(entry);
    }

    /// <summary>统一触发器调用：给定触发器名、引起触发的物品与上下文，遍历双方所有物品；条件匹配的能力入队（Immediate→current，其余→next）。Condition 评估时 Item=引起触发的物品、Source=能力持有者。</summary>
    private static void InvokeTrigger(string triggerName, BattleItemState? causeItem, TriggerInvokeContext? context, int timeMs,
        BattleSide side0, BattleSide side1, List<AbilityQueueEntry> current, List<AbilityQueueEntry> next)
    {
        int pendingCount = (triggerName == Trigger.UseItem || triggerName == Trigger.Freeze || triggerName == Trigger.Slow) && context?.Multicast is int m ? m : 1;
        int battleStartLastTriggerMs = -TriggerIntervalMs;

        foreach (var (ownerSideIndex, ownerSide) in new[] { (0, side0), (1, side1) })
        {
            BattleSide mySide = ownerSide;
            BattleSide enemySide = ownerSideIndex == 0 ? side1 : side0;
            for (int ownerItemIndex = 0; ownerItemIndex < ownerSide.Items.Count; ownerItemIndex++)
            {
                var abilityOwner = ownerSide.Items[ownerItemIndex];
                if (abilityOwner.Destroyed) continue;
                for (int a = 0; a < abilityOwner.Template.Abilities.Count; a++)
                {
                    var ab = abilityOwner.Template.Abilities[a];
                    if (ab.TriggerName != triggerName) continue;
                    var conditionCtx = new ConditionContext
                    {
                        MySide = mySide,
                        EnemySide = enemySide,
                        Item = causeItem,
                        Source = abilityOwner,
                    };
                    if (ab.Condition != null && !ab.Condition.Evaluate(conditionCtx)) continue;
                    if (ab.SourceCondition != null)
                    {
                        var sourceConditionCtx = new ConditionContext
                        {
                            MySide = mySide,
                            EnemySide = enemySide,
                            Item = abilityOwner,
                            Source = abilityOwner,
                        };
                        if (!ab.SourceCondition.Evaluate(sourceConditionCtx)) continue;
                    }
                    if (ab.InvokeTargetCondition != null && context?.InvokeTargetItem is { } invokeTargetItem)
                    {
                        var invokeTargetCtx = new ConditionContext
                        {
                            MySide = mySide,
                            EnemySide = enemySide,
                            Item = invokeTargetItem,
                            Source = abilityOwner,
                        };
                        if (!ab.InvokeTargetCondition.Evaluate(invokeTargetCtx)) continue;
                    }
                    int lastMs = triggerName == Trigger.BattleStart ? battleStartLastTriggerMs : abilityOwner.GetLastTriggerMs(a);
                    AddOrMergeAbility(abilityOwner, a, ab, pendingCount, lastMs, current, next);
                }
            }
        }
    }

    /// <summary>暴击时最终倍率 = CritDamagePercent/100，默认 200 即 2 倍；利爪翻倍为 400 即 4 倍。chargeInducedCastQueue 非 null 时充能导致满会加入该队列。执行过程中引发的新能力通过 AddOrMergeAbility/Invoke*Trigger 加入 current/next（仅 Immediate 入 current）。</summary>
    private void ExecuteOneEffect(BattleItemState item, AbilityDefinition ability, bool isCrit, int critDamagePercent,
        BattleSide side0, BattleSide side1, int timeMs, IBattleLogSink logSink, List<BattleItemState>? chargeInducedCastQueue,
        List<AbilityQueueEntry> currentAbilityQueue, List<AbilityQueueEntry> nextAbilityQueue)
    {
        if (ability.Apply == null) return;
        var side = item.SideIndex == 0 ? side0 : side1;
        var opp = item.SideIndex == 0 ? side1 : side0;
        int critMultiplier = isCrit ? Math.Max(1, critDamagePercent / 100) : 1;
        int value = 0;
        if (ability.ValueKey != null)
        {
            int baseValue = ability.ResolveValue(item.Template, item.Tier, ability.ValueKey);
            bool applyCrit = ItemHasAnyCrittableField(item) && ability.ApplyCritMultiplier;
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
            ChargeInducedCastQueue = chargeInducedCastQueue,
            TargetCondition = ability.TargetCondition,
            OnFreezeApplied = (targets) =>
            {
                foreach (var (ts, ti) in targets)
                {
                    var target = (ts == 0 ? side0 : side1).Items[ti];
                    InvokeTrigger(Trigger.Freeze, item, new TriggerInvokeContext { InvokeTargetItem = target, Multicast = 1 }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                }
            },
            OnSlowApplied = (targets) =>
            {
                foreach (var (ts, ti) in targets)
                {
                    var target = (ts == 0 ? side0 : side1).Items[ti];
                    InvokeTrigger(Trigger.Slow, item, new TriggerInvokeContext { InvokeTargetItem = target, Multicast = 1 }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                }
            },
            OnDestroyApplied = (destroyedItemIdx) =>
            {
                var target = side.Items[destroyedItemIdx];
                InvokeTrigger(Trigger.Destroy, item, new TriggerInvokeContext { InvokeTargetItem = target }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                target.Destroyed = true;
            },
        };
        ability.Apply(ctx);
        if (isCrit)
            InvokeTrigger(Trigger.Crit, item, null, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
    }
}
