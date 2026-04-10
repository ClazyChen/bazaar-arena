#pragma once

namespace bazaararena::core {

class ItemTier final {
public:
    static constexpr int Bronze = 0;
    static constexpr int Silver = 1;
    static constexpr int Gold = 2;
    static constexpr int Diamond = 3;
    static constexpr int Legendary = 4;

    // 物品等级数量
    static constexpr int Count = Legendary + 1; // 物品等级数量
};

}  // namespace bazaararena::core