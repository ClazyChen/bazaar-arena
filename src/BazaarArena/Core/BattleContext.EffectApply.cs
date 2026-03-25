using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

public sealed partial class BattleContext
{
    public int ApplyDamageToOpp(int value, bool isBurn) => BattleSideDamage.ApplyDamageToSide(OppSide, value, isBurn);
    public void HealCaster(int amount) { CurrentSide.Hp = Math.Min(CurrentSide.MaxHp, CurrentSide.Hp + amount); }
    public void AddBurnToOpp(int value) => OppSide.Burn += value;
    public void AddPoisonToOpp(int value) => OppSide.Poison += value;
    public void AddPoisonToCaster(int value) => CurrentSide.Poison += value;
    public void AddShieldToCaster(int value) => CurrentSide.Shield += value;

    public int HealCasterWithDebuffClear(int requestedHeal)
    {
        int room = Math.Max(0, CurrentSide.MaxHp - CurrentSide.Hp);
        int heal = Math.Min(requestedHeal, room);
        CurrentSide.Hp += heal;
        int clear = RatioUtil.PercentFloor(requestedHeal, 5);
        CurrentSide.Burn = Math.Max(0, CurrentSide.Burn - clear);
        CurrentSide.Poison = Math.Max(0, CurrentSide.Poison - clear);
        return heal;
    }

    [Obsolete("兼容保留方法，优先通过 Ability/Apply 路径表达效果。")]
    public void AddRegenToCaster(int value) => CurrentSide.Regen += value;
    public void AddGoldToCaster(int value) => CurrentSide.Gold += value;

    [Obsolete("兼容保留方法，优先使用 ApplyCharge 统一处理目标筛选与日志。")]
    public void ChargeCasterItem(int chargeMs, out bool fullAndShouldCast)
    {
        fullAndShouldCast = false;
        int cooldownMs = BattleState.GetItemInt(Caster, Key.CooldownMs);
        if (cooldownMs <= 0) return;
        int newElapsed = Math.Min(cooldownMs, Caster.ChargedTimeMs + chargeMs);
        int added = newElapsed - Caster.ChargedTimeMs;
        Caster.ChargedTimeMs = newElapsed;
        if (added > 0)
            BattleState.LogSink.OnEffect(Caster, Caster.Template.Name, "充能", added, BattleState.TimeMs, isCrit: false);
        if (Caster.ChargedTimeMs >= cooldownMs)
        {
            int ammoCap = BattleState.GetItemInt(Caster, Key.AmmoCap);
            if (AllowCastQueueEnqueue && (ammoCap <= 0 || Caster.AmmoRemaining > 0))
                BattleState.CastQueue.Add(Caster);
            fullAndShouldCast = true;
            Caster.ChargedTimeMs = 0;
        }
    }

    private BattleContext BuildContext(ItemState item) => new()
    {
        BattleState = BattleState,
        Item = item,
        Caster = Caster,
        Source = item,
        InvokeTarget = InvokeTarget,
    };

    private List<int> GetTargetIndices(BattleSide fromSide, int targetCount, Formula? condition)
    {
        if (condition == null) return [];
        var pool = new List<int>();
        for (int i = 0; i < fromSide.Items.Count; i++)
        {
            var it = fromSide.Items[i];
            if (condition.Evaluate(BuildContext(it)) == 0) continue;
            pool.Add(i);
        }
        int take = Math.Min(targetCount, pool.Count);
        if (take <= 0) return [];
        for (int n = 0; n < take; n++)
        {
            int j = ThreadLocalRandom.Next(n, pool.Count);
            (pool[n], pool[j]) = (pool[j], pool[n]);
        }
        return pool.Take(take).ToList();
    }

