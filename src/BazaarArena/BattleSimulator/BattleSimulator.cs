using System.Linq;
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

            // 7-8. 施放队列产生的能力加入 nextAbilityQueue（下一帧才处理）；步骤 8 只处理 currentAbilityQueue；延后/未消耗的入 nextAbilityQueue；仅步骤 9 将 nextAbilityQueue 移入 currentAbilityQueue
            do
            {
                var toProcess = new List<BattleItemState>(castQueue);
                castQueue.Clear();
                // 7. 遍历本轮施放队列，用触发器调用方式加入能力队列（先合并 current 再 next，都没有则入 next；仅 Immediate 入 current）
                foreach (var item in toProcess)
                {
                    var side = item.SideIndex == 0 ? side0 : side1;
                    int ammoCap = side.GetItemInt(item.ItemIndex, Key.AmmoCap, 0);
                    int multicast = side.GetItemInt(item.ItemIndex, Key.Multicast, 1);
                    InvokeTrigger(Trigger.UseItem, item, new TriggerInvokeContext { Multicast = multicast, UsedTemplate = item.Template, InvokeTargetItem = item }, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue,
                        executeImmediate: (owner, abilityIdx, ability) =>
                        {
                            var entry = new AbilityQueueEntry { Owner = owner, AbilityIndex = abilityIdx, PendingCount = 1 };
                            ExecuteOneEffect(owner, ability, entry, false, 200, side0, side1, timeMs, logSink, castQueue, currentAbilityQueue, nextAbilityQueue);
                        });
                    if (ammoCap > 0)
                        item.AmmoRemaining = Math.Max(0, item.AmmoRemaining - 1);
                    logSink.OnCast(item, item.Template.Name, timeMs, ammoCap > 0 ? item.AmmoRemaining : null);
                    if (ammoCap > 0)
                        InvokeTrigger(Trigger.Ammo, item, null, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                }

                // 8. 处理能力队列（只处理 currentAbilityQueue）；仅按优先级排序，同优先级保持入队顺序。与上次触发间隔不足 250ms 的条目直接加入 next 留到下一帧再判。
                var toProcessAbilities = currentAbilityQueue
                    .OrderBy(e => (int)e.Owner.Template.Abilities[e.AbilityIndex].Priority)
                    .ToList();
                currentAbilityQueue.Clear();
                foreach (var entry in toProcessAbilities)
                {
                    var item = entry.Owner;
                    if (timeMs - item.GetLastTriggerMs(entry.AbilityIndex) < TriggerIntervalMs)
                    {
                        nextAbilityQueue.Add(entry);
                        continue;
                    }
                    var side = item.SideIndex == 0 ? side0 : side1;
                    var opp = item.SideIndex == 0 ? side1 : side0;
                    item.SetLastTriggerMs(entry.AbilityIndex, timeMs);
                    var ability = item.Template.Abilities[entry.AbilityIndex];
                    bool canCrit = ItemHasAnyCrittableField(item) && ability.Apply != null && ability.ApplyCritMultiplier && ability.UseSelf;
                    bool isCrit = false;
                    int critDamagePercent = 200;
                    if (canCrit)
                    {
                        if (item.CritTimeMs == timeMs)
                        {
                            isCrit = item.IsCritThisUse;
                            critDamagePercent = item.CritDamagePercentThisUse;
                        }
                        else
                        {
                            var auraContext = new BattleAuraContext(side, item, opp);
                            int critRate = item.Template.GetInt(Key.CritRatePercent, item.Tier, 0, auraContext);
                            if (critRate > 0 && Random.Shared.Next(100) < critRate)
                            {
                                isCrit = true;
                                critDamagePercent = item.Template.GetInt(Key.CritDamagePercent, item.Tier, 200, auraContext);
                            }
                            item.CritTimeMs = timeMs;
                            item.IsCritThisUse = isCrit;
                            item.CritDamagePercentThisUse = critDamagePercent;
                            if (isCrit)
                                InvokeTrigger(Trigger.Crit, item, null, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
                        }
                    }
                    ExecuteOneEffect(item, ability, entry, isCrit, critDamagePercent, side0, side1, timeMs, logSink, castQueue, currentAbilityQueue, nextAbilityQueue);
                    entry.PendingCount--;
                    if (entry.PendingCount > 0)
                        nextAbilityQueue.Add(entry);
                }
            } while (castQueue.Count > 0);

            // 9. 能力队列更新到下一帧（移入 currentAbilityQueue，供下一帧步骤 8 处理）
            currentAbilityQueue.Clear();
            foreach (var e in nextAbilityQueue) currentAbilityQueue.Add(e);
            nextAbilityQueue.Clear();

            // 10. 胜负判定（先输出本帧结算后的最终生命，再通知结果）。即将落败时触发 AboutToLose（Hp≤0 即触发），「首次」由物品 Custom_0 保证（参考靴里剑），仅执行 Immediate 能力后重判。
            bool dead0 = side0.Hp <= 0;
            bool dead1 = side1.Hp <= 0;
            var aboutToLoseCurrent = new List<AbilityQueueEntry>();
            var aboutToLoseNext = new List<AbilityQueueEntry>();
            if (dead0)
                InvokeTrigger(Trigger.AboutToLose, null, null, timeMs, side0, side1, aboutToLoseCurrent, aboutToLoseNext, null, 0);
            if (dead1)
                InvokeTrigger(Trigger.AboutToLose, null, null, timeMs, side0, side1, aboutToLoseCurrent, aboutToLoseNext, null, 1);
            while (aboutToLoseCurrent.Count > 0)
            {
                var toProcess = aboutToLoseCurrent.OrderBy(e => (int)e.Owner.Template.Abilities[e.AbilityIndex].Priority).ToList();
                aboutToLoseCurrent.Clear();
                foreach (var entry in toProcess)
                {
                    var item = entry.Owner;
                    var ability = item.Template.Abilities[entry.AbilityIndex];
                    ExecuteOneEffect(item, ability, entry, false, 200, side0, side1, timeMs, logSink, null, aboutToLoseCurrent, aboutToLoseNext);
                }
            }
            dead0 = side0.Hp <= 0;
            dead1 = side1.Hp <= 0;
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

            // 11. 沙尘暴 120s 结束判平局
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
                        Hero = t.Hero,
                        Tags = [..t.Tags],
                        Abilities = t.Abilities.Select(a =>
                        {
                            var def = new AbilityDefinition
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
                                UseSelf = a.UseSelf,
                                Apply = a.Apply,
                                EffectLogName = a.EffectLogName,
                                TargetCountKey = a.TargetCountKey,
                                Triggers = a.Triggers?.Select(e => new AbilityDefinition.TriggerEntry
                                {
                                    TriggerName = e.TriggerName,
                                    Condition = Condition.Clone(e.Condition),
                                    SourceCondition = Condition.Clone(e.SourceCondition),
                                    InvokeTargetCondition = Condition.Clone(e.InvokeTargetCondition),
                                }).ToList(),
                            };
                            def.EnsureTriggersInitializedFromTopLevel();
                            return def;
                        }).ToList(),
                Auras = t.Auras.Select(a => new AuraDefinition { AttributeName = a.AttributeName, Condition = Condition.Clone(a.Condition), SourceCondition = Condition.Clone(a.SourceCondition), Value = a.Value, Percent = a.Percent }).ToList(),
            };
            clone.SetIntsByTier(t.GetIntsByTierSnapshot());
            // 仅对模板声明为可复写的属性应用 Overrides，避免卡组中多出的键（或旧版序列化）覆盖 Multicast、AmmoCap、Damage 等模板字段导致效果失效
            if (entry.Overrides != null && t.OverridableAttributes != null)
            {
                foreach (var kv in entry.Overrides)
                {
                    if (t.OverridableAttributes.ContainsKey(kv.Key))
                        clone.SetInt(kv.Key, kv.Value);
                }
            }
            side.Items.Add(new BattleItemState(clone, entry.Tier));
        }
        return side;
    }

    /// <summary>condition ?? default：UseItem → SameAsSource，Freeze/Slow/Crit/Destroy → SameSide，BattleStart → Always。</summary>
    private static Condition? EnsureTriggerCondition(string triggerName, Condition? condition)
    {
        if (triggerName == Trigger.UseItem) return condition ?? Condition.SameAsSource;
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

    /// <summary>将能力加入队列或合并 PendingCount：先查 currentAbilityQueue，再查 nextAbilityQueue；都没有则新建，仅 Immediate 入 current，其余入 next。当 invokeTargetSideIndex 与 invokeTargetItemIndex 均非 null 时不合并，新建条目且效果应对该 invoke 目标施加。</summary>
    private static void AddOrMergeAbility(BattleItemState owner, int abilityIdx, AbilityDefinition ability, int pendingCount,
        List<AbilityQueueEntry> current, List<AbilityQueueEntry> next,
        int? invokeTargetSideIndex = null, int? invokeTargetItemIndex = null)
    {
        if (invokeTargetSideIndex is int si && invokeTargetItemIndex is int ii)
        {
            var entry = new AbilityQueueEntry
            {
                Owner = owner,
                AbilityIndex = abilityIdx,
                PendingCount = pendingCount,
                InvokeTargetSideIndex = si,
                InvokeTargetItemIndex = ii,
            };
            if (ability.Priority == AbilityPriority.Immediate)
                current.Add(entry);
            else
                next.Add(entry);
            return;
        }
        var existing = current.FirstOrDefault(e => e.Owner == owner && e.AbilityIndex == abilityIdx && e.InvokeTargetSideIndex == null);
        if (existing != null) { existing.PendingCount += pendingCount; return; }
        existing = next.FirstOrDefault(e => e.Owner == owner && e.AbilityIndex == abilityIdx && e.InvokeTargetSideIndex == null);
        if (existing != null) { existing.PendingCount += pendingCount; return; }
        var newEntry = new AbilityQueueEntry
        {
            Owner = owner,
            AbilityIndex = abilityIdx,
            PendingCount = pendingCount,
        };
        if (ability.Priority == AbilityPriority.Immediate)
            current.Add(newEntry);
        else
            next.Add(newEntry);
    }

    /// <summary>统一触发器调用：给定触发器名、引起触发的物品与上下文，遍历双方所有物品；条件匹配的能力入队（Immediate→current，其余→next）。若传入 executeImmediate，则 Immediate 能力不入队、当场执行。onlyForSideIndex 非空时仅遍历该侧（用于即将落败等仅对单侧触发）。</summary>
    private static void InvokeTrigger(string triggerName, BattleItemState? causeItem, TriggerInvokeContext? context, int timeMs,
        BattleSide side0, BattleSide side1, List<AbilityQueueEntry> current, List<AbilityQueueEntry> next,
        Action<BattleItemState, int, AbilityDefinition>? executeImmediate = null, int? onlyForSideIndex = null)
    {
        int pendingCount = (triggerName == Trigger.UseItem || triggerName == Trigger.Freeze || triggerName == Trigger.Slow || triggerName == Trigger.Haste || triggerName == Trigger.Burn || triggerName == Trigger.Poison) && context?.Multicast is int m ? m : 1;
        Func<BattleItemState, IReadOnlySet<string>> getEffectiveTags = item => EffectiveTagHelper.GetEffectiveTags(side0, side1, item);

        // 规定次序：onlyForSideIndex 指定时仅该侧；有 causeItem 时先处理 causeItem 所在侧；无 causeItem 时按 (0, side0), (1, side1) 顺序。
        var sideOrder = onlyForSideIndex is int ofs
            ? new[] { (ofs, ofs == 0 ? side0 : side1) }
            : causeItem != null && !causeItem.Destroyed
                ? new[] { (causeItem.SideIndex, causeItem.SideIndex == 0 ? side0 : side1), (1 - causeItem.SideIndex, causeItem.SideIndex == 0 ? side1 : side0) }
                : new[] { (0, side0), (1, side1) };

        foreach (var (ownerSideIndex, ownerSide) in sideOrder)
        {
            BattleSide mySide = ownerSide;
            BattleSide enemySide = ownerSideIndex == 0 ? side1 : side0;
            // 该侧物品下标顺序：有 causeItem 且本侧为 cause 所在侧时，先 causeItem.ItemIndex，再其余下标；否则 0..Count-1。
            var indices = new List<int>();
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
                for (int a = 0; a < abilityOwner.Template.Abilities.Count; a++)
                {
                    var ab = abilityOwner.Template.Abilities[a];
                        ab.EnsureTriggersInitializedFromTopLevel();
                        if (ab.Triggers == null || ab.Triggers.Count == 0)
                        {
                            if (ab.TriggerName != triggerName) continue;
                            var conditionCtx = new ConditionContext
                            {
                                MySide = mySide,
                                EnemySide = enemySide,
                                Item = causeItem,
                                Source = abilityOwner,
                                GetEffectiveTagsForItem = getEffectiveTags,
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
                                    GetEffectiveTagsForItem = getEffectiveTags,
                                };
                                if (!ab.SourceCondition.Evaluate(sourceConditionCtx)) continue;
                            }
                            if (ab.InvokeTargetCondition != null && context?.InvokeTargetItem is { } invokeTargetItem0)
                            {
                                var invokeTargetCtx = new ConditionContext
                                {
                                    MySide = mySide,
                                    EnemySide = enemySide,
                                    Item = invokeTargetItem0,
                                    Source = abilityOwner,
                                    GetEffectiveTagsForItem = getEffectiveTags,
                                };
                                if (!ab.InvokeTargetCondition.Evaluate(invokeTargetCtx)) continue;
                            }
                            if (ab.Priority == AbilityPriority.Immediate && executeImmediate != null)
                            {
                                executeImmediate(abilityOwner, a, ab);
                                continue;
                            }
                            AddOrMergeAbility(abilityOwner, a, ab, pendingCount, current, next, context?.InvokeTargetItem?.SideIndex, context?.InvokeTargetItem?.ItemIndex);
                            continue;
                        }

                        bool matched = false;
                        foreach (var entry in ab.Triggers)
                        {
                            if (entry.TriggerName != triggerName) continue;
                            var conditionCtx = new ConditionContext
                            {
                                MySide = mySide,
                                EnemySide = enemySide,
                                Item = causeItem,
                                Source = abilityOwner,
                                GetEffectiveTagsForItem = getEffectiveTags,
                            };
                            if (entry.Condition != null && !entry.Condition.Evaluate(conditionCtx)) continue;
                            if (entry.SourceCondition != null)
                            {
                                var sourceConditionCtx = new ConditionContext
                                {
                                    MySide = mySide,
                                    EnemySide = enemySide,
                                    Item = abilityOwner,
                                    Source = abilityOwner,
                                    GetEffectiveTagsForItem = getEffectiveTags,
                                };
                                if (!entry.SourceCondition.Evaluate(sourceConditionCtx)) continue;
                            }
                            if (entry.InvokeTargetCondition != null && context?.InvokeTargetItem is { } invokeTargetItem)
                            {
                                var invokeTargetCtx = new ConditionContext
                                {
                                    MySide = mySide,
                                    EnemySide = enemySide,
                                    Item = invokeTargetItem,
                                    Source = abilityOwner,
                                    GetEffectiveTagsForItem = getEffectiveTags,
                                };
                                if (!entry.InvokeTargetCondition.Evaluate(invokeTargetCtx)) continue;
                            }
                            matched = true;
                            break;
                        }

                        if (!matched) continue;
                        if (ab.Priority == AbilityPriority.Immediate && executeImmediate != null)
                        {
                            executeImmediate(abilityOwner, a, ab);
                            continue;
                        }
                        AddOrMergeAbility(abilityOwner, a, ab, pendingCount, current, next, context?.InvokeTargetItem?.SideIndex, context?.InvokeTargetItem?.ItemIndex);
                }
            }
        }
    }

    /// <summary>暴击时最终倍率 = CritDamagePercent/100，默认 200 即 2 倍；利爪翻倍为 400 即 4 倍。chargeInducedCastQueue 非 null 时充能导致满会加入该队列。执行过程中引发的新能力通过 AddOrMergeAbility/Invoke*Trigger 加入 current/next（仅 Immediate 入 current）。</summary>
    private void ExecuteOneEffect(BattleItemState item, AbilityDefinition ability, AbilityQueueEntry queueEntry, bool isCrit, int critDamagePercent,
        BattleSide side0, BattleSide side1, int timeMs, IBattleLogSink logSink, List<BattleItemState>? chargeInducedCastQueue,
        List<AbilityQueueEntry> currentAbilityQueue, List<AbilityQueueEntry> nextAbilityQueue)
    {
        if (ability.Apply == null) return;
        var side = item.SideIndex == 0 ? side0 : side1;
        var opp = item.SideIndex == 0 ? side1 : side0;
        BattleItemState? invokeTargetItem = null;
        if (queueEntry.InvokeTargetSideIndex is int si && queueEntry.InvokeTargetItemIndex is int ii)
        {
            var targetSide = si == side0.SideIndex ? side0 : side1;
            if (ii >= 0 && ii < targetSide.Items.Count)
                invokeTargetItem = targetSide.Items[ii];
        }
        int critMultiplier = isCrit ? Math.Max(1, critDamagePercent / 100) : 1;
        int value = 0;
        if (ability.ValueKey != null)
        {
            int baseValue = ability.ResolveValue(item.Template, item.Tier, ability.ValueKey);
            bool applyCrit = ItemHasAnyCrittableField(item) && ability.ApplyCritMultiplier;
            value = applyCrit ? baseValue * critMultiplier : baseValue;
        }
        var effectAppliedTriggerQueue = new List<(string TriggerName, int SideIndex, int ItemIndex)>();
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
            EffectAppliedTriggerQueue = effectAppliedTriggerQueue,
            TargetCondition = ability.TargetCondition,
            EffectLogName = ability.EffectLogName,
            TargetCountKey = ability.TargetCountKey,
            InvokeTargetItem = invokeTargetItem,
        };
        ability.Apply(ctx);
        foreach (var (triggerName, sideIndex, itemIndex) in effectAppliedTriggerQueue)
        {
            var target = (sideIndex == side0.SideIndex ? side0 : side1).Items[itemIndex];
            // Burn/Poison/Shield 的 queue 存的是施加者（己方），故 causeItem = target；Freeze/Slow/Destroy 存的是目标，causeItem = item（施放者）
            var causeItem = (triggerName == Trigger.Burn || triggerName == Trigger.Poison || triggerName == Trigger.Shield) ? target : item;
            var context = new TriggerInvokeContext { InvokeTargetItem = (triggerName == Trigger.Burn || triggerName == Trigger.Poison || triggerName == Trigger.Shield) ? null : target, Multicast = triggerName == Trigger.Destroy ? null : 1 };
            InvokeTrigger(triggerName, causeItem, context, timeMs, side0, side1, currentAbilityQueue, nextAbilityQueue);
            if (triggerName == Trigger.Destroy)
                target.Destroyed = true;
        }
    }
}
