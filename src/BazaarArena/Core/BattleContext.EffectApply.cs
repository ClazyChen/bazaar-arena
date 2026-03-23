using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

public sealed partial class BattleContext
{
    public int GetResolvedValue(int key, bool applyCritMultiplier = false, int defaultValue = 0)
    {
        int baseValue = Caster.GetAttribute(key);
        return applyCritMultiplier ? baseValue * CritMultiplier : baseValue;
    }

    public int ApplyDamageToOpp(int value, bool isBurn) => BattleSideDamage.ApplyDamageToSide(Opp, value, isBurn);
    public void HealCaster(int amount) { Side.Hp = Math.Min(Side.MaxHp, Side.Hp + amount); }
    public void AddBurnToOpp(int value) => Opp.Burn += value;
    public void AddPoisonToOpp(int value) => Opp.Poison += value;
    public void AddPoisonToCaster(int value) => Side.Poison += value;
    public void AddShieldToCaster(int value) => Side.Shield += value;

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
    public void AddGoldToCaster(int value) => Side.Gold += value;

    public void ChargeCasterItem(int chargeMs, out bool fullAndShouldCast)
    {
        fullAndShouldCast = false;
        int cooldownMs = Side.GetItemInt(Caster.ItemIndex, Key.CooldownMs, 0);
        if (cooldownMs <= 0) return;
        int newElapsed = Math.Min(cooldownMs, Caster.CooldownElapsedMs + chargeMs);
        int added = newElapsed - Caster.CooldownElapsedMs;
        Caster.CooldownElapsedMs = newElapsed;
        if (added > 0)
            LogSink.OnEffect(Caster, Caster.Template.Name, "充能", added, TimeMs, isCrit: false);
        if (Caster.CooldownElapsedMs >= cooldownMs && ChargeInducedCastQueue != null)
        {
            int ammoCap = Side.GetItemInt(Caster.ItemIndex, Key.AmmoCap, 0);
            if (ammoCap <= 0 || Caster.AmmoRemaining > 0)
                ChargeInducedCastQueue.Add(Caster);
            fullAndShouldCast = true;
            Caster.CooldownElapsedMs = 0;
        }
    }

    private BattleContext BuildContext(ItemState item) => new()
    {
        BattleState = BattleState,
        Side = Side,
        Opp = Opp,
        Item = item,
        Caster = Caster,
        Source = Caster,
        InvokeTarget = InvokeTarget,
        TimeMs = TimeMs,
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
        foreach (var fromSide in new[] { Side, Opp })
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
        string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
        LogSink.OnEffect(Caster, Caster.Template.Name, effectName, logValue ?? indices.Count, TimeMs, isCrit: false, extraSuffix);
        if (effectTriggerName != null && EffectAppliedTriggerQueue != null)
        {
            foreach (int i in indices)
                EffectAppliedTriggerQueue.Add((effectTriggerName.Value, fromSide.SideIndex, i));
        }
    }

    private void ApplyToTargetsBothSides(int targetCount, Formula? condition, string effectName, int? logValue, Func<BattleSide, int, string> perTarget, int? effectTriggerName = null)
    {
        var targets = GetTargetsFromBothSides(targetCount, condition);
        if (targets.Count == 0) return;
        var targetNames = new List<string>();
        foreach (var (side, index) in targets)
            targetNames.Add(perTarget(side, index));
        string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
        LogSink.OnEffect(Caster, Caster.Template.Name, effectName, logValue ?? targets.Count, TimeMs, isCrit: false, extraSuffix);
        if (effectTriggerName != null && EffectAppliedTriggerQueue != null)
        {
            foreach (var (side, index) in targets)
                EffectAppliedTriggerQueue.Add((effectTriggerName.Value, side.SideIndex, index));
        }
    }

    private string ChargeItemAt(BattleSide side, int itemIndex, int chargeMs)
    {
        var target = side.Items[itemIndex];
        int cooldownMs = side.GetItemInt(itemIndex, Key.CooldownMs, 0);
        if (cooldownMs <= 0) return target.Template.Name;
        int newElapsed = Math.Min(cooldownMs, target.CooldownElapsedMs + chargeMs);
        target.CooldownElapsedMs = newElapsed;
        if (target.CooldownElapsedMs >= cooldownMs && side == Side && ChargeInducedCastQueue != null)
        {
            int ammoCap = side.GetItemInt(itemIndex, Key.AmmoCap, 0);
            if (ammoCap <= 0 || target.AmmoRemaining > 0)
                ChargeInducedCastQueue.Add(target);
            target.CooldownElapsedMs = 0;
        }
        return target.Template.Name;
    }

