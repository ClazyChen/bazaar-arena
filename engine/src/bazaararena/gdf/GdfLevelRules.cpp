#include <bazaararena/gdf/GdfLevelRules.hpp>

#include <bazaararena/core/ItemTemplate.hpp>
#include <bazaararena/core/ItemTier.hpp>

namespace bazaararena::gdf {
namespace core = bazaararena::core;

bool GdfLevelRules::IsMinTierAllowedInPool(int template_min_tier, int player_level) {
    using namespace bazaararena::core;
    switch (template_min_tier) {
        case ItemTier::Bronze:
            return true;
        case ItemTier::Silver:
            return player_level >= 5;
        case ItemTier::Gold:
            return player_level >= 8;
        case ItemTier::Diamond:
            return player_level >= 11;
        default:
            return false;
    }
}

int GdfLevelRules::CombatTier(int player_level) {
    using namespace bazaararena::core;
    if (player_level <= 4) return ItemTier::Bronze;
    if (player_level <= 7) return ItemTier::Silver;
    if (player_level <= 10) return ItemTier::Gold;
    return ItemTier::Diamond;
}

int GdfLevelRules::MaxSlotsForLevel(int level) {
    switch (level) {
        case 1:
            return 4;
        case 2:
            return 6;
        case 3:
            return 8;
        default:
            return 10;
    }
}

int GdfLevelRules::ComputeOverridableValue(const core::ItemTemplate& source, int item_key, int player_level) {
    const int bronze_val = source.attributes[core::ItemTier::Bronze][item_key];
    const int silver_val = source.attributes[core::ItemTier::Silver][item_key];
    const int gold_val = source.attributes[core::ItemTier::Gold][item_key];
    const int diamond_val = source.attributes[core::ItemTier::Diamond][item_key];

    if (player_level <= 1) return bronze_val / 2;

    switch (player_level) {
        case 2:
            return bronze_val / 2;
        case 3:
            return bronze_val;
        case 4:
        case 5:
            return (bronze_val + silver_val) / 2;
        case 6:
            return silver_val;
        case 7:
        case 8:
            return (silver_val + gold_val) / 2;
        case 9:
            return gold_val;
        case 10:
        case 11:
            return (gold_val + diamond_val) / 2;
        case 12:
            return diamond_val;
        default:
            return diamond_val + (player_level - 12) * (diamond_val - gold_val) / 2;
    }
}

}  // namespace bazaararena::gdf
