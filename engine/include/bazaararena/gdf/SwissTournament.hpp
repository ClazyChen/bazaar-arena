#pragma once

#include <bazaararena/gdf/BattleEvaluator.hpp>
#include <bazaararena/gdf/CandidateState.hpp>

#include <random>
#include <unordered_set>
#include <vector>

namespace bazaararena::gdf {

struct SwissTournament {
    static std::vector<CandidateState> RunSwissAndPickTop(std::vector<CandidateState> candidates, int rounds, int top_count,
        BattleEvaluator& evaluator, std::mt19937& main_rng);

    /// 循环赛积分 + 可选成对对照锚点边际（λ>0 时 compute_anchor=true）。
    static void RunRoundRobinAndAnchorMargin(std::vector<CandidateState>& candidates, int games_per_pair, bool compute_anchor,
        const std::unordered_set<std::string>& seed_names, BattleEvaluator& evaluator);

    /// λ/μ 贪心选 TopK（MMR）。
    static std::vector<CandidateState> GreedyPickByObjective(std::vector<CandidateState> candidates, int top_k, double lambda_anchor,
        double mu_diversity, bool diversity_exclude_seeds, const std::unordered_set<std::string>& seed_names, std::mt19937& rng);

private:
    static int RemoveSwissImpossible(std::vector<CandidateState*>& active, int top_count, int remaining_rounds);
    static double JaccardSimilarity(const std::vector<std::string>& a, const std::vector<std::string>& b);
    static std::vector<std::string> FilterSeedsCopy(const std::vector<std::string>& items, const std::unordered_set<std::string>& seeds);
};

}  // namespace bazaararena::gdf
