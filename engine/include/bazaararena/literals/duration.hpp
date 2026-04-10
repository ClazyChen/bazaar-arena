#pragma once

namespace bazaararena::inline literals::inline duration_literals {

/// 将「秒」字面量转为毫秒（整数）。例如 `1_s` → 1000。
[[nodiscard]] constexpr int operator""_s(unsigned long long sec) noexcept {
    return static_cast<int>(sec * 1000ULL);
}

/// 将「秒」字面量转为毫秒（整数）。例如 `0.25_s` → 250。
[[nodiscard]] constexpr int operator""_s(long double sec) noexcept {
    return static_cast<int>(sec * 1000.0L);
}

}  // namespace bazaararena::inline literals::inline duration_literals
