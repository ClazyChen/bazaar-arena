using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

public static class Condition
{
    private static bool HasRuntimeTag(BattleContext ctx, int tagMask)
    {
        // Tag 与 DerivedTag 使用独立位域，需分别判定后再合并语义。
        return (ctx.Item.GetAttribute(Key.Tags) & tagMask) != 0
            || (ctx.Item.GetAttribute(Key.DerivedTags) & tagMask) != 0;
    }

    private static bool HasTemplateTag(BattleContext ctx, int tagMask)
    {
        return (ctx.GetItemInt(ctx.Item, Key.Tags) & tagMask) != 0
            || (ctx.GetItemInt(ctx.Item, Key.DerivedTags) & tagMask) != 0;
    }

    /// <summary>
    /// 与 <see cref="BattleContext.Caster"/> 比较方位的「另一端」：
    /// 若 <see cref="BattleContext.Source"/> 与 Caster 为同槽（效果/公式里常如此），则用 <see cref="BattleContext.Item"/>（候选）；
    /// 否则用 Source（触发器下为引起触发的那件物品）。
    /// </summary>
    private static ItemState SpatialOtherVsCaster(BattleContext ctx)
    {
        if (ctx.Source == null) return ctx.Item;
        if (ctx.Source.SideIndex == ctx.Caster.SideIndex && ctx.Source.ItemIndex == ctx.Caster.ItemIndex)
            return ctx.Item;
        return ctx.Source;
    }

    public static Formula Always { get; } = Formula.True;

    /// <summary>引起触发者（Source）与能力持有者（Caster）为同一件物品（如自用 UseItem）。</summary>
    public static Formula SameAsCaster { get; } = new(ctx =>
        ctx.Source != null
        && ctx.Source.SideIndex == ctx.Caster.SideIndex
        && ctx.Source.ItemIndex == ctx.Caster.ItemIndex ? 1 : 0);

    /// <summary>引起触发者（Source）与能力持有者（Caster）不是同一件物品。</summary>
    public static Formula DifferentFromCaster { get; } = new(ctx =>
        ctx.Source == null
        || ctx.Source.SideIndex != ctx.Caster.SideIndex
        || ctx.Source.ItemIndex != ctx.Caster.ItemIndex ? 1 : 0);

    /// <summary>Source 与 Caster 在同一阵营侧。</summary>
    public static Formula SameSide { get; } = new(ctx =>
        ctx.Source == null
        || ctx.Source.SideIndex == ctx.Caster.SideIndex ? 1 : 0);

    /// <summary>Source 与 Caster 在不同阵营侧。</summary>
    public static Formula DifferentSide { get; } = new(ctx =>
        ctx.Source != null && ctx.Source.SideIndex != ctx.Caster.SideIndex ? 1 : 0);

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

    public static Formula InFlight { get; } = new(ctx => ctx.Item.InFlight ? 1 : 0);
    public static Formula NotInFlight { get; } = new(ctx => !ctx.Item.InFlight ? 1 : 0);
    public static Formula NotDestroyed { get; } = new(ctx => !ctx.Item.Destroyed ? 1 : 0);
    public static Formula Destroyed { get; } = new(ctx => ctx.Item.Destroyed ? 1 : 0);
    public static Formula HasCooldown { get; } = new(ctx =>
        ctx.GetItemInt(ctx.Item, Key.CooldownMs) > 0 ? 1 : 0);
    public static Formula AmmoDepleted { get; } = new(ctx => ctx.Item.AmmoRemaining == 0 ? 1 : 0);
    public static Formula IsFrozen { get; } = new(ctx => ctx.Item.FreezeRemainingMs > 0 ? 1 : 0);
    public static Formula Leftmost { get; } = new(ctx => ctx.Item.ItemIndex == 0 ? 1 : 0);
    public static Formula OnlyCompanion { get; } = Formula.False;
    public static Formula OnlyWeaponWithCooldown { get; } = Formula.False;

    /// <summary>能力持有者（Caster）的 Custom_0 为 0；用于「首次」等，与光环 SourceCondition 一致。</summary>
    public static Formula CasterCustom0IsZero { get; } = new(ctx =>
        ctx.Caster.GetAttribute(Key.Custom_0) == 0 ? 1 : 0);

    public static Formula HasAnyCrittableTag { get; } = Formula.True;
    public static Formula CanCrit { get; } = Formula.True;

    /// <summary>候选（Item）为 Caster 同侧右侧第一个未摧毁物品（中间槽位须均已摧毁）。</summary>
    public static Formula FirstNonDestroyedRightOfCaster { get; } = new(ctx =>
    {
        if (ctx.Item.SideIndex != ctx.Caster.SideIndex) return 0;
        if (ctx.Item.Destroyed) return 0;
        if (ctx.Item.ItemIndex <= ctx.Caster.ItemIndex) return 0;
        var side = ctx.Caster.SideIndex == 0 ? ctx.BattleState.Side0 : ctx.BattleState.Side1;
        for (int j = ctx.Caster.ItemIndex + 1; j < ctx.Item.ItemIndex; j++)
        {
            if (j >= side.Items.Count) return 0;
            if (!side.Items[j].Destroyed) return 0;
        }
        return 1;
    });

    public static Formula WithTag(int tagMask) => new(ctx => HasRuntimeTag(ctx, tagMask) ? 1 : 0);
    public static Formula NotWithTag(int tagMask) => new(ctx => HasRuntimeTag(ctx, tagMask) ? 0 : 1);
    public static Formula WithTemplateTag(int tagMask) => new(ctx => HasTemplateTag(ctx, tagMask) ? 1 : 0);
    public static Formula LeftMost(Formula inner) => inner;
    public static Formula RightMost(Formula inner) => inner;
}
