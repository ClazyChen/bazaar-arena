using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

public static class Condition
{
    /// <summary>
    /// 与 <see cref="BattleContext.Caster"/> 比较方位的「另一端」：
    /// 若 <see cref="BattleContext.Source"/> 与 Caster 为同槽（效果/公式里常如此），则用 <see cref="BattleContext.Item"/>（候选）；
    /// 否则用 Source（触发器下为引起触发的那件物品）。
    /// </summary>
    private static ItemState SpatialOtherVsCaster(BattleContext ctx)
    {
        if (ctx.Source.SideIndex == ctx.Caster.SideIndex && ctx.Source.ItemIndex == ctx.Caster.ItemIndex)
            return ctx.Item;
        return ctx.Source;
    }

    public static Formula Always { get; } = Formula.True;

    /// <summary>引起触发者（Source）与能力持有者（Caster）为同一件物品（如自用 UseItem）。</summary>
    public static Formula SameAsCaster { get; } = new(ctx =>
        ctx.Source.SideIndex == ctx.Caster.SideIndex
        && ctx.Source.ItemIndex == ctx.Caster.ItemIndex ? 1 : 0);

    /// <summary>引起触发者（Source）与能力持有者（Caster）不是同一件物品。</summary>
    public static Formula DifferentFromCaster { get; } = new(ctx =>
        ctx.Source.SideIndex != ctx.Caster.SideIndex
        || ctx.Source.ItemIndex != ctx.Caster.ItemIndex ? 1 : 0);

    /// <summary>Source 与 Caster 在同一阵营侧。</summary>
    public static Formula SameSide { get; } = new(ctx =>
        ctx.Source.SideIndex == ctx.Caster.SideIndex ? 1 : 0);

    /// <summary>Source 与 Caster 在不同阵营侧。</summary>
    public static Formula DifferentSide { get; } = new(ctx =>
        ctx.Source.SideIndex != ctx.Caster.SideIndex ? 1 : 0);

    public static Formula SameAsInvokeTarget { get; } = new(ctx =>
        ctx.InvokeTarget != null
        && ctx.Item.SideIndex == ctx.InvokeTarget.SideIndex
        && ctx.Item.ItemIndex == ctx.InvokeTarget.ItemIndex ? 1 : 0);

    public static Formula AdjacentToCaster { get; } = new(ctx =>
    {
        var o = SpatialOtherVsCaster(ctx);
        return o.SideIndex == ctx.Caster.SideIndex
            && Math.Abs(o.ItemIndex - ctx.Caster.ItemIndex) == 1 ? 1 : 0;
    });

    public static Formula RightOfCaster { get; } = new(ctx =>
    {
        var o = SpatialOtherVsCaster(ctx);
        return o.SideIndex == ctx.Caster.SideIndex
            && o.ItemIndex == ctx.Caster.ItemIndex + 1 ? 1 : 0;
    });

    public static Formula LeftOfCaster { get; } = new(ctx =>
    {
        var o = SpatialOtherVsCaster(ctx);
        return o.SideIndex == ctx.Caster.SideIndex
            && o.ItemIndex == ctx.Caster.ItemIndex - 1 ? 1 : 0;
    });

    public static Formula StrictlyLeftOfCaster { get; } = new(ctx =>
    {
        var o = SpatialOtherVsCaster(ctx);
        return o.SideIndex == ctx.Caster.SideIndex
            && o.ItemIndex < ctx.Caster.ItemIndex ? 1 : 0;
    });

    public static Formula StrictlyRightOfCaster { get; } = new(ctx =>
    {
        var o = SpatialOtherVsCaster(ctx);
        return o.SideIndex == ctx.Caster.SideIndex
            && o.ItemIndex > ctx.Caster.ItemIndex ? 1 : 0;
    });

    public static Formula InFlight { get; } = Formula.Item(Key.InFlight);
    public static Formula Destroyed { get; } = Formula.Item(Key.Destroyed);
    public static Formula HasCooldown { get; } = new(ctx =>
        ctx.GetItemInt(ctx.Item, Key.CooldownMs) > 0 ? 1 : 0);
    public static Formula AmmoDepleted { get; } = new(ctx => ctx.GetItemInt(ctx.Item, Key.AmmoRemaining) == 0 ? 1 : 0);
    public static Formula IsFrozen { get; } = new(ctx => ctx.Item.FreezeRemainingMs > 0 ? 1 : 0);
    public static Formula Leftmost { get; } = new(ctx => ctx.Item.ItemIndex == 0 ? 1 : 0);

