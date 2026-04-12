#pragma once

#include <bazaararena/core/SideState.hpp>
#include <bazaararena/gdf/DeckRep.hpp>

#include <cstdint>
#include <mutex>
#include <random>
#include <shared_mutex>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

namespace bazaararena::gdf {

struct MatchPoints {
    double a = 0;
    double b = 0;
};

/// 对战评估器：追求 GDF 极致性能，**不保证**批内/批间 RNG 可复现。
class BattleEvaluator {
public:
    BattleEvaluator(int best_of, int workers, int player_level);

    int PlayBoN(const DeckRep& a, const DeckRep& b);

    std::vector<int> PlayBoNBatch(const std::vector<std::pair<DeckRep, DeckRep>>& pairs);

    std::vector<MatchPoints> PlaySeriesBatch(const std::vector<std::pair<DeckRep, DeckRep>>& pairs, int game_count);

    MatchPoints PlaySeriesPoints(const DeckRep& a, const DeckRep& b, int game_count);

private:
    int best_of_;
    int workers_;
    int player_level_;
    int combat_tier_;

    mutable std::shared_mutex deck_cache_mu_;
    std::unordered_map<std::string, bazaararena::core::SideState> deck_cache_;

    /// 在持锁期间从缓存拷贝，避免调用方持有悬空引用（map 重哈希或其它线程写入）。
    bazaararena::core::SideState ToSide(const DeckRep& rep);

    /// game_word：驱动 swap 与 sim.rng 的低位熵（不要求密码学随机）。
    int PlaySingleGameForSeries(const DeckRep& a, const DeckRep& b, unsigned game_word);
    int PlaySingleGameForBoN(const DeckRep& a, const DeckRep& b, unsigned game_word);

    MatchPoints PlaySeriesPointsForPair(const DeckRep& a, const DeckRep& b, int game_count, unsigned pair_seed);

    int RunBoNStream(const DeckRep& a, const DeckRep& b, unsigned stream_seed);

    static std::vector<size_t> CreateShuffledOrder(size_t count, std::mt19937& rng);
};

}  // namespace bazaararena::gdf