    private List<(BattleSide side, int index)> GetTargetsFromBothSides(int targetCount, Formula? condition)
    {
        if (condition == null) return [];
        var pool = new List<(BattleSide side, int index)>();
        foreach (var fromSide in new[] { CurrentSide, OppSide })
        {
            for (int i = 0; i < fromSide.Items.Count; i++)
            {
                var it = fromSide.Items[i];
                if (condition.Evaluate(BuildContext(it)) == 0) continue;
                pool.Add((fromSide, i));
            }
        }
        int take = Math.Min(targetCount, pool.Count);
        if (take <= 0) return [];
        for (int n = 0; n < take; n++)
        {
            int j = ThreadLocalRandom.Next(n, pool.Count);
            (pool[n], pool[j]) = (pool[j], pool[n]);
        }
        return pool.Take(take).ToList();
    }

    private void ApplyToTargets(BattleSide fromSide, int targetCount, Formula? condition, string effectName, int? logValue, Func<ItemState, int, string> perTarget, int? effectTriggerName = null)
    {
        var indices = GetTargetIndices(fromSide, targetCount, condition);
        if (indices.Count == 0) return;
        var targetNames = new List<string>();
        foreach (int i in indices)
        {
            var target = fromSide.Items[i];
            targetNames.Add(perTarget(target, i));
        }
        if (!string.IsNullOrEmpty(effectName))
        {
            string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
            BattleState.LogSink.OnEffect(Caster, Caster.Template.Name, effectName, logValue ?? indices.Count, BattleState.TimeMs, isCrit: false, extraSuffix);
        }
        if (effectTriggerName != null)
        {
            var triggerTargets = new List<ItemState>(indices.Count);
            foreach (int i in indices)
                triggerTargets.Add(fromSide.Items[i]);
            BattleState.InvokeTriggerMany(effectTriggerName.Value, Caster, triggerTargets, triggerTargets.Count);
        }
    }

    private void ApplyToTargetsBothSides(int targetCount, Formula? condition, string effectName, int? logValue, Func<BattleSide, int, string> perTarget, int? effectTriggerName = null)
    {
        var targets = GetTargetsFromBothSides(targetCount, condition);
        if (targets.Count == 0) return;
        var targetNames = new List<string>();
        foreach (var (side, index) in targets)
            targetNames.Add(perTarget(side, index));
        if (!string.IsNullOrEmpty(effectName))
        {
            string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
            BattleState.LogSink.OnEffect(Caster, Caster.Template.Name, effectName, logValue ?? targets.Count, BattleState.TimeMs, isCrit: false, extraSuffix);
        }
        if (effectTriggerName != null)
        {
            var triggerTargets = new List<ItemState>(targets.Count);
            foreach (var (side, index) in targets)
                triggerTargets.Add(side.Items[index]);
            BattleState.InvokeTriggerMany(effectTriggerName.Value, Caster, triggerTargets, triggerTargets.Count);
        }
    }

    private string ChargeItemAt(BattleSide side, int itemIndex, int chargeMs)
    {
        var target = side.Items[itemIndex];
        int cooldownMs = BattleState.GetItemInt(target, Key.CooldownMs);
        if (cooldownMs <= 0) return target.Template.Name;
        int newElapsed = Math.Min(cooldownMs, target.ChargedTimeMs + chargeMs);
        target.ChargedTimeMs = newElapsed;
        if (target.ChargedTimeMs >= cooldownMs && side == CurrentSide)
        {
            int ammoCap = BattleState.GetItemInt(target, Key.AmmoCap);
            if (AllowCastQueueEnqueue && (ammoCap <= 0 || target.AmmoRemaining > 0))
                BattleState.CastQueue.Add(target);
            target.ChargedTimeMs = 0;
        }
        return target.Template.Name;
    }

    public void ApplyFreeze(int freezeMs, int targetCount, Formula? targetCondition = null)
    {
        if (freezeMs <= 0 || targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.DifferentSide) & ~Condition.Destroyed;
        ApplyToTargetsBothSides(targetCount, cond, "冻结", freezeMs, (side, index) =>
        {
            var t = side.Items[index];
            int pct = Math.Clamp(GetItemInt(t, Key.PercentFreezeReduction), 0, 100);
            int effectiveMs = freezeMs - RatioUtil.PercentFloor(freezeMs, pct);
            t.FreezeRemainingMs += effectiveMs;
            return t.Template.Name;
        }, Trigger.Freeze);
    }

