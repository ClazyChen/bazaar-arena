#pragma once

#include <string>
#include <array>

#include <bazaararena/core/ItemKey.hpp>
#include <bazaararena/core/ItemTier.hpp>
#include <bazaararena/core/AbilityDefinition.hpp>
#include <bazaararena/core/AuraDefinition.hpp>

namespace bazaararena::core {

// 物品模板，所有物品都使用该模板进行定义
class ItemTemplate {
public:
    // 物品的最大能力数量
    static constexpr size_t MaxAbilities = 8;
    // 物品的最大光环数量
    static constexpr size_t MaxAuras = 8;

    std::string name; // 物品名称，用于 UI 中显示
    std::string desc; // 物品描述，用于 UI 中显示

    // 物品的所有属性都在这里定义
    // Attributes[item_tier][item_key] = value 表示物品在对应等级下的属性值
    using AttributesTable = std::array<int, ItemKey::Count>;
    std::array<AttributesTable, ItemTier::Count> attributes; 

    // 物品的能力列表
    std::array<AbilityDefinition, MaxAbilities> abilities;
    int ability_count = 0;

    // 物品的光环列表
    std::array<AuraDefinition, MaxAuras> auras;
    int aura_count = 0;

};

}