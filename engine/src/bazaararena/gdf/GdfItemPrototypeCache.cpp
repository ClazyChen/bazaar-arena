#include <bazaararena/gdf/GdfItemPrototypeCache.hpp>

#include <bazaararena/gdf/DeckRep.hpp>
#include <bazaararena/core/HpTable.hpp>
#include <bazaararena/core/ItemKey.hpp>
#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/data/ItemDatabase.hpp>
#include <bazaararena/gdf/GdfLevelRules.hpp>
#include <bazaararena/gdf/ItemPool.hpp>

#include <stdexcept>
#include <string>

namespace bazaararena::gdf {
namespace core = bazaararena::core;

namespace {

static int ComputeMakQuestOverride(std::string_view db_key, int player_level) {
    // Mak 的部分物品在 legacy 扁平化中会按等级覆写 Quest。
    // 规则来自用户说明：
    // - 时间之砂、永恒火炬、生命导体、腐朽圣像：L2=0，L3-4=1，L5-7=3，L8+=7
    // - 空白石碑：L2-4=0，L5+=31
    if (db_key == "时间之砂" || db_key == "永恒火炬" || db_key == "生命导体" || db_key == "腐朽圣像") {
        if (player_level <= 2) return 0;
        if (player_level <= 4) return 1;
        if (player_level <= 7) return 3;
        return 7;
    }
    if (db_key == "空白石碑") {
        if (player_level <= 4) return 0;
        return 31;
    }
    return -1;
}

}  // namespace

GdfItemPrototypeCache::GdfItemPrototypeCache(const ItemPool& pool, int player_level) {
    const int combat_tier = GdfLevelRules::CombatTier(player_level);
    std::vector<std::string> names;
    names.reserve(pool.SmallNames().size() + pool.MediumNames().size() + pool.LargeNames().size());
    for (const auto& v : {pool.SmallNames(), pool.MediumNames(), pool.LargeNames()}) {
        for (const auto& s : v) names.push_back(s);
    }
    for (const std::string& name : names) {
        const ResolvedItem ri = ResolveItemAlias(name);
        const core::ItemTemplate* t = bazaararena::data::GetItemByKey(ri.db_key);
        if (!t) {
            throw std::runtime_error("GdfItemPrototypeCache: unknown item key: " + ri.db_key);
        }
        core::ItemState st;
        st.templ = const_cast<core::ItemTemplate*>(t);
        st.attrs = t->attributes[combat_tier];
        for (int k = 0; k < t->overridable_key_count; ++k) {
            const int ik = t->overridable_keys[static_cast<size_t>(k)];
            st.attrs[ik] = GdfLevelRules::ComputeOverridableValue(*t, ik, player_level);
        }
        st.attrs[core::ItemKey::Tier] = combat_tier;
        if (ri.quest_index.has_value()) {
            const int qi = *ri.quest_index;
            if (qi > 0 && qi <= 30) {
                st.attrs[core::ItemKey::Quest] = (1 << (qi - 1));
            }
        }
        {
            const int q = ComputeMakQuestOverride(ri.db_key, player_level);
            if (q >= 0) st.attrs[core::ItemKey::Quest] = q;
        }
        const int ammo_cap = st.attrs[core::ItemKey::AmmoCap];
        if (ammo_cap > 0) {
            st.attrs[core::ItemKey::AmmoRemaining] = ammo_cap;
        }
        protos_.emplace(name, std::move(st));
    }
}

const core::ItemState& GdfItemPrototypeCache::At(std::string_view display_name) const {
    const auto it = protos_.find(std::string(display_name));
    if (it == protos_.end()) {
        throw std::runtime_error("GdfItemPrototypeCache: missing prototype for '" + std::string(display_name) + "'");
    }
    return it->second;
}

core::SideState GdfItemPrototypeCache::BuildSide(const DeckRep& rep, int player_level, int side_id) const {
    if (player_level < 1 || player_level > core::HpTable::MaxLevel) {
        throw std::runtime_error("GdfItemPrototypeCache::BuildSide: player_level out of range");
    }
    if (rep.item_names.size() > static_cast<size_t>(core::SideState::MaxItems)) {
        throw std::runtime_error("GdfItemPrototypeCache::BuildSide: too many items");
    }

    core::SideState out{};
    const int hp = core::HpTable::ByLevel[player_level];
    out.attrs.fill(0);
    out.attrs[core::SideKey::Id] = side_id;
    out.attrs[core::SideKey::MaxHp] = hp;
    out.attrs[core::SideKey::Hp] = hp;
    out.attrs[core::SideKey::Shield] = 0;
    out.attrs[core::SideKey::Burn] = 0;
    out.attrs[core::SideKey::Poison] = 0;
    out.attrs[core::SideKey::Regen] = 0;
    out.attrs[core::SideKey::Gold] = 0;
    out.attrs[core::SideKey::Income] = 7;
    out.attrs[core::SideKey::ItemCount] = static_cast<int>(rep.item_names.size());

    for (size_t i = 0; i < rep.item_names.size(); ++i) {
        const core::ItemState& proto = At(rep.item_names[i]);
        out.items[i].templ = proto.templ;
        out.items[i].attrs = proto.attrs;
        out.items[i].attrs[core::ItemKey::SideIndex] = side_id;
        out.items[i].attrs[core::ItemKey::ItemIndex] = static_cast<int>(i);
    }
    if (!rep.item_names.empty()) {
        out.items[0].attrs[core::ItemKey::Resistance] = 0;
    }
    return out;
}

}  // namespace bazaararena::gdf
