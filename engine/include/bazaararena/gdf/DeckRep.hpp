#pragma once

#include <optional>
#include <string>
#include <unordered_set>
#include <vector>

namespace bazaararena::gdf {

struct DeckRep {
    std::vector<std::string> item_names;

    std::string Signature() const;
};

struct ResolvedItem {
    std::string db_key;
    /// 物品 Quest 位图（ItemKey::Quest）。例如 quest_index=1 => Quest bit0；2 => bit1。
    std::optional<int> quest_index;
};

/// 物品展示名别名解析（例如烙刀变体）。
ResolvedItem ResolveItemAlias(std::string_view display_name);

std::string BuildComboKey(const std::vector<std::string>& item_names);

/// 去掉全部种子物品（按名移除；与种子出现次数一致）。
DeckRep StripSeeds(const DeckRep& rep, const std::unordered_set<std::string>& seed_names);

}  // namespace bazaararena::gdf
