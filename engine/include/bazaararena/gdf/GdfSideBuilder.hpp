#pragma once

#include <bazaararena/gdf/DeckRep.hpp>
#include <bazaararena/io/SimulateJob.hpp>

#include <string>

namespace bazaararena::gdf {

/// 将展示名列表（可含别名）转为 SideSpec；失败时 error 非空。
std::string TierStringFromCombatTier(int combat_tier);

bool BuildSideSpecFromDeck(const DeckRep& rep, int player_level, int combat_tier, bazaararena::io::SideSpec& out, std::string& error);

}  // namespace bazaararena::gdf
