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

    public bool HasLifeSteal => Item.Template.GetInt(nameof(ItemTemplate.LifeSteal), Item.Tier, 0) != 0;

    public int GetResolvedValue(string key, bool applyCritMultiplier)
    {
        int baseValue = Item.Template.GetInt(key, Item.Tier, 0);
        return applyCritMultiplier ? baseValue * CritMultiplier : baseValue;
    }

    public int GetCasterItemInt(string key, int defaultValue) => Item.Template.GetInt(key, Item.Tier, defaultValue);

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
        int cooldownMs = Item.GetCooldownMs();
        if (cooldownMs <= 0) return;
        int newElapsed = Math.Min(cooldownMs, Item.CooldownElapsedMs + chargeMs);
        int added = newElapsed - Item.CooldownElapsedMs;
        Item.CooldownElapsedMs = newElapsed;
        if (added > 0)
            LogSink.OnEffect(SideIndex, ItemIndex, Item.Template.Name, "充能", added, TimeMs, isCrit: false);
        if (Item.CooldownElapsedMs >= cooldownMs && ChargeInducedCastQueue != null)
        {
            if (Item.GetAmmoCap() <= 0 || Item.AmmoRemaining > 0)
                ChargeInducedCastQueue.Add((SideIndex, ItemIndex));
            fullAndShouldCast = true;
            Item.CooldownElapsedMs = 0;
        }
    }

    public void ApplyFreeze(int freezeMs, int targetCount)
    {
        if (freezeMs <= 0 || targetCount <= 0) return;
        var withCooldown = new List<int>();
        var withoutCooldown = new List<int>();
        for (int i = 0; i < Opp.Items.Count; i++)
        {
            if (Opp.Items[i].Destroyed) continue;
            if (Opp.Items[i].GetCooldownMs() > 0) withCooldown.Add(i);
            else withoutCooldown.Add(i);
        }
        var pool = withCooldown.Count > 0 ? withCooldown : withoutCooldown;
        if (pool.Count == 0) return;
        var targetNames = new List<string>();
        for (int n = 0; n < targetCount; n++)
        {
            int idx = Random.Shared.Next(pool.Count);
            var target = Opp.Items[pool[idx]];
            target.FreezeRemainingMs = Math.Max(target.FreezeRemainingMs, freezeMs);
            targetNames.Add(target.Template.Name);
        }
        string extraSuffix = string.Concat(targetNames.Select(name => " →[" + name + "]"));
        LogSink.OnEffect(SideIndex, ItemIndex, Item.Template.Name, "冻结", freezeMs, TimeMs, isCrit: false, extraSuffix);
    }

    public void ApplySlow(int slowMs, int targetCount)
    {
        if (slowMs <= 0 || targetCount <= 0) return;
        var withCooldown = new List<int>();
        var withoutCooldown = new List<int>();
        for (int i = 0; i < Opp.Items.Count; i++)
        {
            if (Opp.Items[i].Destroyed) continue;
            if (Opp.Items[i].GetCooldownMs() > 0) withCooldown.Add(i);
            else withoutCooldown.Add(i);
        }
        var pool = withCooldown.Count > 0 ? withCooldown : withoutCooldown;
        if (pool.Count == 0) return;
        var targetNames = new List<string>();
        for (int n = 0; n < targetCount; n++)
        {
            int idx = Random.Shared.Next(pool.Count);
            var target = Opp.Items[pool[idx]];
            target.SlowRemainingMs = Math.Max(target.SlowRemainingMs, slowMs);
            targetNames.Add(target.Template.Name);
        }
        string extraSuffix = string.Concat(targetNames.Select(name => " →[" + name + "]"));
        LogSink.OnEffect(SideIndex, ItemIndex, Item.Template.Name, "减速", slowMs, TimeMs, isCrit: false, extraSuffix);
    }

    public void AddWeaponDamageBonusToCasterSide(int value)
    {
        foreach (var wi in Side.Items)
        {
            if (wi.Destroyed) continue;
            if (wi.Template.Tags.Contains(Tag.Weapon))
                wi.Template.Damage = wi.Template.Damage.Add(value);
        }
    }

    public void ReduceOpponentShieldItemsShield(int reduceBy)
    {
        if (reduceBy <= 0) return;
        var targetNames = new List<string>();
        foreach (var oppItem in Opp.Items)
        {
            if (oppItem.Destroyed) continue;
            if (!oppItem.TypeSnapshot.IsShieldItem) continue;
            int current = oppItem.Template.GetInt(nameof(ItemTemplate.Shield), oppItem.Tier, 0);
            int newShield = Math.Max(0, current - reduceBy);
            oppItem.Template.SetInt(nameof(ItemTemplate.Shield), newShield);
            targetNames.Add(oppItem.Template.Name);
        }
        if (targetNames.Count > 0)
        {
            string extraSuffix = " →[" + string.Join("、", targetNames) + "]";
            LogEffect("裂盾", reduceBy, extraSuffix, showCrit: false);
        }
    }

    public void LogEffect(string effectName, int value, string? extraSuffix = null, bool showCrit = false) =>
        LogSink.OnEffect(SideIndex, ItemIndex, Item.Template.Name, effectName, value, TimeMs, showCrit, extraSuffix);
}
