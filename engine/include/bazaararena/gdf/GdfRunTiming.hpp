#pragma once

#include <atomic>
#include <chrono>
#include <cstdint>
#include <ostream>

namespace bazaararena::gdf {

/// 单次 GDF `GreedySearcher::Run` 的耗时统计（线程安全累加，供 `--timing`）。
struct GdfRunTiming {
    std::atomic<uint64_t> expand_and_pick_ns{0};
    std::atomic<uint64_t> resolve_buckets_ns{0};
    std::atomic<uint64_t> swiss_ns{0};
    std::atomic<uint64_t> post_swiss_stage_ns{0};
    std::atomic<uint64_t> final_playoff_ns{0};

    std::atomic<uint64_t> toside_total_ns{0};
    std::atomic<uint64_t> toside_hits{0};
    std::atomic<uint64_t> toside_misses{0};

    std::atomic<uint64_t> play_series_batch_ns{0};
    std::atomic<uint32_t> play_series_batch_calls{0};
    std::atomic<uint64_t> play_bon_batch_ns{0};
    std::atomic<uint32_t> play_bon_batch_calls{0};

    std::atomic<uint64_t> parallel_batch_wait_ns{0};
    std::atomic<uint32_t> parallel_batch_calls{0};

    static void add_ns(std::atomic<uint64_t>& dest, std::chrono::nanoseconds d) {
        if (d.count() <= 0) return;
        dest.fetch_add(static_cast<uint64_t>(d.count()), std::memory_order_relaxed);
    }

    void reset() {
        expand_and_pick_ns.store(0, std::memory_order_relaxed);
        resolve_buckets_ns.store(0, std::memory_order_relaxed);
        swiss_ns.store(0, std::memory_order_relaxed);
        post_swiss_stage_ns.store(0, std::memory_order_relaxed);
        final_playoff_ns.store(0, std::memory_order_relaxed);
        toside_total_ns.store(0, std::memory_order_relaxed);
        toside_hits.store(0, std::memory_order_relaxed);
        toside_misses.store(0, std::memory_order_relaxed);
        play_series_batch_ns.store(0, std::memory_order_relaxed);
        play_series_batch_calls.store(0, std::memory_order_relaxed);
        play_bon_batch_ns.store(0, std::memory_order_relaxed);
        play_bon_batch_calls.store(0, std::memory_order_relaxed);
        parallel_batch_wait_ns.store(0, std::memory_order_relaxed);
        parallel_batch_calls.store(0, std::memory_order_relaxed);
    }

    static double sec(uint64_t ns) { return static_cast<double>(ns) * 1e-9; }

    void dump(std::ostream& os, std::chrono::nanoseconds wall) const {
        const uint64_t ex = expand_and_pick_ns.load(std::memory_order_relaxed);
        const uint64_t rb = resolve_buckets_ns.load(std::memory_order_relaxed);
        const uint64_t sw = swiss_ns.load(std::memory_order_relaxed);
        const uint64_t p2 = post_swiss_stage_ns.load(std::memory_order_relaxed);
        const uint64_t fp = final_playoff_ns.load(std::memory_order_relaxed);
        const uint64_t ts = toside_total_ns.load(std::memory_order_relaxed);
        const uint64_t psb = play_series_batch_ns.load(std::memory_order_relaxed);
        const uint64_t pbb = play_bon_batch_ns.load(std::memory_order_relaxed);
        const uint64_t pbw = parallel_batch_wait_ns.load(std::memory_order_relaxed);

        const int64_t wns = wall.count();
        const double wall_s = static_cast<double>(std::max<int64_t>(0, wns)) * 1e-9;
        const double w = std::max(1e-18, wall_s);

        os << "[GDF timing] wall_s=" << wall_s << "\n";
        auto pct = [&](uint64_t ns) { return 100.0 * sec(ns) / w; };

        os << "  GreedySearcher (sum over sizes):\n";
        os << "    expand+insertion_pick_ns=" << sec(ex) << " s (" << pct(ex) << "% wall)\n";
        os << "    resolve_conflict_buckets_ns=" << sec(rb) << " s (" << pct(rb) << "% wall)\n";
        os << "    swiss_ns=" << sec(sw) << " s (" << pct(sw) << "% wall)\n";
        os << "    post_swiss_rr_or_greedy_ns=" << sec(p2) << " s (" << pct(p2) << "% wall)\n";
        os << "    final_playoff_ns=" << sec(fp) << " s (" << pct(fp) << "% wall)\n";

        os << "  BattleEvaluator::ToSide:\n";
        os << "    total_ns=" << sec(ts) << " s (" << pct(ts) << "% wall)\n";
        os << "    cache_hits=" << toside_hits.load(std::memory_order_relaxed) << " misses=" << toside_misses.load(std::memory_order_relaxed)
           << "\n";

        os << "  BattleEvaluator batches (includes worker CPU; parallel wait is main-thread barrier):\n";
        os << "    PlaySeriesBatch calls=" << play_series_batch_calls.load(std::memory_order_relaxed) << " sum_ns=" << sec(psb) << " s ("
           << pct(psb) << "% wall)\n";
        os << "    PlayBoNBatch calls=" << play_bon_batch_calls.load(std::memory_order_relaxed) << " sum_ns=" << sec(pbb) << " s (" << pct(pbb)
           << "% wall)\n";
        os << "    parallel_batch_wait calls=" << parallel_batch_calls.load(std::memory_order_relaxed) << " sum_ns=" << sec(pbw) << " s ("
           << pct(pbw) << "% wall)\n";

        const uint64_t sum_g = ex + rb + sw + p2 + fp;
        os << "  Note: GreedySearcher phases exclude BattleEvaluator internals; ToSide/Series/BoN overlap Swiss/RR work.\n";
        os << "  Sum(greedy_phases)_s=" << sec(sum_g) << " (for sanity vs wall)\n";
    }
};

}  // namespace bazaararena::gdf
