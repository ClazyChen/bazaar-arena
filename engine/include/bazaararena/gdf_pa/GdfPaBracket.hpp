#pragma once

#include <bazaararena/gdf/BattleEvaluator.hpp>
#include <bazaararena/gdf/DeckRep.hpp>

#include <vector>

namespace bazaararena::gdf_pa {

/// 与 `double-elimination@1.0.0` npm 结构一致的赛程图执行；簇冠军经「胜者组冠军 vs 败者组冠军」BoN，败者组胜则追加一局（reset）。
/// 参与者为 `decks` 下标 0..m-1，不足 `nextPow2(m)` 以轮空补位。
[[nodiscard]] int RunClusterDoubleElimination(const std::vector<bazaararena::gdf::DeckRep>& decks, bazaararena::gdf::BattleEvaluator& eval);

/// m≤3：BoN 小循环积分；m>3：双败 + 总决赛。
[[nodiscard]] int RunClusterRepresentative(const std::vector<bazaararena::gdf::DeckRep>& decks, bazaararena::gdf::BattleEvaluator& eval);

}  // namespace bazaararena::gdf_pa
