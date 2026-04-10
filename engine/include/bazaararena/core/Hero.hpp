#pragma once

namespace bazaararena::core {

class Hero final {
public:
    static constexpr int Common = 0;
    static constexpr int Vanessa = 1;
    static constexpr int Pygmalien = 2; // 暂时不使用
    static constexpr int Dooley = 3; // 暂时不使用
    static constexpr int Mak = 4;
    static constexpr int Stelle = 5; // 暂时不使用
    static constexpr int Jules = 6; // 暂时不使用
    static constexpr int Karnok = 7; // 暂时不使用

    // 英雄数量
    static constexpr int Count = Karnok + 1;
};

}  // namespace bazaararena::core