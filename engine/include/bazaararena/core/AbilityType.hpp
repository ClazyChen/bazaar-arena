#pragma once

namespace bazaararena::core {

// 能力类型
class AbilityType final {
public:
    static constexpr int None = 0; // 无
    static constexpr int Damage = 1; // 伤害
    static constexpr int Shield = 2; // 护盾
    static constexpr int Heal = 3; // 治疗
    static constexpr int Burn = 4; // 灼烧
    static constexpr int Poison = 5; // 剧毒
    static constexpr int Charge = 6; // 充能
    static constexpr int Haste = 7; // 加速
    static constexpr int Slow = 8; // 减速
    static constexpr int Freeze = 9; // 冻结
    static constexpr int Reload = 10; // 装填
    static constexpr int Repair = 11; // 修复
    static constexpr int Destroy = 12; // 摧毁
    static constexpr int AddAttribute = 13; // 添加属性
    static constexpr int ReduceAttribute = 14; // 减少属性
    static constexpr int GainGold = 15; // 获得金币
    static constexpr int Regen = 16; // 生命再生
    static constexpr int Resistance = 17; // 抗性
    static constexpr int PoisonSelf = 18; // 自身施加剧毒
    static constexpr int Cast = 19; // 立刻施放

    // 能力类型数量
    static constexpr int Count = 20; // 能力类型数量
};

}  // namespace bazaararena::core