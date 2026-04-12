#pragma once

#include <bazaararena/gdf/BattleEvaluator.hpp>
#include <bazaararena/gdf/CandidateState.hpp>
#include <bazaararena/gdf/DeckRep.hpp>
#include <bazaararena/gdf/ItemPool.hpp>

#include <functional>
#include <optional>
#include <random>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace bazaararena::gdf {

struct GreedyConfig {
    int player_level = 2;
    int top_k = 10;
    int top_multiplier = 3;
    int best_of = 5;
    int workers = 0;
    std::optional<int> seed;
    double lambda_anchor = 0;
    double mu_diversity = 0;
    bool diversity_exclude_seeds = false;
};

class GreedySearcher {
public:
    GreedySearcher(const ItemPool& pool, BattleEvaluator& evaluator, std::mt19937& rng, const GreedyConfig& config,
        const std::unordered_set<std::string>& seed_items);

    /// 返回每个 size -> 该档最终 Top 候选（已含循环赛 / 锚点 / 多样性处理）。
    std::unordered_map<int, std::vector<CandidateState>> Run(const std::vector<std::string>& seed_ordered_items,
        const std::function<void(int size, const std::vector<CandidateState>& top)>& on_size_completed = {});

private:
    const ItemPool& pool_;
    BattleEvaluator& evaluator_;
    std::mt19937& rng_;
    GreedyConfig config_;
    std::unordered_set<std::string> seed_items_;

    std::vector<DeckRep> BuildInsertionReps(const DeckRep& prev, const std::string& new_item);

    std::vector<CandidateState> ResolveConflictBuckets(std::unordered_map<std::string, std::vector<CandidateState>>& buckets);

    std::vector<DeckRep> RunDeckKnockoutMany(std::vector<std::vector<DeckRep>> sources);

    std::vector<CandidateState> RunCandidateKnockoutMany(std::vector<std::vector<CandidateState>> sources);

    std::vector<CandidateState> ResolveFinalTopTieByPlayoff(std::vector<CandidateState> stage2);
};

}  // namespace bazaararena::gdf