    public void ApplyFreeze(int freezeMs, int targetCount, Formula? targetCondition = null)
    {
        if (freezeMs <= 0 || targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.DifferentSide) & Condition.NotDestroyed;
        ApplyToTargetsBothSides(targetCount, cond, "冻结", freezeMs, (side, index) =>
        {
            var t = side.Items[index];
            int pct = Math.Clamp(t.Template.GetInt(Key.PercentFreezeReduction, t.Tier, 0), 0, 100);
            int effectiveMs = freezeMs - RatioUtil.PercentOf(freezeMs, pct);
            t.FreezeRemainingMs += effectiveMs;
            return t.Template.Name;
        }, Trigger.Freeze);
    }

    public void ApplySlow(int slowMs, int targetCount, Formula? targetCondition = null)
    {
        if (slowMs <= 0 || targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.DifferentSide) & Condition.NotDestroyed & Condition.HasCooldown;
        ApplyToTargetsBothSides(targetCount, cond, "减速", slowMs, (side, index) =>
        {
            var t = side.Items[index];
            t.SlowRemainingMs += slowMs;
            return t.Template.Name;
        }, Trigger.Slow);
    }

    public void ApplyCharge(int chargeMs, int targetCount, Formula? targetCondition = null)
    {
        if (chargeMs <= 0 || targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.SameSide) & Condition.NotDestroyed & Condition.HasCooldown;
        ApplyToTargetsBothSides(targetCount, cond, "充能", chargeMs, (side, index) => ChargeItemAt(side, index, chargeMs), null);
    }

    public void ApplyHaste(int hasteMs, int targetCount, Formula? targetCondition = null)
    {
        if (hasteMs <= 0 || targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.SameSide) & Condition.NotDestroyed & Condition.HasCooldown;
        ApplyToTargetsBothSides(targetCount, cond, EffectLogName ?? "加速", hasteMs, (side, index) =>
        {
            var t = side.Items[index];
            t.HasteRemainingMs += hasteMs;
            return t.Template.Name;
        }, Trigger.Haste);
    }

    public void ApplyReload(int amount, int targetCount, Formula? targetCondition = null)
    {
        if (amount <= 0 || targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.SameSide) & Condition.NotDestroyed & Condition.WithTag(DerivedTag.Ammo);
        ApplyToTargetsBothSides(targetCount, cond, EffectLogName ?? "装填", amount, (side, index) =>
        {
            var t = side.Items[index];
            int cap = t.GetAttribute(Key.AmmoCap);
            int add = Math.Min(amount, Math.Max(0, cap - t.AmmoRemaining));
            t.AmmoRemaining += add;
            if (side == Side && ChargeInducedCastQueue != null)
            {
                int cooldownMs = side.GetItemInt(index, Key.CooldownMs, 0);
                if (cooldownMs > 0 && t.CooldownElapsedMs >= cooldownMs && (cap <= 0 || t.AmmoRemaining > 0))
                {
                    ChargeInducedCastQueue.Add(t);
                    t.CooldownElapsedMs = 0;
                }
            }
            return t.Template.Name;
        }, null);
    }

