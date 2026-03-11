using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>效果应用上下文：实现 IEffectApplyContext，供 EffectDefinition.Apply 委托使用。</summary>
internal sealed class EffectApplyContextImpl : IEffectApplyContext
{
    public required BattleSide Side { get; init; }
    public required BattleSide Opp { get; init; }
    public required BattleItemState Item { get; init; }
    public int Value { get; init; }
    public int CritMultiplier { get; init; }
    public bool IsCrit { get; init; }
    public int TimeMs { get; init; }
    public required IBattleLogSink LogSink { get; init; }
    public int SideIndex { get; init; }
    public int ItemIndex { get; init; }
    public List<(int, int)>? ChargeInducedCastQueue { get; init; }
    /// <summary>己方施加冻结后由模拟器注入，用于触发「触发冻结」能力入队（传入本次冻结目标 (sideIndex, itemIndex) 列表）。</summary>
    public Action<IReadOnlyList<(int sideIndex, int itemIndex)>>? OnFreezeApplied { get; init; }
    /// <summary>己方施加减速后由模拟器注入，用于触发「触发减速」能力入队（传入本次减速目标 (sideIndex, itemIndex) 列表）。</summary>
    public Action<IReadOnlyList<(int sideIndex, int itemIndex)>>? OnSlowApplied { get; init; }
    /// <summary>摧毁施放者右侧下一件物品时由模拟器注入；回调内先 InvokeTrigger(Destroy)，再标记 Destroyed。</summary>
    public Action<int>? OnDestroyApplied { get; init; }

    public bool HasLifeSteal => Side.GetItemInt(ItemIndex, nameof(ItemTemplate.LifeSteal), 0) != 0;
    public bool IsCasterInFlight => Item.InFlight;

    public int GetResolvedValue(string key, bool applyCritMultiplier = false, int defaultValue = 0)
    {
        var auraContext = new BattleAuraContext(Side, ItemIndex, Opp);
        int baseValue = Item.Template.GetInt(key, Item.Tier, defaultValue, auraContext);
        return applyCritMultiplier ? baseValue * CritMultiplier : baseValue;
    }

    public Condition? TargetCondition { get; init; }

    public int ApplyDamageToOpp(int value, bool isBurn) => BattleSideDamage.ApplyDamageToSide(Opp, value, isBurn);
    public void HealCaster(int amount) { Side.Hp = Math.Min(Side.MaxHp, Side.Hp + amount); }
    public void AddBurnToOpp(int value) => Opp.Burn += value;
    public void AddPoisonToOpp(int value) => Opp.Poison += value;
    public void AddShieldToCaster(int value) => Side.Shield += value;

    public int HealCasterWithDebuffClear(int requestedHeal)
    {
        int heal = Math.Min(requestedHeal, Side.MaxHp - Side.Hp);
        Side.Hp += heal;
        int clear = RatioUtil.PercentFloor(heal, 5);
        Side.Burn = Math.Max(0, Side.Burn - clear);
        Side.Poison = Math.Max(0, Side.Poison - clear);
        return heal;
    }

    public void AddRegenToCaster(int value) => Side.Regen += value;

    public void ChargeCasterItem(int chargeMs, out bool fullAndShouldCast)
    {
        fullAndShouldCast = false;
        int cooldownMs = Side.GetItemInt(ItemIndex, "CooldownMs", 0);
        if (cooldownMs <= 0) return;
        int newElapsed = Math.Min(cooldownMs, Item.CooldownElapsedMs + chargeMs);
        int added = newElapsed - Item.CooldownElapsedMs;
        Item.CooldownElapsedMs = newElapsed;
        if (added > 0)
            LogSink.OnEffect(SideIndex, ItemIndex, Item.Template.Name, "充能", added, TimeMs, isCrit: false);
        if (Item.CooldownElapsedMs >= cooldownMs && ChargeInducedCastQueue != null)
        {
            int ammoCap = Side.GetItemInt(ItemIndex, "AmmoCap", 0);
            if (ammoCap <= 0 || Item.AmmoRemaining > 0)
                ChargeInducedCastQueue.Add((SideIndex, ItemIndex));
            fullAndShouldCast = true;
            Item.CooldownElapsedMs = 0;
        }
    }

    /// <summary>从 fromSide 中选取至多 targetCount 个目标：仅考虑未销毁、有冷却时间的物品，且满足 condition（Source=施放者）；不放回随机选取。</summary>
    private List<int> GetTargetIndices(BattleSide fromSide, int fromSideIndex, int targetCount, Condition condition)
    {
        var pool = new List<int>();
        BattleSide enemySide = fromSide == Side ? Opp : Side;
        for (int i = 0; i < fromSide.Items.Count; i++)
        {
            var it = fromSide.Items[i];
            if (it.Destroyed || fromSide.GetItemInt(i, "CooldownMs", 0) <= 0) continue;
            var ctx = new ConditionContext
            {
                MySide = fromSide,
                EnemySide = enemySide,
                Item = it,
                Source = Item,
            };
            if (!condition.Evaluate(ctx)) continue;
            pool.Add(i);
        }
        int take = Math.Min(targetCount, pool.Count);
        if (take <= 0) return [];
        for (int n = 0; n < take; n++)
        {
            int j = Random.Shared.Next(n, pool.Count);
            (pool[n], pool[j]) = (pool[j], pool[n]);
        }
        return pool.Take(take).ToList();
    }

