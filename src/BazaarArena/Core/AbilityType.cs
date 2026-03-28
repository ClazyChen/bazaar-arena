namespace BazaarArena.Core;

public enum AbilityType
{
    None = 0,
    Damage = 1,
    Shield = 2,
    Heal = 3,
    Burn = 4,
    Poison = 5,
    Charge = 6,
    Haste = 7,
    Slow = 8,
    Freeze = 9,
    Reload = 10,
    Repair = 11,
    Destroy = 12,
    AddAttribute = 13,
    ReduceAttribute = 14,
    GainGold = 15,
    Invincible = 16,
    UseThisItem = 17,
    /// <summary>清除己方无敌剩余时间（用于「首次使用物品时解除无敌」等）。</summary>
    ClearInvincible = 18,
    /// <summary>提高己方阵营生命再生（写入阵营 Regen）；与 <see cref="Ability.Regen"/> / <see cref="Key.Regen"/> 对齐。</summary>
    Regen = 19,
}
