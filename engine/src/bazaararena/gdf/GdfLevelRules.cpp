#include <bazaararena/gdf/GdfLevelRules.hpp>

namespace bazaararena::gdf {

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

}  // namespace bazaararena::gdf