    /// <summary>从 fromSide 中选取至多 targetCount 个已摧毁且满足 condition 的物品索引；不放回随机选取。</summary>
    private List<int> GetRepairTargetIndices(BattleSide fromSide, int fromSideIndex, int targetCount, Condition condition)
    {
        var pool = new List<int>();
        BattleSide enemySide = fromSide == Side ? Opp : Side;
        for (int i = 0; i < fromSide.Items.Count; i++)
        {
            var it = fromSide.Items[i];
            if (!it.Destroyed) continue;
            var ctx = new ConditionContext
            {
                MySide = fromSide,
                EnemySide = enemySide,
                Item = it,
                Source = Item,
            };
            if (!condition.Evaluate(ctx)) continue;
            pool.Add(i);
        }
        int take = Math.Min(targetCount, pool.Count);
        if (take <= 0) return [];
        for (int n = 0; n < take; n++)
        {
            int j = Random.Shared.Next(n, pool.Count);
            (pool[n], pool[j]) = (pool[j], pool[n]);
        }
        return pool.Take(take).ToList();
    }

    public void ApplyFreeze(int freezeMs, int targetCount, Condition? targetCondition = null)
    {
        if (freezeMs <= 0 || targetCount <= 0) return;
        int oppSideIndex = 1 - SideIndex;
        var indices = GetTargetIndices(Opp, oppSideIndex, targetCount, targetCondition ?? Condition.DifferentSide);
        if (indices.Count == 0) return;
        var targetNames = new List<string>();
        foreach (int i in indices)
        {
            var target = Opp.Items[i];
            target.FreezeRemainingMs = Math.Max(target.FreezeRemainingMs, freezeMs);
            targetNames.Add(target.Template.Name);
        }
        string extraSuffix = string.Concat(targetNames.Select(name => " →[" + name + "]"));
        LogSink.OnEffect(SideIndex, ItemIndex, Item.Template.Name, "冻结", freezeMs, TimeMs, isCrit: false, extraSuffix);
        var freezeTargets = indices.Select(i => (oppSideIndex, i)).ToList();
        OnFreezeApplied?.Invoke(freezeTargets);
    }

    public void ApplySlow(int slowMs, int targetCount, Condition? targetCondition = null)
    {
        if (slowMs <= 0 || targetCount <= 0) return;
        int oppSideIndex = 1 - SideIndex;
        var indices = GetTargetIndices(Opp, oppSideIndex, targetCount, targetCondition ?? Condition.DifferentSide);
        if (indices.Count == 0) return;
        var targetNames = new List<string>();
        foreach (int i in indices)
        {
            var target = Opp.Items[i];
            target.SlowRemainingMs = Math.Max(target.SlowRemainingMs, slowMs);
            targetNames.Add(target.Template.Name);
        }
        string extraSuffix = string.Concat(targetNames.Select(name => " →[" + name + "]"));
        LogSink.OnEffect(SideIndex, ItemIndex, Item.Template.Name, "减速", slowMs, TimeMs, isCrit: false, extraSuffix);
        var slowTargets = indices.Select(i => (oppSideIndex, i)).ToList();
        OnSlowApplied?.Invoke(slowTargets);
    }

    public void ApplyCharge(int chargeMs, int targetCount, Condition? targetCondition = null)
    {
        if (chargeMs <= 0 || targetCount <= 0) return;
        var indices = GetTargetIndices(Side, SideIndex, targetCount, targetCondition ?? Condition.SameSide);
        if (indices.Count == 0) return;
        var targetNames = new List<string>();
        foreach (int i in indices)
            targetNames.Add(ChargeItemAt(SideIndex, i, chargeMs));
        string extraSuffix = string.Concat(targetNames.Select(name => " →[" + name + "]"));
        LogSink.OnEffect(SideIndex, ItemIndex, Item.Template.Name, "充能", chargeMs, TimeMs, isCrit: false, extraSuffix);
    }

    /// <summary>为指定侧指定下标的物品充能 chargeMs；若满则加入施放队列。返回该物品名称。</summary>
    private string ChargeItemAt(int sideIndex, int itemIndex, int chargeMs)
    {
        var target = Side.Items[itemIndex];
        int cooldownMs = Side.GetItemInt(itemIndex, "CooldownMs", 0);
        if (cooldownMs <= 0) return target.Template.Name;
        int newElapsed = Math.Min(cooldownMs, target.CooldownElapsedMs + chargeMs);
        target.CooldownElapsedMs = newElapsed;
        if (target.CooldownElapsedMs >= cooldownMs && ChargeInducedCastQueue != null)
        {
            int ammoCap = Side.GetItemInt(itemIndex, "AmmoCap", 0);
            if (ammoCap <= 0 || target.AmmoRemaining > 0)
                ChargeInducedCastQueue.Add((sideIndex, itemIndex));
            target.CooldownElapsedMs = 0;
        }
        return target.Template.Name;
    }

