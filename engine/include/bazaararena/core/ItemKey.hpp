#pragma once

namespace bazaararena::core {

// 所有的物品属性，只有标注可以被光环影响的才会实际被光环影响
class ItemKey final {
public:
    static constexpr int Id = 0; // 物品的模板 ID，用于区分不同的物品
    static constexpr int SideIndex = 1; // 战斗内阵营下标
    static constexpr int ItemIndex = 2; // 战斗内物品在阵营中的下标
    static constexpr int Damage = 3; // 物品伤害，可以被光环影响
    static constexpr int Burn = 4; // 物品灼烧，可以被光环影响
    static constexpr int Poison = 5; // 物品剧毒，可以被光环影响
    static constexpr int Shield = 6; // 物品护盾，可以被光环影响
    static constexpr int Heal = 7; // 物品治疗，可以被光环影响
    static constexpr int Regen = 8; // 物品生命再生，可以被光环影响
    static constexpr int CritRate = 9; // 物品暴击率（百分比），可以被光环影响
    static constexpr int CritDamage = 10; // 物品暴击伤害（百分比），可以被光环影响
    static constexpr int Multicast = 11; // 物品多重释放次数，可以被光环影响
    static constexpr int AmmoCap = 12; // 物品最大弹药量，可以被光环影响
    static constexpr int AmmoRemaining = 13; // 物品剩余弹药量
    static constexpr int Charge = 14; // 物品充能（毫秒），可以被光环影响
    static constexpr int ChargeTargetCount = 15; // 物品充能目标数量，可以被光环影响
    static constexpr int Haste = 16; // 物品加速（毫秒），可以被光环影响
    static constexpr int HasteTargetCount = 17; // 物品加速目标数量，可以被光环影响
    static constexpr int Slow = 18; // 物品减速（毫秒），可以被光环影响
    static constexpr int SlowTargetCount = 19; // 物品减速目标数量，可以被光环影响
    static constexpr int PercentSlowReduction = 20; // 物品减速抗性百分比，可以被光环影响
    static constexpr int Freeze = 21; // 物品冻结（毫秒），可以被光环影响
    static constexpr int FreezeTargetCount = 22; // 物品冻结目标数量，可以被光环影响
    static constexpr int PercentFreezeReduction = 23; // 物品冻结抗性百分比，可以被光环影响
    static constexpr int Reload = 24; // 物品装填，可以被光环影响
    static constexpr int ReloadTargetCount = 25; // 物品装填目标数量，可以被光环影响
    static constexpr int DestroyTargetCount = 26; // 物品摧毁目标数量，可以被光环影响
    static constexpr int RepairTargetCount = 27; // 物品修复目标数量，可以被光环影响
    static constexpr int InFlight = 28; // 物品是否在飞行中
    static constexpr int Destroyed = 29; // 物品是否已摧毁
    static constexpr int Cooldown = 30; // 物品冷却时间（毫秒）
    static constexpr int ChargedTime = 31; // 物品已被充能的时间（毫秒）
    static constexpr int FreezeRemaining = 32; // 物品冻结剩余时间（毫秒）
    static constexpr int SlowRemaining = 33; // 物品减速剩余时间（毫秒）
    static constexpr int HasteRemaining = 34; // 物品加速剩余时间（毫秒）
    static constexpr int Value = 35; // 物品价值，可以被光环影响
    static constexpr int Tags = 36; // 物品标签（位图），可以被光环影响
    static constexpr int DerivedTags = 37; // 物品衍生标签（位图）
    static constexpr int Size = 38; // 物品大小
    static constexpr int Quest = 39; // 物品任务进度（位图）
    static constexpr int LifeSteal = 40; // 物品生命窃取（百分比），可以被光环影响
    static constexpr int ModifyAttributeTargetCount = 41; // 物品修改属性目标数量，可以被光环影响
    static constexpr int Hero = 42; // 物品英雄
    static constexpr int MinTier = 43; // 物品最小等级
    static constexpr int Tier = 44; // 物品当前等级
    static constexpr int CooldownReduction = 45; // 物品冷却时间减少（固定值），可以被光环影响
    static constexpr int CooldownReductionPercent = 46; // 物品冷却时间减少（百分比），可以被光环影响
    
    // 自定义属性
    static constexpr int Custom_0 = 47; // 自定义属性0，可以被光环影响
    static constexpr int Custom_1 = 48; // 自定义属性1，可以被光环影响
    static constexpr int Custom_2 = 49; // 自定义属性2，可以被光环影响
    static constexpr int Custom_3 = 50; // 自定义属性3，可以被光环影响

    // 物品状态属性数量
    static constexpr int Count = Custom_3 + 1; // 物品状态属性数量
};

template <int key>
constexpr bool IsAuraEffect = (
    key == ItemKey::Damage ||
    key == ItemKey::Burn ||
    key == ItemKey::Poison ||
    key == ItemKey::Shield ||
    key == ItemKey::Heal ||
    key == ItemKey::Regen ||
    key == ItemKey::CritRate ||
    key == ItemKey::CritDamage ||
    key == ItemKey::Multicast ||
    key == ItemKey::AmmoCap ||
    key == ItemKey::Charge ||
    key == ItemKey::ChargeTargetCount ||
    key == ItemKey::Haste ||
    key == ItemKey::HasteTargetCount ||
    key == ItemKey::Slow ||
    key == ItemKey::SlowTargetCount ||
    key == ItemKey::PercentSlowReduction ||
    key == ItemKey::Freeze ||
    key == ItemKey::FreezeTargetCount ||
    key == ItemKey::PercentFreezeReduction ||
    key == ItemKey::Reload ||
    key == ItemKey::ReloadTargetCount ||
    key == ItemKey::DestroyTargetCount ||
    key == ItemKey::RepairTargetCount ||
    key == ItemKey::Value ||
    key == ItemKey::LifeSteal ||
    key == ItemKey::ModifyAttributeTargetCount ||
    key == ItemKey::CooldownReduction ||
    key == ItemKey::CooldownReductionPercent ||
    key == ItemKey::Custom_0 ||
    key == ItemKey::Custom_1 ||
    key == ItemKey::Custom_2 ||
    key == ItemKey::Custom_3
);

}  // namespace bazaararena::core