#pragma once

#include <bazaararena/core/AbilityPriority.hpp>
#include <bazaararena/core/AbilityType.hpp>
#include <bazaararena/core/Trigger.hpp>
#include <bazaararena/formula/Formula.hpp>
#include <array>

namespace bazaararena::core {

// 前置声明
class Simulator;

// 能力定义类
class AbilityDefinition  {
public:
    static constexpr size_t MaxTriggerEntries = 4;

    int priority = AbilityPriority::Medium; // 优先级
    int type = AbilityType::None; // 能力类型

    // 触发器和触发条件列表的条目
    struct TriggerEntry {
        int trigger = Trigger::UseItem; // 触发器
        const formula::Formula condition = formula::True; // 触发条件
    };

    std::array<TriggerEntry, MaxTriggerEntries> trigger_entries;
    int trigger_entry_count = 0;

    // 选择目标条件
    const formula::Formula target_condition;

    // 能力对应值的 key
    int value_key = 0;

    // 能力对应目标数量的 key
    int target_count_key = 0;

    // 对于修改属性的能力，属性对应的 key
    int attribute_key = 0;

};

} // namespace bazaararena::core