    private static Formula IsCompanionCandidate { get; } =
        SameSide & ~Destroyed & WithTag(Tag.Friend);

    private static Formula IsWeaponWithCooldownCandidate { get; } =
        SameSide & ~Destroyed & WithTag(Tag.Weapon) & HasCooldown;

    public static Formula OnlyCompanion { get; } =
        IsCompanionCandidate & Formula.Apply(Formula.Count(IsCompanionCandidate), n => n == 1 ? 1 : 0);

    public static Formula OnlyWeaponWithCooldown { get; } =
        IsWeaponWithCooldownCandidate & Formula.Apply(Formula.Count(IsWeaponWithCooldownCandidate), n => n == 1 ? 1 : 0);

    /// <summary>能力持有者（Caster）的 Custom_0 为 0；用于「首次」等，与光环 SourceCondition 一致。</summary>
    public static Formula CasterCustom0IsZero { get; } = new(ctx =>
        ctx.Caster.GetAttribute(Key.Custom_0) == 0 ? 1 : 0);

    public static Formula CanCrit { get; } = new(ctx => ctx.Item.CanCrit ? 1 : 0);

    /// <summary>候选（Item）为 Caster 同侧右侧第一个未摧毁物品（中间槽位须均已摧毁）。</summary>
    public static Formula FirstNonDestroyedRightOfCaster { get; } = new(ctx =>
    {
        if (ctx.Item.SideIndex != ctx.Caster.SideIndex) return 0;
        if (ctx.Item.Destroyed) return 0;
        if (ctx.Item.ItemIndex <= ctx.Caster.ItemIndex) return 0;
        var side = ctx.BattleState.Side[ctx.Caster.SideIndex];
        for (int j = ctx.Caster.ItemIndex + 1; j < ctx.Item.ItemIndex; j++)
        {
            if (j >= side.Items.Count) return 0;
            if (!side.Items[j].Destroyed) return 0;
        }
        return 1;
    });

    public static Formula WithTag(int tagMask) => new(ctx => (ctx.GetItemInt(ctx.Item, Key.Tags) & tagMask) != 0 ? 1 : 0);
    public static Formula WithDerivedTag(int tagMask) => new(ctx => (ctx.GetItemInt(ctx.Item, Key.DerivedTags) & tagMask) != 0 ? 1 : 0);
    public static Formula LeftMost(Formula inner) => new(ctx =>
    {
        if (inner.Evaluate(ctx) == 0) return 0;
        int sideIndex = ctx.Item.SideIndex;
        int minIndex = int.MaxValue;
        var side = ctx.BattleState.Side[sideIndex];
        var scanCtx = new BattleContext
        {
            BattleState = ctx.BattleState,
            Caster = ctx.Caster,
            Source = ctx.Source,
            InvokeTarget = ctx.InvokeTarget,
        };
        foreach (var it in side.Items)
        {
            scanCtx.Item = it;
            if (inner.Evaluate(scanCtx) == 0) continue;
            minIndex = Math.Min(minIndex, it.ItemIndex);
        }
        if (minIndex == int.MaxValue) return 0;
        return ctx.Item.ItemIndex == minIndex ? 1 : 0;
    });

    public static Formula RightMost(Formula inner) => new(ctx =>
    {
        if (inner.Evaluate(ctx) == 0) return 0;
        int sideIndex = ctx.Item.SideIndex;
        int maxIndex = int.MinValue;
        var side = ctx.BattleState.Side[sideIndex];
        var scanCtx = new BattleContext
        {
            BattleState = ctx.BattleState,
            Caster = ctx.Caster,
            Source = ctx.Source,
            InvokeTarget = ctx.InvokeTarget,
        };
        foreach (var it in side.Items)
        {
            scanCtx.Item = it;
            if (inner.Evaluate(scanCtx) == 0) continue;
            maxIndex = Math.Max(maxIndex, it.ItemIndex);
        }
        if (maxIndex == int.MinValue) return 0;
        return ctx.Item.ItemIndex == maxIndex ? 1 : 0;
    });
}
