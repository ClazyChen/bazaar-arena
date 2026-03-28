namespace BazaarArena.BattleSimulator;

/// <summary>伤害结算时与护盾的交互规则。</summary>
internal enum DamageShieldRule
{
    /// <summary>等额吸收（普通伤害、沙尘暴等）。</summary>
    Standard,
    /// <summary>灼烧：护盾按 2:1 吸收。</summary>
    Burn,
    /// <summary>剧毒周期伤害：护盾不阻挡，直接作用于生命（与设计一致）。</summary>
    PoisonTick,
}

/// <summary>对一方造成伤害的结算逻辑（护盾吸收规则等），供模拟器与效果上下文共用。</summary>
internal static class BattleSideDamage
{
    /// <summary>对一方造成伤害，返回实际扣减的生命值（用于吸血等）。护盾规则见 <see cref="DamageShieldRule"/>。</summary>
    public static int ApplyDamageToSide(BattleSide side, int damage, DamageShieldRule shieldRule)
    {
        switch (shieldRule)
        {
            case DamageShieldRule.Burn:
                // damage 为 1 时，会被护盾完全吸收，无法造成伤害；但 damage 为 2 时，会消耗 1 点护盾造成 2 点伤害。
                int burnShieldConsume = Math.Min(side.Shield, damage / 2);
                int burnShieldDamage = burnShieldConsume * 2;
                side.Shield -= burnShieldConsume;
                damage = Math.Max(0, damage - burnShieldDamage);
                break;
            case DamageShieldRule.Standard:
                int shieldConsume = Math.Min(side.Shield, damage);
                side.Shield -= shieldConsume;
                damage -= shieldConsume;
                break;
            case DamageShieldRule.PoisonTick:
                break;
        }
        int actualHpDamage = Math.Max(0, damage);
        if (actualHpDamage <= 0) return 0;

        // 无敌：只豁免“生命值降低”，护盾仍按规则吸收并减少。
        if (side.InvincibleRemainingMs > 0)
            return 0;

        side.Hp -= actualHpDamage;
        return actualHpDamage;
    }
}
