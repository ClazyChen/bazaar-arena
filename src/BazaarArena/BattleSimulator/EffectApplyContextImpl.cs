using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>效果应用上下文：实现 IEffectApplyContext，供 AbilityDefinition.Apply 委托使用。</summary>
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
    public List<BattleItemState>? ChargeInducedCastQueue { get; init; }
    /// <summary>效果施加触发的触发器待处理队列（由模拟器传入）。冻结/减速/摧毁时追加 (TriggerName, SideIndex, ItemIndex)，模拟器在 Apply 后统一 InvokeTrigger 并处理 Destroyed。</summary>
    public List<(string TriggerName, int SideIndex, int ItemIndex)>? EffectAppliedTriggerQueue { get; init; }

    public int GetResolvedValue(string key, bool applyCritMultiplier = false, int defaultValue = 0)
    {
        var auraContext = new BattleAuraContext(Side, Item, Opp);
        int baseValue = Item.Template.GetInt(key, Item.Tier, defaultValue, auraContext);
        return applyCritMultiplier ? baseValue * CritMultiplier : baseValue;
    }

    public Condition? TargetCondition { get; init; }

    public int ApplyDamageToOpp(int value, bool isBurn) => BattleSideDamage.ApplyDamageToSide(Opp, value, isBurn);
    public void HealCaster(int amount) { Side.Hp = Math.Min(Side.MaxHp, Side.Hp + amount); }
    public void AddBurnToOpp(int value) => Opp.Burn += value;
    public void AddPoisonToOpp(int value) => Opp.Poison += value;
    public void AddShieldToCaster(int value) => Side.Shield += value;

    /// <summary>实际加血 = min(请求量, 当前可接受量)；清除灼烧/剧毒按请求治疗量的 5%。</summary>
    public int HealCasterWithDebuffClear(int requestedHeal)
    {
        int room = Math.Max(0, Side.MaxHp - Side.Hp);
        int heal = Math.Min(requestedHeal, room);
        Side.Hp += heal;
        int clear = RatioUtil.PercentFloor(requestedHeal, 5);
        Side.Burn = Math.Max(0, Side.Burn - clear);
        Side.Poison = Math.Max(0, Side.Poison - clear);
        return heal;
    }

    public void AddRegenToCaster(int value) => Side.Regen += value;

    public void ChargeCasterItem(int chargeMs, out bool fullAndShouldCast)
    {
        fullAndShouldCast = false;
        int cooldownMs = Side.GetItemInt(Item.ItemIndex, "CooldownMs", 0);
        if (cooldownMs <= 0) return;
        int newElapsed = Math.Min(cooldownMs, Item.CooldownElapsedMs + chargeMs);
        int added = newElapsed - Item.CooldownElapsedMs;
        Item.CooldownElapsedMs = newElapsed;
        if (added > 0)
            LogSink.OnEffect(Item, Item.Template.Name, "充能", added, TimeMs, isCrit: false);
        if (Item.CooldownElapsedMs >= cooldownMs && ChargeInducedCastQueue != null)
        {
            int ammoCap = Side.GetItemInt(Item.ItemIndex, "AmmoCap", 0);
            if (ammoCap <= 0 || Item.AmmoRemaining > 0)
                ChargeInducedCastQueue.Add(Item);
            fullAndShouldCast = true;
            Item.CooldownElapsedMs = 0;
        }
    }

    /// <summary>从 fromSide 中选取至多 targetCount 个满足 condition 的目标（Source=施放者）；不放回随机选取。</summary>
    private List<int> GetTargetIndices(BattleSide fromSide, int targetCount, Condition? condition)
    {
        if (condition == null) return [];
        var pool = new List<int>();
        BattleSide enemySide = fromSide == Side ? Opp : Side;
        for (int i = 0; i < fromSide.Items.Count; i++)
        {
            var it = fromSide.Items[i];
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

    /// <summary>通用「按条件选目标→逐目标应用→记日志」；logValue 为 null 时记目标数。effectTriggerName 非空时将本次目标写入 EffectAppliedTriggerQueue 供模拟器统一触发。</summary>
    private void ApplyToTargets(BattleSide fromSide, int targetCount, Condition? condition, string effectName, int? logValue, Func<BattleItemState, int, string> perTarget, string? effectTriggerName = null)
    {
        var indices = GetTargetIndices(fromSide, targetCount, condition);
        if (indices.Count == 0) return;
        var targetNames = new List<string>();
        foreach (int i in indices)
        {
            var target = fromSide.Items[i];
            targetNames.Add(perTarget(target, i));
        }
        string extraSuffix = string.Concat(targetNames.Select(name => " →[" + name + "]"));
        LogSink.OnEffect(Item, Item.Template.Name, effectName, logValue ?? indices.Count, TimeMs, isCrit: false, extraSuffix);
        if (effectTriggerName != null && EffectAppliedTriggerQueue != null)
        {
            foreach (int i in indices)
                EffectAppliedTriggerQueue.Add((effectTriggerName, fromSide.SideIndex, i));
        }
    }

    /// <summary>为指定下标的物品充能 chargeMs；若满则加入施放队列。返回该物品名称。</summary>
    private string ChargeItemAt(int itemIndex, int chargeMs)
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
                ChargeInducedCastQueue.Add(target);
            target.CooldownElapsedMs = 0;
        }
        return target.Template.Name;
    }

    public void ApplyFreeze(int freezeMs, int targetCount, Condition? targetCondition = null)
    {
        if (freezeMs <= 0 || targetCount <= 0) return;
        ApplyToTargets(Opp, targetCount, targetCondition, "冻结", freezeMs, (t, _) => { t.FreezeRemainingMs += freezeMs; return t.Template.Name; }, Trigger.Freeze);
    }

    public void ApplySlow(int slowMs, int targetCount, Condition? targetCondition = null)
    {
        if (slowMs <= 0 || targetCount <= 0) return;
        ApplyToTargets(Opp, targetCount, targetCondition, "减速", slowMs, (t, _) => { t.SlowRemainingMs += slowMs; return t.Template.Name; }, Trigger.Slow);
    }

    public void ApplyCharge(int chargeMs, int targetCount, Condition? targetCondition = null)
    {
        if (chargeMs <= 0 || targetCount <= 0) return;
        ApplyToTargets(Side, targetCount, targetCondition, "充能", chargeMs, (_, i) => ChargeItemAt(i, chargeMs), null);
    }

    public void ApplyHaste(int hasteMs, int targetCount, Condition? targetCondition = null)
    {
        if (hasteMs <= 0 || targetCount <= 0) return;
        ApplyToTargets(Side, targetCount, targetCondition, "加速", hasteMs, (t, _) => { t.HasteRemainingMs += hasteMs; return t.Template.Name; }, null);
    }

    public void ApplyRepair(int targetCount, Condition? targetCondition = null)
    {
        if (targetCount <= 0) return;
        var condition = (targetCondition ?? Condition.SameSide) & Condition.Destroyed;
        ApplyToTargets(Side, targetCount, condition, "修复", null, (t, _) => { t.Destroyed = false; t.CooldownElapsedMs = 0; return t.Template.Name; }, null);
    }

    /// <summary>对一侧物品按条件筛选并逐项执行 perItem；收集返回的名称并统一记一条日志。perItem 返回 null 表示未施加。</summary>
    private void ApplyToSideWithCondition(BattleSide fromSide, BattleSide enemySide, Condition? targetCondition, string logEffectName, int logValue, Func<BattleItemState, int, string?> perItem)
    {
        if (targetCondition == null) return;
        var targetNames = new List<string>();
        for (int i = 0; i < fromSide.Items.Count; i++)
        {
            var wi = fromSide.Items[i];
            if (wi.Destroyed) continue;
            var ctx = new ConditionContext
            {
                MySide = fromSide,
                EnemySide = enemySide,
                Item = wi,
                Source = Item,
            };
            if (!targetCondition.Evaluate(ctx)) continue;
            var name = perItem(wi, i);
            if (name != null) targetNames.Add(name);
        }
        if (targetNames.Count > 0)
        {
            string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
            LogEffect(logEffectName, logValue, extraSuffix, showCrit: false);
        }
    }

    public void AddAttributeToCasterSide(string attributeName, int value, Condition? targetCondition)
    {
        if (value <= 0 || targetCondition == null) return;
        string logName = attributeName == Key.Damage ? "伤害提高" : attributeName == Key.Poison ? "剧毒提高" : attributeName == Key.InFlight ? "开始飞行" : "属性提高";
        ApplyToSideWithCondition(Side, Opp, targetCondition, logName, value, (wi, _) =>
        {
            if (attributeName == Key.Damage) { wi.Template.Damage = wi.Template.Damage.Add(value); return wi.Template.Name; }
            if (attributeName == Key.Poison) { wi.Template.Poison = wi.Template.Poison.Add(value); return wi.Template.Name; }
            if (attributeName == Key.InFlight) { wi.InFlight = value != 0; return wi.Template.Name; }
            return null;
        });
    }

    public void SetAttributeOnCasterSide(string attributeName, int value, Condition? targetCondition)
    {
        if (targetCondition == null) return;
        string logName = attributeName == Key.InFlight && value == 0 ? "结束飞行" : "属性变更";
        ApplyToSideWithCondition(Side, Opp, targetCondition, logName, 0, (wi, _) =>
        {
            if (attributeName == Key.InFlight) { wi.InFlight = value != 0; return wi.Template.Name; }
            return null;
        });
    }

    public void ReduceAttributeToOpponentSide(string attributeName, int value, Condition? targetCondition)
    {
        if (value <= 0 || targetCondition == null) return;
        string logName = attributeName == Key.Shield ? "护盾降低" : "属性降低";
        ApplyToSideWithCondition(Opp, Side, targetCondition, logName, value, (wi, _) =>
        {
            if (attributeName == Key.Shield)
            {
                int current = wi.Template.GetInt(Key.Shield, wi.Tier, 0);
                int newVal = Math.Max(0, current - value);
                wi.Template.SetInt(Key.Shield, newVal);
                return wi.Template.Name;
            }
            return null;
        });
    }

    public void LogEffect(string effectName, int value, string? extraSuffix = null, bool showCrit = false) =>
        LogSink.OnEffect(Item, Item.Template.Name, effectName, value, TimeMs, showCrit, extraSuffix);

    public void SetCasterInFlight(bool inFlight) => Item.InFlight = inFlight;

    public void ApplyDestroy(int targetCount, Condition? targetCondition = null)
    {
        if (targetCount <= 0) return;
        var indices = GetTargetIndices(Side, targetCount, targetCondition);
        if (indices.Count == 0) return;
        var targetNames = indices.Select(i => Side.Items[i].Template.Name).ToList();
        string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
        LogEffect("摧毁", indices.Count, extraSuffix, showCrit: false);
        if (EffectAppliedTriggerQueue != null)
        {
            foreach (int i in indices)
                EffectAppliedTriggerQueue.Add((Trigger.Destroy, Side.SideIndex, i));
        }
        else
        {
            foreach (int i in indices)
                Side.Items[i].Destroyed = true;
        }
    }
}
