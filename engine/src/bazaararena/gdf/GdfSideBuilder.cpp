#include <bazaararena/gdf/GdfSideBuilder.hpp>

#include <bazaararena/core/ItemTier.hpp>
#include <bazaararena/data/ItemDatabase.hpp>

namespace bazaararena::gdf {
namespace core = bazaararena::core;

std::string TierStringFromCombatTier(int combat_tier) {
    using namespace bazaararena::core;
    if (combat_tier == ItemTier::Bronze) return "bronze";
    if (combat_tier == ItemTier::Silver) return "silver";
    if (combat_tier == ItemTier::Gold) return "gold";
    if (combat_tier == ItemTier::Diamond) return "diamond";
    return "bronze";
}

bool BuildSideSpecFromDeck(const DeckRep& rep, int player_level, int combat_tier, bazaararena::io::SideSpec& out, std::string& error) {
    error.clear();
    out = {};
    out.level = player_level;
    out.sideId = 0;
    const std::string tier = TierStringFromCombatTier(combat_tier);
    for (const auto& name : rep.item_names) {
        ResolvedItem ri = ResolveItemAlias(name);
        if (!bazaararena::data::GetItemByKey(ri.db_key)) {
            error = "unknown item key: " + ri.db_key;
            return false;
        }
        bazaararena::io::ItemSpec is;
        is.key = std::move(ri.db_key);
        is.tier = tier;
        if (ri.quest_index.has_value() && *ri.quest_index > 0 && *ri.quest_index <= 30) {
            is.quest = (1 << (*ri.quest_index - 1));
        }
        out.items.push_back(std::move(is));
    }
    return true;
}

}  // namespace bazaararena::gdf