    public void ApplySlow(int slowMs, int targetCount, Formula? targetCondition = null)
    {
        if (slowMs <= 0 || targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.DifferentSide) & ~Condition.Destroyed & Condition.HasCooldown;
        ApplyToTargetsBothSides(targetCount, cond, "减速", slowMs, (side, index) =>
        {
            var t = side.Items[index];
            int pct = Math.Clamp(GetItemInt(t, Key.PercentSlowReduction), 0, 100);
            int effectiveMs = slowMs - RatioUtil.PercentFloor(slowMs, pct);
            t.SlowRemainingMs += effectiveMs;
            return t.Template.Name;
        }, Trigger.Slow);
    }

    public void ApplyCharge(int chargeMs, int targetCount, Formula? targetCondition = null, string? effectLogName = null)
    {
        if (chargeMs <= 0 || targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.SameSide) & ~Condition.Destroyed & Condition.HasCooldown;
        ApplyToTargetsBothSides(targetCount, cond, effectLogName ?? "充能", chargeMs, (side, index) => ChargeItemAt(side, index, chargeMs), null);
    }

    public void ApplyHaste(int hasteMs, int targetCount, Formula? targetCondition = null, string? effectLogName = null)
    {
        if (hasteMs <= 0 || targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.SameSide) & ~Condition.Destroyed & Condition.HasCooldown;
        ApplyToTargetsBothSides(targetCount, cond, effectLogName ?? "加速", hasteMs, (side, index) =>
        {
            var t = side.Items[index];
            t.HasteRemainingMs += hasteMs;
            return t.Template.Name;
        }, Trigger.Haste);
    }

    public void ApplyReload(int amount, int targetCount, Formula? targetCondition = null, string? effectLogName = null)
    {
        if (amount <= 0 || targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.SameSide) & ~Condition.Destroyed & Condition.WithDerivedTag(DerivedTag.Ammo);
        ApplyToTargetsBothSides(targetCount, cond, effectLogName ?? "装填", amount, (side, index) =>
        {
            var t = side.Items[index];
            int cap = BattleState.GetItemInt(t, Key.AmmoCap);
            int add = Math.Min(amount, Math.Max(0, cap - t.AmmoRemaining));
            t.AmmoRemaining += add;
            if (side == CurrentSide)
            {
                int cooldownMs = BattleState.GetItemInt(t, Key.CooldownMs);
                if (cooldownMs > 0 && t.ChargedTimeMs >= cooldownMs && (cap <= 0 || t.AmmoRemaining > 0))
                {
                    if (AllowCastQueueEnqueue)
                        BattleState.CastQueue.Add(t);
                    t.ChargedTimeMs = 0;
                }
            }
            if (add > 0)
                BattleState.InvokeTrigger(Trigger.Reload, Caster, t, 1);
            return t.Template.Name;
        }, null);
    }

    public void ApplyRepair(int targetCount, Formula? targetCondition = null)
    {
        if (targetCount <= 0) return;
        var condition = (targetCondition ?? Condition.SameSide) & Condition.Destroyed;
        ApplyToTargets(CurrentSide, targetCount, condition, "修复", null, (t, _) => { t.Destroyed = false; t.ChargedTimeMs = 0; return t.Template.Name; }, null);
    }

    private void ApplyToSideWithCondition(BattleSide fromSide, Formula? targetCondition, string logEffectName, int logValue, Func<ItemState, int, string?> perItem)
    {
        if (targetCondition == null) return;
        var targetNames = new List<string>();
        for (int i = 0; i < fromSide.Items.Count; i++)
        {
            var wi = fromSide.Items[i];
            if (wi.Destroyed) continue;
            if (targetCondition.Evaluate(BuildContext(wi)) == 0) continue;
            var name = perItem(wi, i);
            if (name != null) targetNames.Add(name);
        }
        if (targetNames.Count > 0 && !string.IsNullOrEmpty(logEffectName))
        {
            string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
            LogEffect(logEffectName, logValue, extraSuffix, showCrit: false);
        }
    }

