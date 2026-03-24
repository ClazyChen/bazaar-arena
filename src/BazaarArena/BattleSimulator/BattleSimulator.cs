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

        var battleState = new BattleState();
        battleState.Side[0] = side0;
        battleState.Side[1] = side1;
        battleState.SessionTables = _sessionTables;
        battleState.AbilityStates.Clear();
        var abilitySeen = new HashSet<int>();
        foreach (var list in _sessionTables.AbilitiesByTrigger)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int abilityId = list[i];
                if (!abilitySeen.Add(abilityId)) continue;
                battleState.AbilityStates[abilityId] = new AbilityState();
            }
        }
        battleState.LogSink = logSink;

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
                battleState.InvokeTrigger(Trigger.BattleStart, null, null, 0);

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
                    using (battleState.BeginInvokeScope(
                        executeImmediate: (abilityId) =>
                        {
                            ExecuteOneEffect(abilityId, null, side0, side1, battleState.TimeMs, logSink, battleState.CastQueue, battleState.CurrentAbilityBuckets, battleState.NextAbilityBuckets, battleState.AbilityStates);
                        }))
                    {
                        battleState.InvokeTrigger(Trigger.UseItem, item, item, multicast);
                    }
                    if (ammoCap > 0)
                        item.AmmoRemaining = Math.Max(0, item.AmmoRemaining - 1);
                    logSink.OnCast(item, item.Template.Name, battleState.TimeMs, ammoCap > 0 ? item.AmmoRemaining : null);
                    if (ammoCap > 0)
                        battleState.InvokeTrigger(Trigger.Ammo, item, null, 1);
                }

                // 8. 处理能力队列（只处理 current 六桶）；桶序 = Immediate→Lowest，桶内 FIFO。与上次触发间隔不足 250ms 的条目写入 next 对应桶留到下一帧再判。
                for (int pbi = 0; pbi < AbilityQueueBuckets.BucketCount; pbi++)
                {
                    var bucket = battleState.CurrentAbilityBuckets.Bucket(pbi);
                    for (int bj = 0; bj < bucket.Count; bj++)
                    {
                        int abilityId = bucket[bj];
                        if (!battleState.AbilityStates.TryGetValue(abilityId, out var state) || state.PendingCount <= 0)
                            continue;
                        var item = battleState.GetAbilityOwner(abilityId);
                        var ability = battleState.GetAbility(abilityId);
                        if (state.LastTriggerMs != int.MinValue && battleState.TimeMs - state.LastTriggerMs < TriggerIntervalMs)
                        {
                            AddEntryToBucketsByAbilityPriority(battleState, battleState.NextAbilityBuckets, abilityId);
                            continue;
                        }
                        var side = item.SideIndex == 0 ? side0 : side1;
                        var opp = item.SideIndex == 0 ? side1 : side0;
                        state.LastTriggerMs = battleState.TimeMs;
                        bool canCrit = item.GetAttribute(Key.CanCrit) != 0 && ability.Apply != null && ability.ApplyCritMultiplier && ability.UseSelf;
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
                                    battleState.InvokeTrigger(Trigger.Crit, item, null, 1);
                            }
                        }
                        ItemState? invokeTarget = null;
                        if (state.InvokeTargets != null && state.InvokeTargets.Count > 0)
                        {
                            invokeTarget = state.InvokeTargets[0];
                            state.InvokeTargets.RemoveAt(0);
                        }
                        ExecuteOneEffect(abilityId, invokeTarget, side0, side1, battleState.TimeMs, logSink, battleState.CastQueue, battleState.CurrentAbilityBuckets, battleState.NextAbilityBuckets, battleState.AbilityStates);
                        state.PendingCount--;
                        if (state.PendingCount > 0)
                            AddEntryToBucketsByAbilityPriority(battleState, battleState.NextAbilityBuckets, abilityId);
                    }
                    bucket.Clear();
                }
            } while (battleState.CastQueue.Count > 0);

            // 9. 能力队列更新到下一帧：对调 current/next，下一帧步骤 8 直接处理原 next 各桶；清空新的 next（原 current 已空桶，Clear 仅复位容量语义）
            (battleState.CurrentAbilityBuckets, battleState.NextAbilityBuckets) = (battleState.NextAbilityBuckets, battleState.CurrentAbilityBuckets);
            battleState.NextAbilityBuckets.Clear();

            // 10. 胜负判定（先输出本帧结算后的最终生命，再通知结果）。即将落败时触发 AboutToLose（Hp≤0 即触发），「首次」由物品 Custom_0 保证（参考靴里剑），仅执行 Immediate 能力后重判。
            bool dead0 = side0.Hp <= 0;
            bool dead1 = side1.Hp <= 0;
            var aboutToLoseCurrent = new AbilityQueueBuckets();
            var aboutToLoseNext = new AbilityQueueBuckets();
            if (dead0)
            {
                using (battleState.BeginInvokeScope(onlyForSideIndex: 0, current: aboutToLoseCurrent, next: aboutToLoseNext))
                    battleState.InvokeTrigger(Trigger.AboutToLose, null, null, 1);
            }
            if (dead1)
            {
                using (battleState.BeginInvokeScope(onlyForSideIndex: 1, current: aboutToLoseCurrent, next: aboutToLoseNext))
                    battleState.InvokeTrigger(Trigger.AboutToLose, null, null, 1);
            }
            while (!aboutToLoseCurrent.IsEmpty)
            {
                for (int pbi = 0; pbi < AbilityQueueBuckets.BucketCount; pbi++)
                {
                    var bucket = aboutToLoseCurrent.Bucket(pbi);
                    for (int bj = 0; bj < bucket.Count; bj++)
                    {
                        int abilityId = bucket[bj];
                        if (!battleState.AbilityStates.TryGetValue(abilityId, out var state))
                            continue;
                        ItemState? invokeTarget = null;
                        if (state.InvokeTargets != null && state.InvokeTargets.Count > 0)
                        {
                            invokeTarget = state.InvokeTargets[0];
                            state.InvokeTargets.RemoveAt(0);
                        }
                        ExecuteOneEffect(abilityId, invokeTarget, side0, side1, battleState.TimeMs, logSink, null, aboutToLoseCurrent, aboutToLoseNext, battleState.AbilityStates);
                        state.PendingCount = Math.Max(0, state.PendingCount - 1);
                        if (state.PendingCount > 0)
                            AddEntryToBucketsByAbilityPriority(battleState, aboutToLoseNext, abilityId);
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
                for (int abilityIndex = 0; abilityIndex < item.Template.Abilities.Count; abilityIndex++)
                {
                    var ability = item.Template.Abilities[abilityIndex];
                    int abilityId = BattleState.BuildAbilityId(item.SideIndex, item.ItemIndex, abilityIndex);
                    foreach (var entry in ability.TriggerEntries)
                    {
                        if ((uint)entry.Trigger < (uint)Trigger.Count)
                        {
                            tables.AbilitiesByTrigger[entry.Trigger].Add(abilityId);
                        }
                    }
                }

                for (int auraIndex = 0; auraIndex < item.Template.Auras.Count; auraIndex++)
                {
                    var aura = item.Template.Auras[auraIndex];
                    int auraId = BattleState.BuildAuraId(item.SideIndex, item.ItemIndex, auraIndex);
                    if (!tables.AurasByAttribute.TryGetValue(aura.Attribute, out var list))
                    {
                        list = new List<int>();
                        tables.AurasByAttribute[aura.Attribute] = list;
                    }
                    list.Add(auraId);
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

    /// <summary>按条目对应能力的优先级将 <paramref name="entry"/> 写入 <paramref name="buckets"/> 的桶尾（FIFO）。</summary>
    private static void AddEntryToBucketsByAbilityPriority(BattleState battleState, AbilityQueueBuckets buckets, int abilityId)
    {
        var ability = battleState.GetAbility(abilityId);
        buckets.AddToBucket(AbilityQueueBuckets.BucketIndex(ability.Priority), abilityId);
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

    /// <summary>暴击时最终倍率 = CritDamagePercent/100，默认 200 即 2 倍；利爪翻倍为 400 即 4 倍。chargeInducedCastQueue 非 null 时充能导致满会加入该队列。执行过程中引发的新能力通过 AddOrMergeAbility/Invoke*Trigger 加入 current/next（仅 Immediate 入 current）。</summary>
    private void ExecuteOneEffect(int abilityId, ItemState? invokeTargetItem,
        BattleSide side0, BattleSide side1, int timeMs, IBattleLogSink logSink, List<ItemState>? chargeInducedCastQueue,
        AbilityQueueBuckets currentAbilityQueue, AbilityQueueBuckets nextAbilityQueue, Dictionary<int, AbilityState> abilityStates)
    {
        int sideId = abilityId & 1;
        int abilityIndex = abilityId / 32;
        int itemId = (abilityId % 32) / 2;
        var side = sideId == 0 ? side0 : side1;
        var item = side.Items[itemId];
        var ability = item.Template.Abilities[abilityIndex];
        if (ability.Apply == null) return;
        var ctx = new BattleContext();
        var battleState = new BattleState();
        battleState.Side[0] = side0;
        battleState.Side[1] = side1;
        battleState.SessionTables = _sessionTables;
        battleState.AbilityStates.Clear();
        foreach (var kv in abilityStates)
            battleState.AbilityStates[kv.Key] = kv.Value;
        battleState.TimeMs = timeMs;
        battleState.LogSink = logSink;
        battleState.CurrentAbilityBuckets = currentAbilityQueue;
        battleState.NextAbilityBuckets = nextAbilityQueue;
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
}
