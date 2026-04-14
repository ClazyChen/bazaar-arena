#include <bazaararena/gdf/ItemPool.hpp>

#include <bazaararena/core/ItemKey.hpp>
#include <bazaararena/core/ItemSize.hpp>
#include <bazaararena/core/ItemTier.hpp>
#include <bazaararena/data/ItemDatabase.hpp>
#include <bazaararena/gdf/GdfLevelRules.hpp>

#include <algorithm>
#include <cctype>

namespace bazaararena::gdf {
namespace core = bazaararena::core;

namespace {

static std::string Lower(std::string_view s) {
    std::string o;
    o.reserve(s.size());
    for (char c : s) o += static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    return o;
}

static bool HeroMatches(std::string_view file_hero, std::string_view pool_lower) {
    if (pool_lower == "all") return true;
    return Lower(file_hero) == std::string(pool_lower);
}

}  // namespace

int ItemPool::SizeOfItem(const core::ItemTemplate* templ) {
    if (!templ) return 0;
    const int sz = templ->attributes[core::ItemTier::Bronze][core::ItemKey::Size];
    if (sz == core::ItemSize::Small) return 1;
    if (sz == core::ItemSize::Medium) return 2;
    if (sz == core::ItemSize::Large) return 3;
    return 0;
}

ItemPool::ItemPool(int player_level, std::string_view pool_hero, const std::unordered_set<std::string>& excluded,
    const std::unordered_map<std::string, std::string>& key_to_hero) {
    const std::string pool_l = Lower(pool_hero);
    auto all = bazaararena::data::GetAllItems();
    for (const auto& rec : all) {
        const std::string_view key = rec.key;
        if (excluded.count(std::string(key))) continue;
        auto it = key_to_hero.find(std::string(key));
        if (it == key_to_hero.end()) continue;
        if (!HeroMatches(it->second, pool_l)) continue;
        const core::ItemTemplate* t = rec.templ;
        const int min_tier = t->attributes[core::ItemTier::Bronze][core::ItemKey::MinTier];
        if (!GdfLevelRules::IsMinTierAllowedInPool(min_tier, player_level)) continue;
        const int sz = SizeOfItem(t);
        if (sz == 1) small_.push_back(std::string(key));
        else if (sz == 2) medium_.push_back(std::string(key));
        else if (sz == 3) large_.push_back(std::string(key));
    }
    auto sort_u8 = [](std::vector<std::string>& v) {
        std::sort(v.begin(), v.end());
    };
    sort_u8(small_);
    sort_u8(medium_);
    sort_u8(large_);

    // 烙刀变体：作为两个不同展示名参与搜索（仅当池为 Vanessa 且存在「烙刀」）
    if (pool_l == "vanessa") {
        auto it = std::find(medium_.begin(), medium_.end(), "烙刀");
        if (it != medium_.end() && excluded.count("烙刀") == 0) {
            medium_.push_back("减速烙刀");
            medium_.push_back("加速烙刀");
            std::sort(medium_.begin(), medium_.end());
        }
    }
}

const std::vector<std::string>& ItemPool::NamesForSize(int size) const {
    if (size == 1) return small_;
    if (size == 2) return medium_;
    if (size == 3) return large_;
    static const std::vector<std::string> kEmpty;
    return kEmpty;
}

}  // namespace bazaararena::gdf
