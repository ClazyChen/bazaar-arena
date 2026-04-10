#pragma once

namespace bazaararena::core {

// 物品所具有的**原生**标签
class Tag final {
public:
    static constexpr int Weapon = 1 << 0; // 武器
    static constexpr int Tool = 1 << 1; // 工具
    static constexpr int Apparel = 1 << 2; // 服装
    static constexpr int Friend = 1 << 3; // 伙伴
    static constexpr int Food = 1 << 4; // 食物
    static constexpr int Tech = 1 << 5; // 科技
    static constexpr int Property = 1 << 6; // 地产
    static constexpr int Vehicle = 1 << 7; // 载具
    static constexpr int Relic = 1 << 8; // 遗物
    static constexpr int Dragon = 1 << 9; // 巨龙
    static constexpr int Drone = 1 << 10; // 无人机
    static constexpr int Toy = 1 << 11; // 玩具
    static constexpr int Aquatic = 1 << 12; // 水系
    static constexpr int Ray = 1 << 13; // 射线
    static constexpr int Trap = 1 << 14; // 陷阱
    static constexpr int Loot = 1 << 15; // 战利品
    static constexpr int Reagent = 1 << 16; // 原料
    static constexpr int Potion = 1 << 17; // 药水
    static constexpr int Core = 1 << 18; // 核心
    static constexpr int Dinosaur = 1 << 19; // 恐龙
    
    // 标签数量
    static constexpr int Count = 20; // 标签数量
};

}  // namespace bazaararena::core