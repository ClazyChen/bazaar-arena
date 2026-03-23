using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>手里剑（Shuriken）：海盗小型武器、弹药；造成伤害，多重释放 = 剩余弹药 - 1（光环）。</summary>
public static class Shuriken
{
    /// <summary>手里剑：8s 小 铜 武器；▶ 造成 5 » 10 » 15 » 20 伤害；弹药：{AmmoCap}；此物品的多重释放次数等于剩余弹药数量（光环实现为 AmmoRemaining - 1）；使用此物品时消耗 1 发弹药。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "手里剑",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；此物品的多重释放次数等于剩余弹药数量；使用此物品时，消耗全部弹药",
            Tags = [Tag.Weapon],
            Cooldown = 8.0,
            Damage = [5, 10, 15, 20],
            AmmoCap = [3, 4, 5, 6],
            Abilities =
            [
                Ability.ReduceAttribute(Key.AmmoRemaining, amountKey: Key.AmmoCap).Override(
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Immediate,
                    effectLogName: ""
                ),
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = Formula.Source(Key.AmmoRemaining) - Formula.Constant(1),
                },
            ],
        };
    }
}
