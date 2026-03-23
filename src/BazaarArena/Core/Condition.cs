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
        return (ctx.Item.Template.GetInt(Key.Tags, ctx.Item.Tier, 0) & tagMask) != 0
            || (ctx.Item.Template.GetInt(Key.DerivedTags, ctx.Item.Tier, 0) & tagMask) != 0;
    }

    public static Formula Always { get; } = Formula.True;
    public static Formula SameAsSource { get; } = new(ctx =>
        ctx.Source != null
        && ctx.Item.SideIndex == ctx.Source.SideIndex
        && ctx.Item.ItemIndex == ctx.Source.ItemIndex ? 1 : 0);
    public static Formula DifferentFromSource { get; } = new(ctx =>
        ctx.Source == null
        || ctx.Item.SideIndex != ctx.Source.SideIndex
        || ctx.Item.ItemIndex != ctx.Source.ItemIndex ? 1 : 0);
    public static Formula SameSide { get; } = new(ctx =>
        ctx.Source == null || ctx.Item.SideIndex == ctx.Source.SideIndex ? 1 : 0);
    public static Formula DifferentSide { get; } = new(ctx =>
        ctx.Source != null && ctx.Item.SideIndex != ctx.Source.SideIndex ? 1 : 0);
    public static Formula SameAsInvokeTarget { get; } = new(ctx =>
        ctx.InvokeTarget != null
        && ctx.Item.SideIndex == ctx.InvokeTarget.SideIndex
        && ctx.Item.ItemIndex == ctx.InvokeTarget.ItemIndex ? 1 : 0);
    public static Formula AdjacentToSource { get; } = new(ctx =>
        ctx.Source != null
        && ctx.Item.SideIndex == ctx.Source.SideIndex
        && Math.Abs(ctx.Item.ItemIndex - ctx.Source.ItemIndex) == 1 ? 1 : 0);
    public static Formula RightOfSource { get; } = new(ctx =>
        ctx.Source != null
        && ctx.Item.SideIndex == ctx.Source.SideIndex
        && ctx.Item.ItemIndex == ctx.Source.ItemIndex + 1 ? 1 : 0);
    public static Formula LeftOfSource { get; } = new(ctx =>
        ctx.Source != null
        && ctx.Item.SideIndex == ctx.Source.SideIndex
        && ctx.Item.ItemIndex == ctx.Source.ItemIndex - 1 ? 1 : 0);
    public static Formula StrictlyLeftOfSource { get; } = new(ctx =>
        ctx.Source != null
        && ctx.Item.SideIndex == ctx.Source.SideIndex
        && ctx.Item.ItemIndex < ctx.Source.ItemIndex ? 1 : 0);
    public static Formula StrictlyRightOfSource { get; } = new(ctx =>
        ctx.Source != null
        && ctx.Item.SideIndex == ctx.Source.SideIndex
        && ctx.Item.ItemIndex > ctx.Source.ItemIndex ? 1 : 0);
    public static Formula InFlight { get; } = new(ctx => ctx.Item.InFlight ? 1 : 0);
    public static Formula NotInFlight { get; } = new(ctx => !ctx.Item.InFlight ? 1 : 0);
    public static Formula NotDestroyed { get; } = new(ctx => !ctx.Item.Destroyed ? 1 : 0);
    public static Formula Destroyed { get; } = new(ctx => ctx.Item.Destroyed ? 1 : 0);
    public static Formula HasCooldown { get; } = new(ctx =>
        ctx.Item.Template.GetInt(Key.CooldownMs, ctx.Item.Tier, 0) > 0 ? 1 : 0);
    public static Formula AmmoDepleted { get; } = new(ctx => ctx.Item.AmmoRemaining == 0 ? 1 : 0);
    public static Formula IsFrozen { get; } = new(ctx => ctx.Item.FreezeRemainingMs > 0 ? 1 : 0);
    public static Formula Leftmost { get; } = new(ctx => ctx.Item.ItemIndex == 0 ? 1 : 0);
    public static Formula OnlyCompanion { get; } = Formula.False;
    public static Formula OnlyWeaponWithCooldown { get; } = Formula.False;
    public static Formula SourceCustom0IsZero { get; } = new(ctx => ctx.Source != null && ctx.Source.GetAttribute(Key.Custom_0) == 0 ? 1 : 0);
    public static Formula HasAnyCrittableTag { get; } = Formula.True;
    public static Formula CanCrit { get; } = Formula.True;
    public static Formula FirstNonDestroyedRightOfSource { get; } = Formula.False;

    public static Formula WithTag(int tagMask) => new(ctx => HasRuntimeTag(ctx, tagMask) ? 1 : 0);
    public static Formula NotWithTag(int tagMask) => new(ctx => HasRuntimeTag(ctx, tagMask) ? 0 : 1);
    public static Formula WithTemplateTag(int tagMask) => new(ctx => HasTemplateTag(ctx, tagMask) ? 1 : 0);
    public static Formula LeftMost(Formula inner) => inner;
    public static Formula RightMost(Formula inner) => inner;
}