    public void ApplyHaste(int hasteMs, int targetCount, Condition? targetCondition = null)
    {
        if (hasteMs <= 0 || targetCount <= 0) return;
        var indices = GetTargetIndices(Side, SideIndex, targetCount, targetCondition ?? Condition.SameSide);
        if (indices.Count == 0) return;
        var targetNames = new List<string>();
        foreach (int i in indices)
        {
            var target = Side.Items[i];
            target.HasteRemainingMs = Math.Max(target.HasteRemainingMs, hasteMs);
            targetNames.Add(target.Template.Name);
        }
        string extraSuffix = string.Concat(targetNames.Select(name => " →[" + name + "]"));
        LogSink.OnEffect(SideIndex, ItemIndex, Item.Template.Name, "加速", hasteMs, TimeMs, isCrit: false, extraSuffix);
    }

    public void ApplyRepair(int targetCount, Condition? targetCondition = null)
    {
        if (targetCount <= 0) return;
        var indices = GetRepairTargetIndices(Side, SideIndex, targetCount, targetCondition ?? Condition.SameSide);
        if (indices.Count == 0) return;
        var targetNames = new List<string>();
        foreach (int i in indices)
        {
            var target = Side.Items[i];
            target.Destroyed = false;
            target.CooldownElapsedMs = 0;
            targetNames.Add(target.Template.Name);
        }
        string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
        LogEffect("修复", indices.Count, extraSuffix, showCrit: false);
    }

    public void AddAttributeToCasterSide(string attributeName, int value, Condition? targetCondition)
    {
        if (value <= 0 || targetCondition == null) return;
        var targetNames = new List<string>();
        for (int i = 0; i < Side.Items.Count; i++)
        {
            var wi = Side.Items[i];
            if (wi.Destroyed) continue;
            var ctx = new ConditionContext
            {
                MySide = Side,
                EnemySide = Opp,
                Item = wi,
                Source = Item,
            };
            if (!targetCondition.Evaluate(ctx)) continue;
            if (attributeName == nameof(ItemTemplate.Damage))
            {
                wi.Template.Damage = wi.Template.Damage.Add(value);
                targetNames.Add(wi.Template.Name);
            }
            else if (attributeName == nameof(ItemTemplate.Poison))
            {
                wi.Template.Poison = wi.Template.Poison.Add(value);
                targetNames.Add(wi.Template.Name);
            }
        }
        if (targetNames.Count > 0)
        {
            string logName = attributeName == nameof(ItemTemplate.Damage) ? "伤害提高" : attributeName == nameof(ItemTemplate.Poison) ? "剧毒提高" : "属性提高";
            string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
            LogEffect(logName, value, extraSuffix, showCrit: false);
        }
    }

    public void ReduceAttributeToOpponentSide(string attributeName, int value, Condition? targetCondition)
    {
        if (value <= 0 || targetCondition == null) return;
        var targetNames = new List<string>();
        for (int i = 0; i < Opp.Items.Count; i++)
        {
            var wi = Opp.Items[i];
            if (wi.Destroyed) continue;
            var ctx = new ConditionContext
            {
                MySide = Opp,
                EnemySide = Side,
                Item = wi,
                Source = Item,
            };
            if (!targetCondition.Evaluate(ctx)) continue;
            if (attributeName == nameof(ItemTemplate.Shield))
            {
                int current = wi.Template.GetInt(nameof(ItemTemplate.Shield), wi.Tier, 0);
                int newVal = Math.Max(0, current - value);
                wi.Template.SetInt(nameof(ItemTemplate.Shield), newVal);
                targetNames.Add(wi.Template.Name);
            }
        }
        if (targetNames.Count > 0)
        {
            string logName = attributeName == nameof(ItemTemplate.Shield) ? "护盾降低" : "属性降低";
            string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
            LogEffect(logName, value, extraSuffix, showCrit: false);
        }
    }

    public void LogEffect(string effectName, int value, string? extraSuffix = null, bool showCrit = false) =>
        LogSink.OnEffect(SideIndex, ItemIndex, Item.Template.Name, effectName, value, TimeMs, showCrit, extraSuffix);

    public void SetCasterInFlight(bool inFlight) => Side.Items[ItemIndex].InFlight = inFlight;

    public void DestroyNextItemToRightOfCaster()
    {
        for (int i = ItemIndex + 1; i < Side.Items.Count; i++)
        {
            if (Side.Items[i].Destroyed) continue;
            var target = Side.Items[i];
            LogEffect("摧毁", 0, " →[" + target.Template.Name + "]", showCrit: false);
            if (OnDestroyApplied != null)
                OnDestroyApplied(i);
            else
                Side.Items[i].Destroyed = true;
            return;
        }
    }
}
