#pragma once

#include <array>

namespace bazaararena::core {

// 玩家等级到最大生命值的映射
class HpTable final {
public:
    // 本映射覆盖的最高等级（含） 对应下标 MaxLevel
    static constexpr int MaxLevel = 30;

    // 等级 → 最大生命值。下标即等级：ByLevel[1] 为 1 级，ByLevel[MaxLevel] 为最高级；ByLevel[0] 未使用。
    static constexpr std::array<int, MaxLevel + 1> ByLevel = {{
        0,
        300,
        400,
        550,
        750,
        1000,
        1300,
        1650,
        2100,
        2600,
        3200,
        3900,
        4700,
        5600,
        6600,
        7700,
        8900,
        10200,
        11600,
        13100,
        14700,
        16400,
        18200,
        20100,
        22200,
        23400,
        25600,
        26900,
        28250,
        29650,
        31100,
    }};
};

}  // namespace bazaararena::core
