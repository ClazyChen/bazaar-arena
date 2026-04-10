#pragma once

#include <bazaararena/formula/Formula.hpp>

namespace bazaararena::formula {

// 计算百分比（向下取整，最小为 1）
constexpr int PercentFloor(int value, int percent) {
    if (value <= 0 || percent <= 0) return 0;
    int result = value * percent / 100;
    return result < 1 ? 1 : result;
}

// 计算百分比（向下取整，最小为 1）
template<Formula a, int pct>
constexpr Formula Percent = [](const BattleContext& ctx) -> int {
    return PercentFloor(a(ctx), pct);
};

} // namespace bazaararena::formula