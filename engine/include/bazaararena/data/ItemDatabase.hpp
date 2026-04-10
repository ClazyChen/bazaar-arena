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

}  // namespace bazaararena::data

