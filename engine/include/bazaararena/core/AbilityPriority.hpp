#pragma once

namespace bazaararena::core {

// 能力优先级
// 从高到低。Immediate 同帧执行，其余下一帧。
class AbilityPriority final {
public:
    static constexpr int Immediate = -1; // 立即执行
    static constexpr int Highest = 0; // 最高优先级
    static constexpr int High = 1; // 高优先级
    static constexpr int Medium = 2; // 中优先级
    static constexpr int Low = 3; // 低优先级
    static constexpr int Lowest = 4; // 最低优先级

    // 能力优先级数量
    static constexpr int Count = 5; // 能力优先级数量
};

}  // namespace bazaararena::core