    public void ApplyRepair(int targetCount, Formula? targetCondition = null)
    {
        if (targetCount <= 0) return;
        var condition = (targetCondition ?? Condition.SameSide) & Condition.Destroyed;
        ApplyToTargets(Side, targetCount, condition, "修复", null, (t, _) => { t.Destroyed = false; t.CooldownElapsedMs = 0; return t.Template.Name; }, null);
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

    public void AddAttributeToCasterSide(int attributeKey, int value, Formula? targetCondition, int maxTargetCount = 0)
    {
        if (value <= 0 || targetCondition == null) return;
        var cond = (targetCondition ?? Condition.SameSide) & Condition.NotDestroyed;
        if (attributeKey == Key.CritRatePercent)
            cond &= Condition.CanCrit;
        string logName = EffectLogName ?? (AttributeLogNames.Get(attributeKey) + "提高");
        if (maxTargetCount > 0)
        {
            var indices = GetTargetIndices(Side, maxTargetCount, cond);
            if (indices.Count == 0) return;
            var targetNames = new List<string>();
            foreach (int i in indices)
            {
                var wi = Side.Items[i];
                if (attributeKey == Key.Damage) { wi.SetAttribute(Key.Damage, wi.GetAttribute(Key.Damage) + value); targetNames.Add(wi.Template.Name); }
                else if (attributeKey == Key.Poison) { wi.SetAttribute(Key.Poison, wi.GetAttribute(Key.Poison) + value); targetNames.Add(wi.Template.Name); }
                else if (attributeKey == Key.CritRatePercent) { wi.SetAttribute(Key.CritRatePercent, wi.GetAttribute(Key.CritRatePercent) + value); targetNames.Add(wi.Template.Name); }
                else if (attributeKey == Key.InFlight) { wi.InFlight = value != 0; targetNames.Add(wi.Template.Name); }
                else { wi.SetAttribute(attributeKey, wi.GetAttribute(attributeKey) + value); targetNames.Add(wi.Template.Name); }
            }
            if (targetNames.Count > 0 && !string.IsNullOrEmpty(logName))
                LogSink.OnEffect(Caster, Caster.Template.Name, logName, value, TimeMs, isCrit: false, " →[" + string.Join("、", targetNames) + "]");
            return;
        }
        ApplyToSideWithCondition(Side, cond, logName, value, (wi, _) =>
        {
            if (attributeKey == Key.Damage) { wi.SetAttribute(Key.Damage, wi.GetAttribute(Key.Damage) + value); return wi.Template.Name; }
            if (attributeKey == Key.Poison) { wi.SetAttribute(Key.Poison, wi.GetAttribute(Key.Poison) + value); return wi.Template.Name; }
            if (attributeKey == Key.CritRatePercent) { wi.SetAttribute(Key.CritRatePercent, wi.GetAttribute(Key.CritRatePercent) + value); return wi.Template.Name; }
            if (attributeKey == Key.InFlight) { wi.InFlight = value != 0; return wi.Template.Name; }
            wi.SetAttribute(attributeKey, wi.GetAttribute(attributeKey) + value);
            return wi.Template.Name;
        });
    }

    public void SetAttributeOnCasterSide(int attributeKey, int value, Formula? targetCondition)
    {
        if (targetCondition == null) return;
        string logName = EffectLogName ?? (attributeKey == Key.InFlight && value == 0 ? "结束飞行" : AttributeLogNames.Get(attributeKey) + "变更");
        ApplyToSideWithCondition(Side, targetCondition, logName, 0, (wi, _) =>
        {
            if (attributeKey == Key.InFlight) { wi.InFlight = value != 0; return wi.Template.Name; }
            wi.SetAttribute(attributeKey, value);
            return wi.Template.Name;
        });
    }

    public void ReduceAttributeToSide(int attributeKey, int value, Formula? targetCondition, int maxTargetCount = 0, string? effectLogName = null)
    {
        if (value <= 0 || targetCondition == null) return;
        var cond = (targetCondition ?? Condition.DifferentSide) & Condition.NotDestroyed;
        if (attributeKey == Key.CritRatePercent)
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

            if (attributeKey == Key.CooldownMs && side == Side && wi.CooldownElapsedMs >= newVal && ChargeInducedCastQueue != null)
            {
                int ammoCap = side.GetItemInt(index, Key.AmmoCap, 0);
                if (ammoCap <= 0 || wi.AmmoRemaining > 0)
                    ChargeInducedCastQueue.Add(wi);
                wi.CooldownElapsedMs = 0;
            }
        }
        if (targetNames.Count > 0 && !string.IsNullOrEmpty(logName))
            LogSink.OnEffect(Caster, Caster.Template.Name, logName, value, TimeMs, isCrit: false, " →[" + string.Join("、", targetNames) + "]");
    }

    public void ReportTriggerCause(int triggerName) =>
        EffectAppliedTriggerQueue?.Add((triggerName, Side.SideIndex, Caster.ItemIndex));

    public void LogEffect(string effectName, int value, string? extraSuffix = null, bool showCrit = false) =>
        LogSink.OnEffect(Caster, Caster.Template.Name, effectName, value, TimeMs, showCrit, extraSuffix);

    public void SetCasterInFlight(bool inFlight) => Caster.InFlight = inFlight;

    public void ApplyDestroy(int targetCount, Formula? targetCondition = null)
    {
        if (targetCount <= 0) return;
        var cond = (targetCondition ?? Condition.SameSide) & Condition.NotDestroyed;
        var sideIndices = GetTargetIndices(Side, targetCount, cond);
        BattleSide targetSide;
        List<int> indices;
        if (sideIndices.Count > 0)
        {
            targetSide = Side;
            indices = sideIndices;
        }
        else
        {
            indices = GetTargetIndices(Opp, targetCount, cond);
            targetSide = Opp;
        }
        if (indices.Count == 0) return;
        var targetNames = indices.Select(i => targetSide.Items[i].Template.Name).ToList();
        string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
        LogEffect("摧毁", indices.Count, extraSuffix, showCrit: false);
        if (EffectAppliedTriggerQueue != null)
        {
            foreach (int i in indices)
                EffectAppliedTriggerQueue.Add((Trigger.Destroy, targetSide.SideIndex, i));
        }
        else
        {
            foreach (int i in indices)
                targetSide.Items[i].Destroyed = true;
        }
    }
}
