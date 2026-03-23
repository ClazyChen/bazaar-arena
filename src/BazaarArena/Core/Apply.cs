namespace BazaarArena.Core;

public static class Apply
{
    public static readonly Action<BattleContext> Damage = _ => { };
    public static readonly Action<BattleContext> Shield = _ => { };
    public static readonly Action<BattleContext> Heal = _ => { };
    public static readonly Action<BattleContext> Burn = _ => { };
    public static readonly Action<BattleContext> Poison = _ => { };
    public static readonly Action<BattleContext> Charge = _ => { };
    public static readonly Action<BattleContext> Haste = _ => { };
    public static readonly Action<BattleContext> Slow = _ => { };
    public static readonly Action<BattleContext> Freeze = _ => { };
    public static readonly Action<BattleContext> Reload = _ => { };
    public static readonly Action<BattleContext> Repair = _ => { };
    public static readonly Action<BattleContext> Destroy = _ => { };
    public static readonly Action<BattleContext> GainGold = _ => { };
}
