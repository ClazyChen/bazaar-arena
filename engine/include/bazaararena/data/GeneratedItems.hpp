#pragma once

#include <span>
#include <string_view>

#include "bazaararena/core/ItemTemplate.hpp"

namespace bazaararena::data::generated {

struct GeneratedItem {
    std::string_view key;  // 与 YAML Name 一致（UTF-8），用于数据库索引
    core::ItemTemplate templ;
};

std::span<const GeneratedItem> GetGeneratedItems();

}  // namespace bazaararena::data::generated

