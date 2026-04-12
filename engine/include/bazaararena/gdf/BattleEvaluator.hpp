#pragma once

#include <bazaararena/core/SideState.hpp>
#include <bazaararena/gdf/DeckRep.hpp>

#include <cstdint>
#include <random>
#include <optional>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

namespace bazaararena::gdf {

struct MatchPoints {
    double a = 0;
    double b = 0;
};

class BattleEvaluator {
public:
    BattleEvaluator(int best_of, int workers, int player_level, std::optional<int> deterministic_battle_seed);

    int PlayBoN(const DeckRep& a, const DeckRep& b);

    std::vector<int> PlayBoNBatch(const std::vector<std::pair<DeckRep, DeckRep>>& pairs);

    std::vector<MatchPoints> PlaySeriesBatch(const std::vector<std::pair<DeckRep, DeckRep>>& pairs, int game_count);

    MatchPoints PlaySeriesPoints(const DeckRep& a, const DeckRep& b, int game_count);

private:
    int best_of_;
    int workers_;
    int player_level_;
    int combat_tier_;
    std::optional<int> deterministic_battle_seed_;
    long long parallel_batch_seq_ = 0;

    std::unordered_map<std::string, bazaararena::core::SideState> deck_cache_;

    const bazaararena::core::SideState& ToSide(const DeckRep& rep);

    /// 0=A 胜, 1=B 胜；-1 表示系列赛可计半分平局。
    int PlaySingleGameForSeries(const DeckRep& a, const DeckRep& b, std::optional<unsigned> dedicated_seed);
    int PlaySingleGameForBoN(const DeckRep& a, const DeckRep& b, std::optional<unsigned> dedicated_seed);

    MatchPoints PlaySeriesPointsCore(const DeckRep& a, const DeckRep& b, int game_count, std::optional<unsigned> dedicated_seed);

    /// stream_seed：整局 BO 的随机流种子；nullopt 则每局独立随机。
    int RunBoNStream(const DeckRep& a, const DeckRep& b, std::optional<unsigned> stream_seed);

    static std::vector<size_t> CreateShuffledOrder(size_t count, std::mt19937& rng);
    static int MixSeed(int base, long long batch_id, int pair_index, int salt);
};

}  // namespace bazaararena::gdf
