#pragma once

#include <span>
#include <string_view>

namespace bazaararena::core {
class ItemTemplate;
}  // namespace bazaararena::core

namespace bazaararena::data {

struct ItemRecord {
    int id = 0;
    std::string_view key;  // 物品显示名（中文，UTF-8），用于索引
    const core::ItemTemplate* templ = nullptr;
};

std::span<const ItemRecord> GetAllItems();
const core::ItemTemplate* GetItemById(int id);
const core::ItemTemplate* GetItemByKey(std::string_view key);

// 由模板静态推导 DerivedTags（与 BuildCache 中逻辑一致，供战斗内转化等场景使用）
int ComputeDerivedTags(const core::ItemTemplate& templ);

}  // namespace bazaararena::data

