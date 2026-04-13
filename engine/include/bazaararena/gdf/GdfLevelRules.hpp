#pragma once

#include <bazaararena/core/ItemTemplate.hpp>
#include <bazaararena/core/ItemTier.hpp>

namespace bazaararena::gdf {

/// Greedy 专用等级规则（与 GUI Deck 门槛不同）。
struct GdfLevelRules {
    static constexpr int MinPlayerLevel = 2;
    static constexpr int MaxPlayerLevel = 20;

    static bool IsMinTierAllowedInPool(int template_min_tier, int player_level);

    /// 战斗扁平化使用的物品档位。
    static int CombatTier(int player_level);

    /// 等级 → 卡组总槽位上限（与 legacy Deck.MaxSlotsForLevel 一致）。
    static int MaxSlotsForLevel(int level);

    /// 与 legacy `GreedyLevelRules.ComputeOverridableValue` 一致：对模板某 `ItemKey` 按玩家等级缩放。
    static int ComputeOverridableValue(const bazaararena::core::ItemTemplate& source, int item_key, int player_level);
};

}  // namespace bazaararena::gdf
