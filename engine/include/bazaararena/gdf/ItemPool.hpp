#pragma once

#include <bazaararena/core/ItemTemplate.hpp>

#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace bazaararena::gdf {

/// pool_hero: "Vanessa"|"Mak"|"Common"|"All"（大小写不敏感）
class ItemPool {
public:
    static constexpr const char* kAllHeroes = "All";

    ItemPool(int player_level, std::string_view pool_hero, const std::unordered_set<std::string>& excluded,
        const std::unordered_map<std::string, std::string>& key_to_hero);

    const std::vector<std::string>& SmallNames() const { return small_; }
    const std::vector<std::string>& MediumNames() const { return medium_; }
    const std::vector<std::string>& LargeNames() const { return large_; }

    const std::vector<std::string>& NamesForSize(int size) const;

    /// 物品占用槽位 1/2/3；未知返回 0。
    static int SizeOfItem(const bazaararena::core::ItemTemplate* templ);

private:
    std::vector<std::string> small_;
    std::vector<std::string> medium_;
    std::vector<std::string> large_;
};

}  // namespace bazaararena::gdf
