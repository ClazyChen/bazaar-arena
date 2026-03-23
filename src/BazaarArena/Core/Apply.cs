namespace BazaarArena.Core;

public static class Apply
{
    private static IEffectApplyContext Ctx() => BazaarArena.BattleSimulator.BattleSimulatorThreadScratch.CurrentEffectApplyContextOrThrow();

    public static readonly Action<BattleContext> Damage = _ => Effect.DamageApply(Ctx());
    public static readonly Action<BattleContext> Shield = _ => Effect.ShieldApply(Ctx());
    public static readonly Action<BattleContext> Heal = _ => Effect.HealApply(Ctx());
    public static readonly Action<BattleContext> Burn = _ => Effect.BurnApply(Ctx());
    public static readonly Action<BattleContext> Poison = _ => Effect.PoisonApply(Ctx());
    public static readonly Action<BattleContext> Charge = _ => Effect.ChargeApply(Ctx());
    public static readonly Action<BattleContext> Haste = _ => Effect.HasteApply(Ctx());
    public static readonly Action<BattleContext> Slow = _ => Effect.SlowApply(Ctx());
    public static readonly Action<BattleContext> Freeze = _ => Effect.FreezeApply(Ctx());
    public static readonly Action<BattleContext> Reload = _ => Effect.ReloadApply(Ctx());
    public static readonly Action<BattleContext> Repair = _ => Effect.RepairApply(Ctx());
    public static readonly Action<BattleContext> Destroy = _ => Effect.DestroyApply(Ctx());
    public static readonly Action<BattleContext> GainGold = _ => Effect.GainGoldApply(Ctx());

    public static Action<BattleContext> AddAttribute(int attributeKey) =>
        _ => Effect.AddAttributeApply(attributeKey)(Ctx());

    public static Action<BattleContext> ReduceAttribute(int attributeKey) =>
        _ => Effect.ReduceAttributeApply(attributeKey)(Ctx());

    public static readonly Action<BattleContext> StopFlying = _ => Effect.StopFlyingApply(Ctx());
}
