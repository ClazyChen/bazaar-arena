#pragma once

#include <bazaararena/core/SideState.hpp>
#include <bazaararena/gdf/DeckRep.hpp>
#include <bazaararena/gdf/GdfRunTiming.hpp>

namespace bazaararena::gdf {
class GdfItemPrototypeCache;
}

#include <condition_variable>
#include <cstdint>
#include <functional>
#include <mutex>
#include <queue>
#include <random>
#include <shared_mutex>
#include <string>
#include <thread>
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
    /// `timing` 可选；启用时统计 `ToSide`、批对战与并行批主线程等待（见 `GdfRunTiming`）。
    /// `item_prototypes` 非空时 `ToSide` 使用 YAML overridable + legacy 缩放后的预计算 `ItemState`（仅 GDF）。
    BattleEvaluator(int best_of, int workers, int player_level, GdfRunTiming* timing = nullptr,
        const GdfItemPrototypeCache* item_prototypes = nullptr);
    ~BattleEvaluator();

    BattleEvaluator(const BattleEvaluator&) = delete;
    BattleEvaluator& operator=(const BattleEvaluator&) = delete;

    int PlayBoN(const DeckRep& a, const DeckRep& b);

    std::vector<int> PlayBoNBatch(const std::vector<std::pair<DeckRep, DeckRep>>& pairs);

    std::vector<MatchPoints> PlaySeriesBatch(const std::vector<std::pair<DeckRep, DeckRep>>& pairs, int game_count);

    MatchPoints PlaySeriesPoints(const DeckRep& a, const DeckRep& b, int game_count);

private:
    int best_of_;
    int workers_;
    int player_level_;
    int combat_tier_;
    GdfRunTiming* timing_ = nullptr;
    const GdfItemPrototypeCache* item_prototypes_ = nullptr;

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

    /// `workers_ > 1` 时懒启动；每条线程内 `thread_local Simulator` 跑对战，主线程只投递 chunk 任务。
    void ensure_sim_worker_pool();
    void pool_worker_loop();
    void run_parallel_chunks(std::vector<std::function<void()>> chunks);

    std::mutex pool_queue_mu_;
    std::condition_variable pool_queue_cv_;
    std::queue<std::function<void()>> pool_queue_;
    bool pool_shutdown_ = false;
    bool pool_workers_started_ = false;
    std::mutex pool_start_mu_;
    std::vector<std::thread> pool_threads_;
};

}  // namespace bazaararena::gdf
