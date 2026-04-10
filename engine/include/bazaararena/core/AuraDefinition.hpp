#pragma once

#include <bazaararena/formula/Formula.hpp>

namespace bazaararena::core {

// 光环定义类
class AuraDefinition {
public:
    int attribute = 0;
    const formula::Formula condition = formula::True;
    const formula::Formula value = formula::True;
    bool percent = false;
};

} // namespace bazaararena::core