#pragma once

#include <bazaararena/gdf/BattleEvaluator.hpp>
#include <bazaararena/gdf/DeckRep.hpp>

#include <string>
#include <vector>

namespace bazaararena::gdf_pa {

struct QualityLeagueStanding {
    int final_rank = 0;
    std::string signature;
    /// 循环赛总局分：在 BO_N 中每局胜 +1、平 +0.5（因此每场对决两边局分之和恒为 N），最终用总局分排序。
    double total_points = 0;
    int matchup_wins = 0;
    int matchup_draws = 0;
    int matchup_losses = 0;
};

/// 全两两 BoN（与 BattleEvaluator 的 best_of 一致，通常为 5）。`wins[i]` 为 deck i 赢下的系列数。
void RunBoNRoundRobin(const std::vector<bazaararena::gdf::DeckRep>& decks, bazaararena::gdf::BattleEvaluator& eval, std::vector<int>& wins_out);

/// `quality_peel_sources` 卡组循环赛：每对打 `series_games` 局，按系列赛总点数比胜负；总点数相同则本场各记 0.5 循环赛分（例如 10:10）。
/// 若 `symm_delta_out` 非空，则写入 n×n 矩阵：`(*symm_delta_out)[i][j]` 为 i 对 j 的系列赛「点差」`points_i - points_j`（i 与 j 对阵时）。
void RunPeelSourcesRoundRobin(const std::vector<bazaararena::gdf::DeckRep>& decks, bazaararena::gdf::BattleEvaluator& eval, int series_games,
    std::vector<double>& total_points_out, std::vector<int>& matchup_wins_out, std::vector<int>& matchup_draws_out,
    std::vector<int>& matchup_losses_out, std::vector<std::vector<double>>* symm_delta_out = nullptr);

/// 严格胜负图（i 胜 j 当且仅当 delta[i][j]>0）上迭代删去「对任意存活对手均无胜场」的点，直至稳定；若一轮内全员均无胜场（例如全平局）则停止。
[[nodiscard]] std::vector<int> FilterWinlessStrictIterative(const std::vector<std::vector<double>>& symm_delta);

/// 仅在 `subset_idx` 子集上，按 `symm_delta` 重算循环赛积分与胜负平统计（下标为子集内顺序）。
void AggregateLeagueFromDeltaSubset(const std::vector<int>& subset_idx, const std::vector<std::vector<double>>& symm_delta, int series_games,
    std::vector<double>& total_points_out, std::vector<int>& matchup_wins_out, std::vector<int>& matchup_draws_out,
    std::vector<int>& matchup_losses_out);

/// 按积分降序、签名升序赋 `final_rank`（积分相同则名次并列，下一档跳号）。
[[nodiscard]] std::vector<QualityLeagueStanding> BuildQualityLeagueStandings(const std::vector<bazaararena::gdf::DeckRep>& decks,
    const std::vector<double>& total_points, const std::vector<int>& matchup_wins, const std::vector<int>& matchup_draws,
    const std::vector<int>& matchup_losses);

[[nodiscard]] bool WriteQualityRankingCsv(const std::string& path, const std::vector<QualityLeagueStanding>& rows, std::string& err);

}  // namespace bazaararena::gdf_pa
