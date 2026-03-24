using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>救生圈（Life Preserver）：海盗中型水系。▶ 使用物品时获得 10 » 20 » 40 » 80 护盾；每场战斗即将首次落败时治疗 200 » 500 » 1000 » 2000 生命值(I)，「首次」由物品 Custom_0 保证（参考靴里剑）。</summary>
public static class LifePreserver
{
    /// <summary>救生圈：12s 中 铜 水系；▶ 获得护盾；即将首次落败时治疗（Immediate），Custom_0=0 时生效、生效后置 1。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "救生圈",
            Desc = "▶ 获得 {Shield} 护盾；每场战斗即将首次落败时，治疗 {Heal} 生命值",
            Tags = [Tag.Aquatic],
            Cooldown = 12.0,
            Shield = [10, 20, 40, 80],
            Heal = [200, 500, 1000, 2000],
            Custom_0 = 0,
            Custom_1 = 1,
            Abilities =
            [
                Ability.Shield,
                Ability.Heal.Override(
                    trigger: Trigger.AboutToLose,
                    additionalCondition: Condition.CasterCustom0IsZero,
                    priority: AbilityPriority.Immediate
                ),
                Ability.AddAttribute(Key.Custom_0).Override(
                    trigger: Trigger.AboutToLose,
                    additionalCondition: Condition.CasterCustom0IsZero,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1,
                    effectLogName: "",
                    priority: AbilityPriority.Immediate
                ),
            ],
        };
    }
}
