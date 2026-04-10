#pragma once

#include <bazaararena/formula/Formula.hpp>

namespace bazaararena::core {

// 光环定义类
class AuraDefinition {
public:
    int attribute = 0;
    formula::Formula condition = formula::True;
    formula::Formula value = formula::True;
    bool percent = false;
};

} // namespace bazaararena::core