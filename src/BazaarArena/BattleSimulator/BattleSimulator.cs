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
    private readonly Dictionary<int, AbilityState> _abilityStates = [];
    private BattleSessionTables? _sessionTables;

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
        _sessionTables = BuildSessionTables(side0, side1);
        _abilityStates.Clear();
        foreach (var kv in _sessionTables.AbilityRefIdToState)
            _abilityStates[kv.Key] = kv.Value;

        var battleState = new BattleState();
        battleState.Side[0] = side0;
        battleState.Side[1] = side1;
        battleState.SessionTables = _sessionTables;
        battleState.LogSink = logSink;
        var abilityCurrent = new AbilityQueueBuckets();
        var abilityNext = new AbilityQueueBuckets();

        // 沙尘暴状态：设计文档 30s 开始，首次 tick 300ms，然后间隔递减 20ms 至 140ms 后改为伤害+2，120s 结束
        int sandstormNextTickMs = SandstormStartMs;
        int sandstormIntervalMs = 300;
        int sandstormDamage = 1;

        for (int frame = 0; ; frame++, battleState.TimeMs += FrameMs)
        {
            if (logLevel == BattleLogLevel.Detailed)
                logSink.OnFrameStart(battleState.TimeMs, frame);
            logSink.OnHpSnapshot(battleState.TimeMs, side0.Hp, side1.Hp);

            // 1. 第 0 帧触发「战斗开始」：统一 invoke 入队，仅 Immediate 入 current、其余入 next，步骤 8 再执行
            if (frame == 0)
                InvokeTrigger(Trigger.BattleStart, null, null, 0, side0, side1, abilityCurrent, abilityNext);

            // 2. 处理冷却，充能完成则加入施放队列（冻结时冷却不推进）
            battleState.CastQueue.Clear();
            ProcessCooldown(side0, battleState.TimeMs, battleState.CastQueue);
            ProcessCooldown(side1, battleState.TimeMs, battleState.CastQueue);

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
            if (battleState.TimeMs > 0 && battleState.TimeMs % PoisonRegenTickIntervalMs == 0)
            {
                SettlePoison(side0, side1, logSink, battleState.TimeMs);
                SettlePoison(side1, side0, logSink, battleState.TimeMs);
                SettleRegen(side0, logSink, battleState.TimeMs);
                SettleRegen(side1, logSink, battleState.TimeMs);
            }

            // 5. 500ms 倍数：灼烧
            if (battleState.TimeMs > 0 && battleState.TimeMs % BurnTickIntervalMs == 0)
            {
                SettleBurn(side0, side1, logSink, battleState.TimeMs);
                SettleBurn(side1, side0, logSink, battleState.TimeMs);
            }

            // 6. 沙尘暴
            if (battleState.TimeMs >= SandstormStartMs && battleState.TimeMs < SandstormEndMs && battleState.TimeMs >= sandstormNextTickMs)
            {
                ApplySandstorm(side0, sandstormDamage, logSink, battleState.TimeMs);
                ApplySandstorm(side1, sandstormDamage, logSink, battleState.TimeMs);
                logSink.OnSandstormTick(sandstormDamage, battleState.TimeMs);
                if (sandstormIntervalMs > 140)
                {
                    sandstormIntervalMs -= 20;
                    if (sandstormIntervalMs < 140) sandstormIntervalMs = 140;
                }
                else
                    sandstormDamage += 2;
                sandstormNextTickMs = battleState.TimeMs + sandstormIntervalMs;
            }

            // 7-8. 施放队列产生的能力入 next 各桶（下一帧才处理）；步骤 8 只处理 current 六桶；延后/未消耗的入 next；仅步骤 9 对调 current/next 引用
            do
            {
                var toProcess = new List<ItemState>(battleState.CastQueue);
                battleState.CastQueue.Clear();
                // 7. 遍历本轮施放队列，用触发器调用方式加入能力队列（先合并 current 再 next，都没有则入 next；仅 Immediate 入 current）
                foreach (var item in toProcess)
                {
                    var side = item.SideIndex == 0 ? side0 : side1;
                    int ammoCap = side.GetItemInt(item.ItemIndex, Key.AmmoCap);
                    int multicast = side.GetItemInt(item.ItemIndex, Key.Multicast);
                    InvokeTrigger(Trigger.UseItem, item, new TriggerInvokeContext { Multicast = multicast, InvokeTargetItem = item }, battleState.TimeMs, side0, side1, abilityCurrent, abilityNext,
                        executeImmediate: (owner, abilityIdx, ability) =>
                        {
                            if (_sessionTables == null) return;
                            RuntimeAbilityRef? abilityRef = null;
                            var refs = _sessionTables.AbilitiesByTrigger[Trigger.UseItem];
                            for (int i = 0; i < refs.Count; i++)
                            {
                                if (refs[i].Owner == owner && refs[i].Ability == ability)
                                {
                                    abilityRef = refs[i];
                                    break;
                                }
                            }
                            if (abilityRef != null)
                                ExecuteOneEffect(abilityRef, null, side0, side1, battleState.TimeMs, logSink, battleState.CastQueue, abilityCurrent, abilityNext);
                        });
                    if (ammoCap > 0)
                        item.AmmoRemaining = Math.Max(0, item.AmmoRemaining - 1);
                    logSink.OnCast(item, item.Template.Name, battleState.TimeMs, ammoCap > 0 ? item.AmmoRemaining : null);
                    if (ammoCap > 0)
                        InvokeTrigger(Trigger.Ammo, item, null, battleState.TimeMs, side0, side1, abilityCurrent, abilityNext);
                }

                // 8. 处理能力队列（只处理 current 六桶）；桶序 = Immediate→Lowest，桶内 FIFO。与上次触发间隔不足 250ms 的条目写入 next 对应桶留到下一帧再判。
                for (int pbi = 0; pbi < AbilityQueueBuckets.BucketCount; pbi++)
                {
                    var bucket = abilityCurrent.Bucket(pbi);
                    for (int bj = 0; bj < bucket.Count; bj++)
                    {
                        var abilityRef = bucket[bj];
                        if (!_abilityStates.TryGetValue(abilityRef.Id, out var state) || state.PendingCount <= 0)
                            continue;
                        var item = abilityRef.Owner;
                        var ability = abilityRef.Ability;
                        if (state.LastTriggerMs != int.MinValue && battleState.TimeMs - state.LastTriggerMs < TriggerIntervalMs)
                        {
                            AddEntryToBucketsByAbilityPriority(abilityNext, abilityRef);
                            continue;
                        }
                        var side = item.SideIndex == 0 ? side0 : side1;
                        var opp = item.SideIndex == 0 ? side1 : side0;
                        state.LastTriggerMs = battleState.TimeMs;
                        bool canCrit = ItemHasAnyCrittableField(item) && ability.Apply != null && ability.ApplyCritMultiplier && ability.UseSelf;
                        bool isCrit = false;
                        int critDamagePercent = 200;
                        if (canCrit)
                        {
                            if (item.CritTimeMs == battleState.TimeMs)
                            {
                                isCrit = item.IsCritThisUse;
                                critDamagePercent = item.CritDamage;
                            }
                            else
                            {
                                int critRate = item.GetAttribute(Key.CritRate);
                                if (critRate > 0 && ThreadLocalRandom.Next100() < critRate)
                                {
                                    isCrit = true;
                                    critDamagePercent = item.GetAttribute(Key.CritDamage);
                                    if (critDamagePercent <= 0) critDamagePercent = 200;
                                }
                                item.CritTimeMs = battleState.TimeMs;
                                item.IsCritThisUse = isCrit;
                                item.CritDamage = critDamagePercent;
                                if (isCrit)
                                    InvokeTrigger(Trigger.Crit, item, null, battleState.TimeMs, side0, side1, abilityCurrent, abilityNext);
                            }
                        }
                        ItemState? invokeTarget = null;
                        if (state.InvokeTargets != null && state.InvokeTargets.Count > 0)
                        {
                            invokeTarget = state.InvokeTargets[0];
                            state.InvokeTargets.RemoveAt(0);
                        }
                        ExecuteOneEffect(abilityRef, invokeTarget, side0, side1, battleState.TimeMs, logSink, battleState.CastQueue, abilityCurrent, abilityNext);
                        state.PendingCount--;
                        if (state.PendingCount > 0)
                            AddEntryToBucketsByAbilityPriority(abilityNext, abilityRef);
                    }
                    bucket.Clear();
                }
            } while (battleState.CastQueue.Count > 0);

            // 9. 能力队列更新到下一帧：对调 current/next，下一帧步骤 8 直接处理原 next 各桶；清空新的 next（原 current 已空桶，Clear 仅复位容量语义）
            (abilityCurrent, abilityNext) = (abilityNext, abilityCurrent);
            abilityNext.Clear();

            // 10. 胜负判定（先输出本帧结算后的最终生命，再通知结果）。即将落败时触发 AboutToLose（Hp≤0 即触发），「首次」由物品 Custom_0 保证（参考靴里剑），仅执行 Immediate 能力后重判。
            bool dead0 = side0.Hp <= 0;
            bool dead1 = side1.Hp <= 0;
            var aboutToLoseCurrent = new AbilityQueueBuckets();
            var aboutToLoseNext = new AbilityQueueBuckets();
            if (dead0)
                InvokeTrigger(Trigger.AboutToLose, null, null, battleState.TimeMs, side0, side1, aboutToLoseCurrent, aboutToLoseNext, null, 0);
            if (dead1)
                InvokeTrigger(Trigger.AboutToLose, null, null, battleState.TimeMs, side0, side1, aboutToLoseCurrent, aboutToLoseNext, null, 1);
            while (!aboutToLoseCurrent.IsEmpty)
            {
                for (int pbi = 0; pbi < AbilityQueueBuckets.BucketCount; pbi++)
                {
                    var bucket = aboutToLoseCurrent.Bucket(pbi);
                    for (int bj = 0; bj < bucket.Count; bj++)
                    {
                        var abilityRef = bucket[bj];
                        if (!_abilityStates.TryGetValue(abilityRef.Id, out var state))
                            continue;
                        ItemState? invokeTarget = null;
                        if (state.InvokeTargets != null && state.InvokeTargets.Count > 0)
                        {
                            invokeTarget = state.InvokeTargets[0];
                            state.InvokeTargets.RemoveAt(0);
                        }
                        ExecuteOneEffect(abilityRef, invokeTarget, side0, side1, battleState.TimeMs, logSink, null, aboutToLoseCurrent, aboutToLoseNext);
                        state.PendingCount = Math.Max(0, state.PendingCount - 1);
                        if (state.PendingCount > 0)
                            AddEntryToBucketsByAbilityPriority(aboutToLoseNext, abilityRef);
                    }
                    bucket.Clear();
                }
            }
            dead0 = side0.Hp <= 0;
            dead1 = side1.Hp <= 0;
            if (dead0 && dead1)
            {
                logSink.OnHpSnapshot(battleState.TimeMs, side0.Hp, side1.Hp);
                logSink.OnResult(-1, battleState.TimeMs, true);
                return -1;
            }
            if (dead1)
            {
                logSink.OnHpSnapshot(battleState.TimeMs, side0.Hp, side1.Hp);
                logSink.OnResult(0, battleState.TimeMs, false);
                return 0;
            }
            if (dead0)
            {
                logSink.OnHpSnapshot(battleState.TimeMs, side0.Hp, side1.Hp);
                logSink.OnResult(1, battleState.TimeMs, false);
                return 1;
            }

            // 11. 沙尘暴 120s 结束判平局
            if (battleState.TimeMs >= SandstormEndMs)
            {
                logSink.OnHpSnapshot(battleState.TimeMs, side0.Hp, side1.Hp);
                logSink.OnResult(-1, battleState.TimeMs, true);
                return -1;
            }
        }
    }

    /// <summary>物品是否具备可暴击的六类数值之一；依据模板的 Tag（护盾/伤害/灼烧/剧毒/治疗/再生）。</summary>
    private static bool ItemHasAnyCrittableField(ItemState item)
    {
        int tags = item.GetAttribute(Key.Tags) | item.GetAttribute(Key.DerivedTags);
        return (tags & DerivedTag.Damage) != 0 || (tags & DerivedTag.Burn) != 0 || (tags & DerivedTag.Poison) != 0
            || (tags & DerivedTag.Heal) != 0 || (tags & DerivedTag.Shield) != 0 || (tags & DerivedTag.Regen) != 0;
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
            var item = new ItemState(t, entry.Tier);
            ApplyOverrides(item, entry.Overrides);
            side.Items.Add(item);
        }
        return side;
    }

    private static BattleSessionTables BuildSessionTables(BattleSide side0, BattleSide side1)
    {
        var tables = new BattleSessionTables();
        foreach (var side in new[] { side0, side1 })
        {
            foreach (var item in side.Items)
            {
                foreach (var ability in item.Abilities)
                {
                    var abilityRef = new RuntimeAbilityRef(item, ability);
                    tables.AbilityRefIdToState[abilityRef.Id] = new AbilityState();
                    foreach (var entry in ability.TriggerEntries)
                    {
                        if ((uint)entry.Trigger < (uint)Trigger.Count)
                        {
                            tables.AbilitiesByTrigger[entry.Trigger].Add(abilityRef);
                            if (!tables.AbilitiesByTriggerAndOwner.TryGetValue(entry.Trigger, out var byOwner))
                            {
                                byOwner = [];
                                tables.AbilitiesByTriggerAndOwner[entry.Trigger] = byOwner;
                            }
                            if (!byOwner.TryGetValue(item, out var list))
                            {
                                list = [];
                                byOwner[item] = list;
                            }
                            list.Add(abilityRef);
                        }
                    }
                }

                foreach (var aura in item.Template.Auras)
                {
                    tables.AllAuras.Add((item, aura));
                    if (!tables.AurasByAttribute.TryGetValue(aura.Attribute, out var list))
                    {
                        list = new List<(ItemState, AuraDefinition)>();
                        tables.AurasByAttribute[aura.Attribute] = list;
                    }
                    list.Add((item, aura));
                }
            }
        }
        return tables;
    }

    private static void ApplyOverrides(ItemState item, IReadOnlyDictionary<string, int>? overrides)
    {
        if (overrides == null || overrides.Count == 0) return;
        if (item.Template.OverridableAttributes == null || item.Template.OverridableAttributes.Count == 0) return;
        foreach (var kv in overrides)
        {
            if (!Key.TryGetKey(kv.Key, out int key)) continue;
            if (!item.Template.OverridableAttributes.ContainsKey(key)) continue;
            item.SetAttribute(key, kv.Value);
        }
    }

    /// <summary>condition ?? default：UseItem → SameAsCaster，Freeze/Slow/Crit/Destroy → SameSide，BattleStart → Always。</summary>
    private static Formula? EnsureTriggerCondition(int triggerName, Formula? condition)
    {
        if (triggerName == Trigger.UseItem) return condition ?? Condition.SameAsCaster;
        if (triggerName == Trigger.Freeze) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Slow) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Haste) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Crit) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Destroy) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Burn) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.BattleStart) return condition ?? Condition.Always;
        if (triggerName == Trigger.Ammo) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.AboutToLose) return condition ?? Condition.SameSide;
        return condition;
    }

    /// <summary>按条目对应能力的优先级将 <paramref name="entry"/> 写入 <paramref name="buckets"/> 的桶尾（FIFO）。</summary>
    private static void AddEntryToBucketsByAbilityPriority(AbilityQueueBuckets buckets, RuntimeAbilityRef abilityRef)
    {
        buckets.AddToBucket(AbilityQueueBuckets.BucketIndex(abilityRef.Ability.Priority), abilityRef);
    }

    private static void ProcessCooldown(BattleSide side, int timeMs, List<ItemState> castQueue)
    {
        for (int i = 0; i < side.Items.Count; i++)
        {
            var item = side.Items[i];
            if (item.Destroyed) continue;
            int cooldownMs = side.GetItemInt(i, Key.CooldownMs);
            if (cooldownMs <= 0) continue;
            if (item.FreezeRemainingMs > 0) continue; // 冰冻不推进冷却
            int advanceMs = FrameMs;
            if (item.HasteRemainingMs > 0) advanceMs *= 2;
            if (item.SlowRemainingMs > 0) advanceMs /= 2;
            int cap = Math.Max(1, cooldownMs / 20);
            advanceMs = Math.Min(advanceMs, cap);
            item.ChargedTimeMs += advanceMs;
            if (item.ChargedTimeMs >= cooldownMs)
            {
                int ammoCap = side.GetItemInt(i, Key.AmmoCap);
                if (ammoCap > 0 && item.AmmoRemaining <= 0) continue;
                item.ChargedTimeMs = 0;
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

    /// <summary>将能力加入队列或合并 PendingCount：先查 current 各桶，再查 next 各桶；都没有则新建并按优先级入对应桶，仅 Immediate 入 current。当 invokeTargetSideIndex 与 invokeTargetItemIndex 均非 null 时不合并，新建条目且效果应对该 invoke 目标施加。</summary>
    private void AddOrMergeAbility(RuntimeAbilityRef abilityRef, int pendingCount,
        AbilityQueueBuckets current, AbilityQueueBuckets next, ItemState? invokeTarget = null)
    {
        if (!_abilityStates.TryGetValue(abilityRef.Id, out var state))
        {
            state = new AbilityState();
            _abilityStates[abilityRef.Id] = state;
        }
        bool shouldEnqueue = state.PendingCount <= 0;
        state.PendingCount += pendingCount;
        if (invokeTarget != null)
        {
            state.InvokeTargets ??= new List<ItemState>(4);
            for (int i = 0; i < pendingCount; i++)
                state.InvokeTargets.Add(invokeTarget);
        }
        if (!shouldEnqueue) return;
        int b = AbilityQueueBuckets.BucketIndex(abilityRef.Ability.Priority);
        if (abilityRef.Ability.Priority == AbilityPriority.Immediate) current.AddToBucket(b, abilityRef);
        else next.AddToBucket(b, abilityRef);
    }

    /// <summary>统一触发器调用：给定触发器名、引起触发的物品与上下文，遍历双方所有物品；条件匹配的能力入队（Immediate→current，其余→next）。若传入 executeImmediate，则 Immediate 能力不入队、当场执行。onlyForSideIndex 非空时仅遍历该侧（用于即将落败等仅对单侧触发）。</summary>
    private void InvokeTrigger(int triggerName, ItemState? causeItem, TriggerInvokeContext? context, int timeMs,
        BattleSide side0, BattleSide side1, AbilityQueueBuckets current, AbilityQueueBuckets next,
        Action<ItemState, int, AbilityDefinition>? executeImmediate = null, int? onlyForSideIndex = null)
    {
        int pendingCount = (triggerName == Trigger.UseItem || triggerName == Trigger.Freeze || triggerName == Trigger.Slow || triggerName == Trigger.Haste || triggerName == Trigger.Burn || triggerName == Trigger.Poison) && context?.Multicast is int m ? m : 1;
        // 规定次序：onlyForSideIndex 指定时仅该侧；有 causeItem 时先处理 causeItem 所在侧；无 causeItem 时按 (0, side0), (1, side1) 顺序。
        BattleSimulatorThreadScratch.BeginInvokeTrigger();
        try
        {
            var triggerState = BattleSimulatorThreadScratch.CurrentInvokeBattleState();
            triggerState.Side[0] = side0;
            triggerState.Side[1] = side1;
            triggerState.SessionTables = _sessionTables;
            triggerState.TimeMs = timeMs;
            var triggerCtx = BattleSimulatorThreadScratch.CurrentInvokeContext();
            triggerCtx.BattleState = triggerState;

            void VisitOwnerSide(int ownerSideIndex, BattleSide ownerSide)
            {
                BattleSide mySide = ownerSide;
                BattleSide enemySide = ownerSideIndex == 0 ? side1 : side0;
                var indices = BattleSimulatorThreadScratch.CurrentInvokeIndices();
                indices.Clear();
                if (causeItem != null && !causeItem.Destroyed && ownerSideIndex == causeItem.SideIndex && causeItem.ItemIndex < ownerSide.Items.Count)
                {
                    indices.Add(causeItem.ItemIndex);
                    for (int i = 0; i < ownerSide.Items.Count; i++)
                        if (i != causeItem.ItemIndex) indices.Add(i);
                }
                else
                {
                    for (int i = 0; i < ownerSide.Items.Count; i++) indices.Add(i);
                }

                foreach (int ownerItemIndex in indices)
                {
                    var abilityOwner = ownerSide.Items[ownerItemIndex];
                    if (abilityOwner.Destroyed) continue;
                    if (!_sessionTables!.AbilitiesByTriggerAndOwner.TryGetValue(triggerName, out var byOwner)
                        || !byOwner.TryGetValue(abilityOwner, out var ownerAbilities))
                        continue;
                    foreach (var abilityRef in ownerAbilities)
                    {
                        var ab = abilityRef.Ability;
                        bool matched = false;
                        foreach (var entry in ab.TriggerEntries)
                        {
                            if (entry.Trigger != triggerName) continue;
                            triggerCtx.Item = abilityOwner;
                            triggerCtx.Caster = abilityOwner;
                            triggerCtx.Source = causeItem ?? abilityOwner;
                            triggerCtx.InvokeTarget = context?.InvokeTargetItem;
                            if (entry.Condition.Evaluate(triggerCtx) == 0) continue;
                            matched = true;
                            break;
                        }

                        if (!matched) continue;
                        if (ab.Priority == AbilityPriority.Immediate && executeImmediate != null)
                        {
                            executeImmediate(abilityOwner, 0, ab);
                            continue;
                        }
                        AddOrMergeAbility(abilityRef, pendingCount, current, next, context?.InvokeTargetItem);
                    }
                }
            }

            if (onlyForSideIndex is int ofs)
                VisitOwnerSide(ofs, ofs == 0 ? side0 : side1);
            else if (causeItem != null && !causeItem.Destroyed)
            {
                VisitOwnerSide(causeItem.SideIndex, causeItem.SideIndex == 0 ? side0 : side1);
                VisitOwnerSide(1 - causeItem.SideIndex, causeItem.SideIndex == 0 ? side1 : side0);
            }
            else
            {
                VisitOwnerSide(0, side0);
                VisitOwnerSide(1, side1);
            }
        }
        finally
        {
            BattleSimulatorThreadScratch.EndInvokeTrigger();
        }
    }

    /// <summary>暴击时最终倍率 = CritDamagePercent/100，默认 200 即 2 倍；利爪翻倍为 400 即 4 倍。chargeInducedCastQueue 非 null 时充能导致满会加入该队列。执行过程中引发的新能力通过 AddOrMergeAbility/Invoke*Trigger 加入 current/next（仅 Immediate 入 current）。</summary>
    private void ExecuteOneEffect(RuntimeAbilityRef abilityRef, ItemState? invokeTargetItem,
        BattleSide side0, BattleSide side1, int timeMs, IBattleLogSink logSink, List<ItemState>? chargeInducedCastQueue,
        AbilityQueueBuckets currentAbilityQueue, AbilityQueueBuckets nextAbilityQueue)
    {
        var item = abilityRef.Owner;
        var ability = abilityRef.Ability;
        if (ability.Apply == null) return;
        BattleSimulatorThreadScratch.BeginExecuteOneEffect(out var ctx, out var battleState);
        try
        {
            battleState.Side[0] = side0;
            battleState.Side[1] = side1;
            battleState.SessionTables = _sessionTables;
            battleState.TimeMs = timeMs;
            battleState.LogSink = logSink;
            battleState.TriggerInvoker = (triggerName, causeItem, targetItem, multicast) =>
            {
                var triggerCtx = new TriggerInvokeContext { InvokeTargetItem = targetItem, Multicast = multicast };
                InvokeTrigger(triggerName, causeItem, triggerCtx, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
            };
            ctx.BattleState = battleState;
            ctx.Item = item;
            ctx.Caster = item;
            ctx.Source = item;
            ctx.InvokeTarget = invokeTargetItem;
            if (chargeInducedCastQueue != null)
                battleState.CastQueue.Clear();
            ability.Apply(ctx, ability);
            if (chargeInducedCastQueue != null)
            {
                for (int i = 0; i < battleState.CastQueue.Count; i++)
                    chargeInducedCastQueue.Add(battleState.CastQueue[i]);
            }
        }
        finally
        {
            BattleSimulatorThreadScratch.EndExecuteOneEffect();
        }
    }
}
