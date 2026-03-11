namespace BazaarArena.BattleSimulator;

/// <summary>对一方造成伤害的结算逻辑（护盾吸收规则等），供模拟器与效果上下文共用。</summary>
internal static class BattleSideDamage
{
    /// <summary>对一方造成伤害，返回实际扣减的生命值（用于吸血等）。灼烧时护盾按 2:1 吸收，普通伤害 1:1。</summary>
    public static int ApplyDamageToSide(BattleSide side, int damage, bool isBurn)
    {
        if (isBurn)
        {
            // damage 为 1 时，会被护盾完全吸收，无法造成伤害；但 damage 为 2 时，会消耗 1 点护盾造成 2 点伤害。
            int shieldConsume = Math.Min(side.Shield, damage / 2);
            int shieldDamage = shieldConsume * 2;
            side.Shield -= shieldConsume;
            damage = Math.Max(0, damage - shieldDamage);
        }
        else
        {
            int shieldConsume = Math.Min(side.Shield, damage);
            side.Shield -= shieldConsume;
            damage -= shieldConsume;
        }
        int actualHpDamage = Math.Max(0, damage);
        side.Hp -= actualHpDamage;
        return actualHpDamage;
    }
}
