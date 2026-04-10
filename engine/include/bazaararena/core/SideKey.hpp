#pragma once

namespace bazaararena::core {

class SideKey final {
public:
    static constexpr int Id = 0; // 阵营 ID
    static constexpr int MaxHp = 1; // 阵营最大生命值
    static constexpr int Hp = 2; // 阵营当前生命值
    static constexpr int Shield = 3; // 阵营护盾
    static constexpr int Burn = 4; // 阵营灼烧
    static constexpr int Poison = 5; // 阵营剧毒
    static constexpr int Regen = 6; // 阵营生命再生
    static constexpr int Gold = 7; // 阵营金币
    static constexpr int Income = 8; // 阵营收入
    static constexpr int Resistance = 9; // 阵营抗性
    static constexpr int ItemCount = 10; // 阵营中的物品数量

    // 阵营状态属性数量
    static constexpr int Count = ItemCount + 1; // 阵营状态属性数量
};

}  // namespace bazaararena::core