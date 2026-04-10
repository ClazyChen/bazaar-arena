#pragma once

namespace bazaararena::core {

// 物品所具有的**衍生**标签，这部分标签在注册到数据库的时候自动填充
class DerivedTag final {
public:
    static constexpr int Shield = 1 << 0; // 护盾
    static constexpr int Damage = 1 << 1; // 伤害
    static constexpr int Ammo = 1 << 2; // 弹药
    static constexpr int Burn = 1 << 3; // 灼烧
    static constexpr int Poison = 1 << 4; // 剧毒
    static constexpr int Heal = 1 << 5; // 治疗
    static constexpr int Regen = 1 << 6; // 生命再生
    static constexpr int Crit = 1 << 7; // **是否可以**暴击
    static constexpr int Cooldown = 1 << 8; // **是否具有**冷却时间
    static constexpr int Charge = 1 << 9; // 充能
    static constexpr int Freeze = 1 << 10; // 冻结
    static constexpr int Slow = 1 << 11; // 减速
    static constexpr int Haste = 1 << 12; // 加速
    static constexpr int Reload = 1 << 13; // 装填
    static constexpr int Destroy = 1 << 14; // 摧毁
    static constexpr int Repair = 1 << 15; // 修复
    static constexpr int Quest = 1 << 16; // 任务

    // 衍生标签数量
    static constexpr int Count = 17; // 衍生标签数量
};

}  // namespace bazaararena::core