#pragma once

namespace bazaararena::core {

// 触发器类型
// 在每个触发器触发时，会填入上下文信息，除了触发器类型外，还有以下信息：
// - 触发器来源（Source）
// - 触发器目标（Target）
class Trigger final {
public:
    static constexpr int Cast = 0; // 物品被使用时触发（Source = Target = 使用者；这里和 UseItem 的区别是，这里是物品被施放时实际触发的主动效果，可以暴击，而 UseItem 是物品被使用时触发的被动效果）
    static constexpr int UseItem = 1; // 物品被使用时触发（Source = Target = 使用者）
    static constexpr int BattleStart = 2; // 战斗开始时触发（Source = Target = null）
    static constexpr int EveryFrame = 3; // 每个帧都触发（Source = Target = null）
    
    static constexpr int Shield = 4; // 任意物品施加护盾时触发（Source = 护盾来源，Target = 护盾目标阵营的第 1 个物品）
    static constexpr int Damage = 5; // 任意物品造成伤害时触发（Source = 伤害来源，Target = 伤害目标阵营的第 1 个物品）
    static constexpr int Ammo = 6; // 任意物品消耗弹药时触发（Source = Target = 消耗弹药的物品）
    static constexpr int Burn = 7; // 任意物品施加灼烧时触发（Source = 灼烧来源，Target = 灼烧目标阵营的第 1 个物品）
    static constexpr int Poison = 8; // 任意物品施加剧毒时触发（Source = 剧毒来源，Target = 剧毒目标阵营的第 1 个物品）
    static constexpr int Heal = 9; // 任意物品施加治疗时触发（Source = 治疗来源，Target = 治疗目标阵营的第 1 个物品）
    static constexpr int Regen = 10; // 任意物品施加生命再生时触发（Source = 生命再生来源，Target = 生命再生目标阵营的第 1 个物品）
    static constexpr int Crit = 11; // 任意物品造成暴击时触发（Source = Target = 造成暴击的物品）
    static constexpr int Destroy = 12; // 任意物品施加摧毁时触发（Source = 摧毁来源，Target = 摧毁目标）；注意：在将目标标记为 Destroyed 之前调用
    static constexpr int Repair = 13; // 任意物品施加修复时触发（Source = 修复来源，Target = 修复目标）
    static constexpr int Freeze = 14; // 任意物品施加冻结时触发（Source = 冻结来源，Target = 冻结目标）
    static constexpr int Slow = 15; // 任意物品施加减速时触发（Source = 减速来源，Target = 减速目标）
    static constexpr int Haste = 16; // 任意物品施加加速时触发（Source = 加速来源，Target = 加速目标）
    static constexpr int Reload = 17; // 任意物品装填时触发（Source = 装填来源，Target = 装填目标）
    static constexpr int Charge = 18; // 任意物品充能时触发（Source = 充能来源，Target = 充能目标）
    static constexpr int StartFlying = 19; // 任意物品开始飞行时触发（Source = 开始飞行来源，Target = 开始飞行目标）
    static constexpr int StopFlying = 20; // 任意物品停止飞行时触发（Source = 停止飞行来源，Target = 停止飞行目标）
    
    static constexpr int AboutToLose = 21; // 即将落败时触发（Source = Target = null）
    static constexpr int CritRateIncreased = 22; // 暴击率提高时触发（Source = 暴击率提高来源，Target = 暴击率提高目标）

    static constexpr int Count = 23; // 触发器数量
};

}