    public void AddAttributeToCasterSide(int attributeKey, int value, Formula? targetCondition, int maxTargetCount = 0, string? effectLogName = null)
    {
        if (value <= 0 || targetCondition == null) return;
        var cond = (targetCondition ?? Condition.SameSide) & ~Condition.Destroyed;
        if (attributeKey == Key.CritRate)
            cond &= Condition.CanCrit;
        string logName = effectLogName ?? (AttributeLogNames.Get(attributeKey) + "提高");
        if (maxTargetCount > 0)
        {
            var indices = GetTargetIndices(CurrentSide, maxTargetCount, cond);
            if (indices.Count == 0) return;
            var targetNames = new List<string>();
            List<ItemState>? triggerTargets = null;
            foreach (int i in indices)
            {
                var wi = CurrentSide.Items[i];
                if (attributeKey == Key.Damage) { wi.SetAttribute(Key.Damage, wi.GetAttribute(Key.Damage) + value); targetNames.Add(wi.Template.Name); }
                else if (attributeKey == Key.Poison) { wi.SetAttribute(Key.Poison, wi.GetAttribute(Key.Poison) + value); targetNames.Add(wi.Template.Name); }
                else if (attributeKey == Key.CritRate) { wi.SetAttribute(Key.CritRate, wi.GetAttribute(Key.CritRate) + value); targetNames.Add(wi.Template.Name); (triggerTargets ??= new List<ItemState>(indices.Count)).Add(wi); }
                else if (attributeKey == Key.InFlight) { wi.InFlight = value != 0; targetNames.Add(wi.Template.Name); }
                else { wi.SetAttribute(attributeKey, wi.GetAttribute(attributeKey) + value); targetNames.Add(wi.Template.Name); }
            }
            if (targetNames.Count > 0 && !string.IsNullOrEmpty(logName))
                BattleState.LogSink.OnEffect(Caster, Caster.Template.Name, logName, value, BattleState.TimeMs, isCrit: false, " →[" + string.Join("、", targetNames) + "]");
            if (attributeKey == Key.CritRate && triggerTargets != null && triggerTargets.Count > 0)
                BattleState.InvokeTriggerMany(Trigger.CritRateIncreased, Caster, triggerTargets, triggerTargets.Count);
            return;
        }
        ApplyToSideWithCondition(CurrentSide, cond, logName, value, (wi, _) =>
        {
            if (attributeKey == Key.Damage) { wi.SetAttribute(Key.Damage, wi.GetAttribute(Key.Damage) + value); return wi.Template.Name; }
            if (attributeKey == Key.Poison) { wi.SetAttribute(Key.Poison, wi.GetAttribute(Key.Poison) + value); return wi.Template.Name; }
            if (attributeKey == Key.CritRate) { wi.SetAttribute(Key.CritRate, wi.GetAttribute(Key.CritRate) + value); BattleState.InvokeTrigger(Trigger.CritRateIncreased, Caster, wi, 1); return wi.Template.Name; }
            if (attributeKey == Key.InFlight) { wi.InFlight = value != 0; return wi.Template.Name; }
            wi.SetAttribute(attributeKey, wi.GetAttribute(attributeKey) + value);
            return wi.Template.Name;
        });
    }

    public void SetAttributeOnCasterSide(int attributeKey, int value, Formula? targetCondition, string? effectLogName = null)
    {
        if (targetCondition == null) return;
        string logName = effectLogName ?? (attributeKey == Key.InFlight && value == 0 ? "结束飞行" : AttributeLogNames.Get(attributeKey) + "变更");
        ApplyToSideWithCondition(CurrentSide, targetCondition, logName, 0, (wi, _) =>
        {
            if (attributeKey == Key.InFlight) { wi.InFlight = value != 0; return wi.Template.Name; }
            wi.SetAttribute(attributeKey, value);
            return wi.Template.Name;
        });
    }

