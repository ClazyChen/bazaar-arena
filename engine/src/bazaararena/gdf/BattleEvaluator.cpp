#include <bazaararena/gdf/BattleEvaluator.hpp>

#include <bazaararena/core/ItemKey.hpp>
#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/core/SideState.hpp>
#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/core/SimulatorInit.hpp>
#include <bazaararena/gdf/GdfLevelRules.hpp>
#include <bazaararena/gdf/GdfSideBuilder.hpp>
#include <bazaararena/io/SideStateBuilder.hpp>
#include <bazaararena/io/Sink.hpp>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <random>
#include <thread>

namespace bazaararena::gdf {
namespace core = bazaararena::core;
namespace io = bazaararena::io;

namespace {

static constexpr int kParallelPairsMin = 2;

static int HpShieldTotal(const core::SideState& s) {
    return s.attrs[core::SideKey::Hp] + s.attrs[core::SideKey::Shield];
}

static uint64_t FastEntropyU64() {
    const auto t = std::chrono::high_resolution_clock::now().time_since_epoch().count();
    const uintptr_t p = reinterpret_cast<uintptr_t>(&t);
    return static_cast<uint64_t>(t) ^ (static_cast<uint64_t>(p) << 32);
}

static std::atomic<uint64_t> g_salt_counter{1};

static uint64_t NextBatchSaltU64() {
    return FastEntropyU64() ^ (g_salt_counter.fetch_add(1, std::memory_order_relaxed) * UINT64_C(0xBF58476D1CE4E5B9));
}

core::Simulator& TlsBattleSimulator() {
    thread_local core::Simulator sim;
    return sim;
}

/// `GdfSideBuilder::BuildSideSpecFromDeck` 固定 `sideId=0`，缓存中物品的 `ItemKey::SideIndex` 均为 0。
/// 装入 `sim.sides[1]` 前必须改为 1，否则 `AbilityApply` 等用 `SideIndex` 解析对手时会打到己方（与 CLI 的两侧 `sideId` 不一致）。
static void PatchPhysicalSideSlot(core::SideState& side, int physical_slot) {
    if (physical_slot < 0 || physical_slot >= core::Simulator::SideCount) return;
    side.attrs[core::SideKey::Id] = physical_slot;
    const int n = side.attrs[core::SideKey::ItemCount];
    for (int i = 0; i < n && i < static_cast<int>(core::SideState::MaxItems); i++) {
        side.items[static_cast<size_t>(i)].attrs[core::ItemKey::SideIndex] = physical_slot;
    }
}

/// 与 `engine/cli/main.cpp` 中 `RunBatchSlice` 每局循环一致：先装两侧与沙尘暴，再清 sink、播种、InitializeSimulator，最后 Run。
/// 配合 `thread_local` 复用 `Simulator`，避免每局堆分配，同时保证局间状态与 CLI 批量对战一致。
static int RunSingleBattleReturn(core::Simulator& sim, const core::SideState& side_a, const core::SideState& side_b, int swap, int rng_seed) {
    if (swap == 0) {
        sim.sides[0] = side_a;
        sim.sides[1] = side_b;
    } else {
        sim.sides[0] = side_b;
        sim.sides[1] = side_a;
    }
    sim.sandstorm = core::Simulator::SandStorm{};
    sim.sink.sink_type = io::Sink::TypeNone;
    sim.sink.max_events = 0;
    sim.sink.truncated = false;
    sim.sink.Clear();
    sim.rng.Seed(rng_seed);
    core::InitializeSimulator(sim);
    return sim.Run(true);
}

}  // namespace

BattleEvaluator::BattleEvaluator(int best_of, int workers, int player_level)
    : best_of_(best_of), workers_(std::max(0, workers)), player_level_(player_level), combat_tier_(GdfLevelRules::CombatTier(player_level)) {}

core::SideState BattleEvaluator::ToSide(const DeckRep& rep) {
    const std::string sig = rep.Signature();
    {
        std::shared_lock<std::shared_mutex> lk(deck_cache_mu_);
        const auto it = deck_cache_.find(sig);
        if (it != deck_cache_.end()) return it->second;
    }
    io::SideSpec spec;
    std::string err;
    if (!BuildSideSpecFromDeck(rep, player_level_, combat_tier_, spec, err)) {
        throw std::runtime_error("BuildSideSpecFromDeck: " + err);
    }
    auto built = io::BuildSideState(spec);
    if (!built.side) {
        throw std::runtime_error("BuildSideState: " + built.error);
    }
    std::unique_lock<std::shared_mutex> lk(deck_cache_mu_);
    const auto it2 = deck_cache_.find(sig);
    if (it2 != deck_cache_.end()) return it2->second;
    auto ins = deck_cache_.emplace(sig, std::move(*built.side));
    return ins.first->second;
}

std::vector<size_t> BattleEvaluator::CreateShuffledOrder(size_t count, std::mt19937& rng) {
    std::vector<size_t> order(count);
    for (size_t k = 0; k < count; k++) order[k] = k;
    for (size_t i = count; i > 1; --i) {
        std::uniform_int_distribution<size_t> d(0, i - 1);
        const size_t j = d(rng);
        std::swap(order[i - 1], order[j]);
    }
    return order;
}

int BattleEvaluator::PlaySingleGameForSeries(const DeckRep& a, const DeckRep& b, unsigned game_word) {
    core::SideState side_a = ToSide(a);
    core::SideState side_b = ToSide(b);
    const int swap = static_cast<int>(game_word & 1u);
    const int rng_seed = static_cast<int>(game_word ^ (game_word >> 16));

    if (swap == 0) {
        PatchPhysicalSideSlot(side_a, 0);
        PatchPhysicalSideSlot(side_b, 1);
    } else {
        PatchPhysicalSideSlot(side_b, 0);
        PatchPhysicalSideSlot(side_a, 1);
    }

    core::Simulator& sim = TlsBattleSimulator();
    const int w_run = RunSingleBattleReturn(sim, side_a, side_b, swap, rng_seed);

    auto map_winner_to_a = [&](int win_sim) -> int {
        if (swap == 0) return win_sim;
        return 1 - win_sim;
    };

    if (w_run >= 0) return map_winner_to_a(w_run);

    const int h0 = HpShieldTotal(sim.sides[0]);
    const int h1 = HpShieldTotal(sim.sides[1]);
    int win_side = -1;
    if (h0 > h1) win_side = 0;
    else if (h1 > h0) win_side = 1;
    else {
        return -1;
    }
    return map_winner_to_a(win_side);
}

int BattleEvaluator::PlaySingleGameForBoN(const DeckRep& a, const DeckRep& b, unsigned game_word) {
    core::SideState side_a = ToSide(a);
    core::SideState side_b = ToSide(b);
    const int swap = static_cast<int>(game_word & 1u);
    const int rng_seed = static_cast<int>(game_word ^ (game_word >> 16));

    if (swap == 0) {
        PatchPhysicalSideSlot(side_a, 0);
        PatchPhysicalSideSlot(side_b, 1);
    } else {
        PatchPhysicalSideSlot(side_b, 0);
        PatchPhysicalSideSlot(side_a, 1);
    }

    core::Simulator& sim = TlsBattleSimulator();
    const int w = RunSingleBattleReturn(sim, side_a, side_b, swap, rng_seed);

    auto map_winner_to_a = [&](int win_sim) -> int {
        if (swap == 0) return win_sim;
        return 1 - win_sim;
    };

    if (w >= 0) return map_winner_to_a(w);

    const int h0 = HpShieldTotal(sim.sides[0]);
    const int h1 = HpShieldTotal(sim.sides[1]);
    if (h0 > h1) return map_winner_to_a(0);
    if (h1 > h0) return map_winner_to_a(1);
    return static_cast<int>((game_word >> 1) & 1u);
}

int BattleEvaluator::RunBoNStream(const DeckRep& a, const DeckRep& b, unsigned stream_seed) {
    std::mt19937 st(stream_seed ^ 0xA11CEu);
    const int need = (best_of_ / 2) + 1;
    int wins_a = 0;
    int wins_b = 0;
    while (wins_a < need && wins_b < need) {
        const unsigned gw = st();
        const int g = PlaySingleGameForBoN(a, b, gw);
        if (g == 0) wins_a++;
        else wins_b++;
    }
    return wins_a > wins_b ? 0 : 1;
}

int BattleEvaluator::PlayBoN(const DeckRep& a, const DeckRep& b) {
    return RunBoNStream(a, b, static_cast<unsigned>(NextBatchSaltU64()));
}

std::vector<int> BattleEvaluator::PlayBoNBatch(const std::vector<std::pair<DeckRep, DeckRep>>& pairs) {
    std::vector<int> winners(pairs.size());
    if (pairs.empty()) return winners;

    const uint64_t batch_salt = NextBatchSaltU64();
    std::mt19937 order_rng(static_cast<unsigned>(batch_salt ^ (batch_salt >> 32)));

    if (workers_ <= 1 || static_cast<int>(pairs.size()) < kParallelPairsMin) {
        for (size_t i = 0; i < pairs.size(); i++) {
            const unsigned ss = static_cast<unsigned>(batch_salt ^ (i * UINT64_C(0xD6E8FEB866E1C9A5)));
            winners[i] = RunBoNStream(pairs[i].first, pairs[i].second, ss);
        }
        return winners;
    }

    const auto order = CreateShuffledOrder(pairs.size(), order_rng);
    std::vector<std::thread> threads;
    const int wcount = std::min(workers_, static_cast<int>(pairs.size()));
    const size_t chunk = (pairs.size() + static_cast<size_t>(wcount) - 1) / static_cast<size_t>(wcount);
    for (int t = 0; t < wcount; t++) {
        const size_t beg = static_cast<size_t>(t) * chunk;
        if (beg >= pairs.size()) break;
        const size_t end = std::min(pairs.size(), beg + chunk);
        threads.emplace_back([&, beg, end, batch_salt]() {
            for (size_t k = beg; k < end; k++) {
                const size_t j = order[k];
                const unsigned ss = static_cast<unsigned>(batch_salt ^ (j * UINT64_C(0xD6E8FEB866E1C9A5)));
                winners[j] = RunBoNStream(pairs[j].first, pairs[j].second, ss);
            }
        });
    }
    for (auto& th : threads) th.join();
    return winners;
}

MatchPoints BattleEvaluator::PlaySeriesPointsForPair(const DeckRep& a, const DeckRep& b, int game_count, unsigned pair_seed) {
    const int rounds = std::max(1, game_count);
    double points_a = 0;
    double points_b = 0;
    std::mt19937 local(pair_seed);
    for (int i = 0; i < rounds; i++) {
        const unsigned gw = local();
        const int r = PlaySingleGameForSeries(a, b, gw);
        if (r == 0) points_a += 1;
        else if (r == 1) points_b += 1;
        else {
            points_a += 0.5;
            points_b += 0.5;
        }
    }
    return {points_a, points_b};
}

MatchPoints BattleEvaluator::PlaySeriesPoints(const DeckRep& a, const DeckRep& b, int game_count) {
    return PlaySeriesPointsForPair(a, b, game_count, static_cast<unsigned>(NextBatchSaltU64()));
}

std::vector<MatchPoints> BattleEvaluator::PlaySeriesBatch(const std::vector<std::pair<DeckRep, DeckRep>>& pairs, int game_count) {
    std::vector<MatchPoints> results(pairs.size());
    if (pairs.empty()) return results;

    const uint64_t batch_salt = NextBatchSaltU64();
    std::mt19937 order_rng(static_cast<unsigned>(batch_salt ^ (batch_salt >> 32)));

    if (workers_ <= 1 || static_cast<int>(pairs.size()) < kParallelPairsMin) {
        for (size_t i = 0; i < pairs.size(); i++) {
            const unsigned ps = static_cast<unsigned>(batch_salt ^ (i * UINT64_C(0xC4CEB4B2570A8625)));
            results[i] = PlaySeriesPointsForPair(pairs[i].first, pairs[i].second, game_count, ps);
        }
        return results;
    }

    const auto order = CreateShuffledOrder(pairs.size(), order_rng);
    std::vector<std::thread> threads;
    const int wcount = std::min(workers_, static_cast<int>(pairs.size()));
    const size_t chunk = (pairs.size() + static_cast<size_t>(wcount) - 1) / static_cast<size_t>(wcount);
    for (int t = 0; t < wcount; t++) {
        const size_t beg = static_cast<size_t>(t) * chunk;
        if (beg >= pairs.size()) break;
        const size_t end = std::min(pairs.size(), beg + chunk);
        threads.emplace_back([&, beg, end, batch_salt, game_count]() {
            for (size_t k = beg; k < end; k++) {
                const size_t j = order[k];
                const unsigned ps = static_cast<unsigned>(batch_salt ^ (j * UINT64_C(0xC4CEB4B2570A8625)));
                results[j] = PlaySeriesPointsForPair(pairs[j].first, pairs[j].second, game_count, ps);
            }
        });
    }
    for (auto& th : threads) th.join();
    return results;
}

}  // namespace bazaararena::gdf
