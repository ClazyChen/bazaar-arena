using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using System.Linq;

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
    private readonly Dictionary<Deck, PreparedDeck> _preparedDeckCache = [];

    private sealed class PreparedDeck
    {
        public required object ResolverRef { get; init; }
        public required int Signature { get; init; }
        public required int MaxHp { get; init; }
        public required int Shield { get; init; }
        public required int Regen { get; init; }
        public required List<ItemState> FlattenedItems { get; init; }
    }

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
        var prepared0 = PrepareDeck(deck1, resolver);
        var prepared1 = PrepareDeck(deck2, resolver);
        var side0 = prepared0 == null ? null : BuildSide(prepared0);
        var side1 = prepared1 == null ? null : BuildSide(prepared1);
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
        InitializeAmmoRemaining(side0, battleState);
        InitializeAmmoRemaining(side1, battleState);

        void RunAbilityApply(int abilityId, ItemState? invokeTargetItem, bool allowCastQueueEnqueue)
        {
            var item = battleState.GetAbilityOwner(abilityId);
            var ability = battleState.GetAbility(abilityId);
            if (ability.Apply == null) return;
            var ctx = new BattleContext
            {
                BattleState = battleState,
                Item = item,
                Caster = item,
                Source = item,
                InvokeTarget = invokeTargetItem,
                AllowCastQueueEnqueue = allowCastQueueEnqueue,
            };
            ability.Apply(ctx, ability);
        }

        void ExecuteImmediateAbility(int abilityId, ItemState? invokeTargetItem) =>
            RunAbilityApply(abilityId, invokeTargetItem, allowCastQueueEnqueue: true);
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
                battleState.InvokeTrigger(Trigger.BattleStart, null, null, 1);

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

            // 7-8. 施放队列产生的能力入 next 各桶（下一帧才处理）；步骤 8 只处理 current 六桶；延后/未消耗的入 next
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
                    battleState.InvokeTrigger(Trigger.UseItem, item, item, multicast, ExecuteImmediateAbility);
                    battleState.InvokeTrigger(Trigger.UseOtherItem, item, item, multicast, ExecuteImmediateAbility);
                    if (ammoCap > 0)
                        item.AmmoRemaining = Math.Max(0, item.AmmoRemaining - 1);
                    logSink.OnCast(item, item.Template.Name, battleState.TimeMs, ammoCap > 0 ? item.AmmoRemaining : null);
                    if (ammoCap > 0)
                        battleState.InvokeTrigger(Trigger.Ammo, item, null, 1);
                }

                // 8. 处理能力队列（只处理 current 六桶）；桶序 = Immediate→Lowest，桶内 FIFO。与上次触发间隔不足 250ms 的条目写入 next 对应桶留到下一帧再判。
                DrainCurrentAbilityBuckets(
                    battleState,
                    allowCastQueueEnqueue: true,
                    withThrottleAndCrit: true,
                    runAbilityApply: RunAbilityApply);
            } while (battleState.CastQueue.Count > 0);

            // 9. 胜负判定（先输出本帧结算后的最终生命，再通知结果）。即将落败时触发 AboutToLose（Hp≤0 即触发），「首次」由物品 Custom_0 保证（参考靴里剑）。
            bool dead0 = side0.Hp <= 0;
            bool dead1 = side1.Hp <= 0;
            if (dead0 || dead1)
                battleState.InvokeTrigger(Trigger.AboutToLose, null, null, 1);
            while (!battleState.CurrentAbilityBuckets.IsEmpty)
            {
                DrainCurrentAbilityBuckets(
                    battleState,
                    allowCastQueueEnqueue: false,
                    withThrottleAndCrit: false,
                    runAbilityApply: RunAbilityApply);
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

            // 10. 仅在本帧未结束时，对调 current/next，下一帧步骤 8 处理原 next；清空新的 next。
            battleState.SwapAbilityBuckets();
            battleState.NextAbilityBuckets.Clear();
            battleState.CastQueue.Clear();

            // 11. 沙尘暴 120s 结束判平局
            if (battleState.TimeMs >= SandstormEndMs)
            {
                logSink.OnHpSnapshot(battleState.TimeMs, side0.Hp, side1.Hp);
                logSink.OnResult(-1, battleState.TimeMs, true);
                return -1;
            }
        }
    }

    private PreparedDeck? PrepareDeck(Deck deck, IItemTemplateResolver resolver)
    {
        int signature = ComputeDeckSignature(deck);
        if (_preparedDeckCache.TryGetValue(deck, out var cached)
            && ReferenceEquals(cached.ResolverRef, resolver)
            && cached.Signature == signature)
        {
            return cached;
        }

        int maxHp = deck.PlayerOverrides?.GetValueOrDefault("MaxHp", LevelUpTable.GetMaxHp(deck.PlayerLevel)) ?? LevelUpTable.GetMaxHp(deck.PlayerLevel);
        int shield = deck.PlayerOverrides?.GetValueOrDefault("Shield", 0) ?? 0;
        int regen = deck.PlayerOverrides?.GetValueOrDefault("Regen", 0) ?? 0;
        var flattened = new List<ItemState>(deck.Slots.Count);
        foreach (var entry in deck.Slots)
        {
            var t = resolver.GetTemplate(entry.ItemName);
            if (t == null) return null;
            var item = new ItemState(t, entry.Tier);
            ApplyOverrides(item, entry.Overrides);
            flattened.Add(item);
        }

        var prepared = new PreparedDeck
        {
            ResolverRef = resolver,
            Signature = signature,
            MaxHp = maxHp,
            Shield = shield,
            Regen = regen,
            FlattenedItems = flattened,
        };
        _preparedDeckCache[deck] = prepared;
        return prepared;
    }

    private static BattleSide BuildSide(PreparedDeck prepared)
    {
        var side = new BattleSide
        {
            MaxHp = prepared.MaxHp,
            Shield = prepared.Shield,
            Regen = prepared.Regen,
        };
        side.Hp = side.MaxHp;
        foreach (var item in prepared.FlattenedItems)
        {
            side.Items.Add(new ItemState(item));
        }
        return side;
    }

    private static int ComputeDeckSignature(Deck deck)
    {
        var hash = new HashCode();
        hash.Add(deck.PlayerLevel);
        if (deck.PlayerOverrides != null)
        {
            foreach (var kv in deck.PlayerOverrides.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                hash.Add(kv.Key, StringComparer.Ordinal);
                hash.Add(kv.Value);
            }
        }
        hash.Add(deck.Slots.Count);
        foreach (var slot in deck.Slots)
        {
            hash.Add(slot.ItemName, StringComparer.Ordinal);
            hash.Add((int)slot.Tier);
            if (slot.Overrides != null)
            {
                foreach (var kv in slot.Overrides.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    hash.Add(kv.Key, StringComparer.Ordinal);
                    hash.Add(kv.Value);
                }
            }
        }
        return hash.ToHashCode();
    }

    private static void InitializeAmmoRemaining(BattleSide side, BattleState battleState)
    {
        for (int i = 0; i < side.Items.Count; i++)
        {
            var item = side.Items[i];
            int ammoCap = battleState.GetItemInt(item, Key.AmmoCap);
            if (ammoCap > 0 && item.AmmoRemaining <= 0)
                item.AmmoRemaining = ammoCap;
        }
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

    private static void DrainCurrentAbilityBuckets(
        BattleState battleState,
        bool allowCastQueueEnqueue,
        bool withThrottleAndCrit,
        Action<int, ItemState?, bool> runAbilityApply)
    {
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
                if (withThrottleAndCrit)
                {
                    if (state.LastTriggerMs != int.MinValue && battleState.TimeMs - state.LastTriggerMs < TriggerIntervalMs)
                    {
                        AddEntryToBucketsByAbilityPriority(battleState, battleState.NextAbilityBuckets, abilityId);
                        continue;
                    }
                    state.LastTriggerMs = battleState.TimeMs;
                    bool canCrit = item.GetAttribute(Key.CanCrit) != 0
                        && ability.Apply != null
                        && ability.ApplyCritMultiplier
                        && ability.TriggerEntries.Any(e => e.Trigger == Trigger.UseItem);
                    if (canCrit)
                    {
                        if (item.CritTimeMs != battleState.TimeMs)
                        {
                            bool isCrit = false;
                            int critDamagePercent = 200;
                            // 暴击判定应读取运行时生效值（含光环），否则类似「唯一伙伴+暴击率光环」无法生效。
                            var critCtx = new BattleContext
                            {
                                BattleState = battleState,
                                Item = item,
                                Caster = item,
                                Source = item,
                            };
                            int critRate = critCtx.GetItemInt(item, Key.CritRate);
                            if (critRate > 0 && ThreadLocalRandom.Next100() < critRate)
                            {
                                isCrit = true;
                                critDamagePercent = critCtx.GetItemInt(item, Key.CritDamage);
                                if (critDamagePercent <= 0) critDamagePercent = 200;
                            }
                            item.CritTimeMs = battleState.TimeMs;
                            item.IsCritThisUse = isCrit;
                            item.CritDamage = critDamagePercent;
                            if (isCrit)
                                battleState.InvokeTrigger(Trigger.Crit, item, null, 1);
                        }
                    }
                }
                ItemState? invokeTarget = null;
                if (state.InvokeTargets != null && state.InvokeTargets.Count > 0)
                    invokeTarget = state.InvokeTargets.Dequeue();
                runAbilityApply(abilityId, invokeTarget, allowCastQueueEnqueue);
                state.PendingCount = Math.Max(0, state.PendingCount - 1);
                if (state.PendingCount > 0)
                    AddEntryToBucketsByAbilityPriority(battleState, battleState.NextAbilityBuckets, abilityId);
            }
            bucket.Clear();
        }
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

}
