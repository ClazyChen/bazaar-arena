#pragma once

#include <bazaararena/core/ItemTemplate.hpp>

namespace bazaararena::core {

// 物品在战斗中的运行时状态
class ItemState {

public:
    core::ItemTemplate* templ = nullptr; // 物品模板
    core::ItemTemplate::AttributesTable attrs; // 物品属性
};

}