    public void ReduceAttributeToSide(int attributeKey, int value, Formula? targetCondition, int maxTargetCount = 0, string? effectLogName = null)
    {
        if (value <= 0 || targetCondition == null) return;
        var cond = (targetCondition ?? Condition.DifferentSide) & ~Condition.Destroyed;
        if (attributeKey == Key.CritRate)
            cond &= Condition.CanCrit;
        string logName = effectLogName ?? (AttributeLogNames.Get(attributeKey) + "降低");
        int take = maxTargetCount > 0 ? maxTargetCount : 100;
        var targets = GetTargetsFromBothSides(take, cond);
        if (targets.Count == 0) return;
        var targetNames = new List<string>();
        const int minCooldownMs = 1000;
        foreach (var (side, index) in targets)
        {
            var wi = side.Items[index];
            int current = wi.GetAttribute(attributeKey);
            int newVal = current - value;
            if (attributeKey == Key.CooldownMs)
                newVal = Math.Max(minCooldownMs, newVal);
            else
                newVal = Math.Max(0, newVal);
            wi.SetAttribute(attributeKey, newVal);
            targetNames.Add(wi.Template.Name);

            if (attributeKey == Key.CooldownMs && side == CurrentSide && wi.ChargedTimeMs >= newVal)
            {
                int ammoCap = BattleState.GetItemInt(wi, Key.AmmoCap);
                if (AllowCastQueueEnqueue && (ammoCap <= 0 || wi.AmmoRemaining > 0))
                    BattleState.CastQueue.Add(wi);
                wi.ChargedTimeMs = 0;
            }
        }
        if (targetNames.Count > 0 && !string.IsNullOrEmpty(logName))
            BattleState.LogSink.OnEffect(Caster, Caster.Template.Name, logName, value, BattleState.TimeMs, isCrit: false, " →[" + string.Join("、", targetNames) + "]");
    }

    public void ReportTriggerCause(int triggerName) =>
        BattleState.InvokeTrigger(triggerName, Caster, null, 1);

    public void LogEffect(string effectName, int value, string? extraSuffix = null, bool showCrit = false) =>
        BattleState.LogSink.OnEffect(Caster, Caster.Template.Name, effectName, value, BattleState.TimeMs, showCrit, extraSuffix);

    [Obsolete("兼容保留方法，优先使用 SetAttributeOnCasterSide(Key.InFlight, ...) 统一路径。")]
    public void SetCasterInFlight(bool inFlight) => Caster.InFlight = inFlight;

    public void ApplyDestroy(int targetCount, Formula? targetCondition = null)
    {
        if (targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.SameSide) & ~Condition.Destroyed;
        var sideIndices = GetTargetIndices(CurrentSide, targetCount, cond);
        BattleSide targetSide;
        List<int> indices;
        if (sideIndices.Count > 0)
        {
            targetSide = CurrentSide;
            indices = sideIndices;
        }
        else
        {
            indices = GetTargetIndices(OppSide, targetCount, cond);
            targetSide = OppSide;
        }
        if (indices.Count == 0) return;
        var targetNames = indices.Select(i => targetSide.Items[i].Template.Name).ToList();
        string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
        LogEffect("摧毁", indices.Count, extraSuffix, showCrit: false);
        var triggerTargets = new List<ItemState>(indices.Count);
        foreach (int i in indices)
            triggerTargets.Add(targetSide.Items[i]);
        BattleState.InvokeTriggerMany(Trigger.Destroy, Caster, triggerTargets, triggerTargets.Count);
        foreach (int i in indices)
        {
            var target = targetSide.Items[i];
            target.Destroyed = true;
        }
    }
}
