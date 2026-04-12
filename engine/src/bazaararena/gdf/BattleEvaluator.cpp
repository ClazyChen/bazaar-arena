#include <bazaararena/gdf/BattleEvaluator.hpp>

#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/core/SimulatorInit.hpp>
#include <bazaararena/gdf/GdfLevelRules.hpp>
#include <bazaararena/gdf/GdfSideBuilder.hpp>
#include <bazaararena/io/SideStateBuilder.hpp>
#include <bazaararena/io/Sink.hpp>

#include <algorithm>
#include <random>
#include <thread>

namespace bazaararena::gdf {
namespace core = bazaararena::core;
namespace io = bazaararena::io;

namespace {

static constexpr int kParallelPairsMin = 2;
static constexpr int kSaltPlayBoNBatch = 0x504C424E;    // 'PLBN'
static constexpr int kSaltPlaySeriesBatch = 0x53525353;  // 'SRSS'

static int HpShieldTotal(const core::SideState& s) {
    return s.attrs[core::SideKey::Hp] + s.attrs[core::SideKey::Shield];
}

}  // namespace

BattleEvaluator::BattleEvaluator(int best_of, int workers, int player_level, std::optional<int> deterministic_battle_seed)
    : best_of_(best_of)
    , workers_(std::max(0, workers))
    , player_level_(player_level)
    , combat_tier_(GdfLevelRules::CombatTier(player_level))
    , deterministic_battle_seed_(deterministic_battle_seed) {}

const core::SideState& BattleEvaluator::ToSide(const DeckRep& rep) {
    const std::string sig = rep.Signature();
    auto it = deck_cache_.find(sig);
    if (it != deck_cache_.end()) return it->second;
    io::SideSpec spec;
    std::string err;
    if (!BuildSideSpecFromDeck(rep, player_level_, combat_tier_, spec, err)) {
        // 不应到达：上层保证物品合法
        throw std::runtime_error("BuildSideSpecFromDeck: " + err);
    }
    auto built = io::BuildSideState(spec);
    if (!built.side) {
        throw std::runtime_error("BuildSideState: " + built.error);
    }
    auto ins = deck_cache_.emplace(sig, std::move(*built.side));
    return ins.first->second;
}

int BattleEvaluator::MixSeed(int base, long long batch_id, int pair_index, int salt) {
    uint64_t x = static_cast<uint32_t>(base);
    x ^= static_cast<uint64_t>(batch_id) * 0x9E3779B97F4A7C15ULL;
    x ^= static_cast<uint64_t>(pair_index) * 0xBF58476D1CE4E5B9ULL;
    x ^= static_cast<uint64_t>(salt) * 0x94D049BB133111EBULL;
    x ^= x >> 33;
    x *= 0xff51afd7ed558ccdULL;
    x ^= x >> 33;
    return static_cast<int>(x & 0x7fffffff);
}

std::vector<size_t> BattleEvaluator::CreateShuffledOrder(size_t count, std::mt19937& rng) {
    std::vector<size_t> order(count);
    for (size_t k = 0; k < count; k++) order[k] = k;
    for (size_t i = count; i > 1; --i) {
        std::uniform_int_distribution<size_t> d(0, i - 1);
        size_t j = d(rng);
        std::swap(order[i - 1], order[j]);
    }
    return order;
}

int BattleEvaluator::PlaySingleGameForSeries(const DeckRep& a, const DeckRep& b, std::optional<unsigned> dedicated_seed) {
    const core::SideState& side_a = ToSide(a);
    const core::SideState& side_b = ToSide(b);
    std::mt19937 rng;
    if (dedicated_seed.has_value()) rng.seed(*dedicated_seed);
    else {
        std::random_device rd;
        rng.seed(rd());
    }
    const int swap = static_cast<int>(rng() & 1u);

    core::Simulator sim;
    sim.sink.sink_type = io::Sink::TypeNone;
    sim.sink.max_events = 0;
    if (swap == 0) {
        sim.sides[0] = side_a;
        sim.sides[1] = side_b;
    } else {
        sim.sides[0] = side_b;
        sim.sides[1] = side_a;
    }
    sim.rng.Seed(static_cast<int>(rng()));
    core::InitializeSimulator(sim);
    int w = sim.Run(true);

    auto map_winner_to_a = [&](int win_sim) -> int {
        // win_sim: 0 = sides[0] 胜
        if (swap == 0) return win_sim;  // sides[0]=A
        return 1 - win_sim;             // sides[0]=B
    };

    if (w >= 0) return map_winner_to_a(w);

    const int h0 = HpShieldTotal(sim.sides[0]);
    const int h1 = HpShieldTotal(sim.sides[1]);
    int win_side = -1;
    if (h0 > h1) win_side = 0;
    else if (h1 > h0) win_side = 1;
    else {
        return -1;  // 系列赛：绝对平局记半分，由上层处理
    }
    return map_winner_to_a(win_side);
}

int BattleEvaluator::PlaySingleGameForBoN(const DeckRep& a, const DeckRep& b, std::optional<unsigned> dedicated_seed) {
    const core::SideState& side_a = ToSide(a);
    const core::SideState& side_b = ToSide(b);
    std::mt19937 rng;
    if (dedicated_seed.has_value()) rng.seed(*dedicated_seed);
    else {
        std::random_device rd;
        rng.seed(rd());
    }
    const int swap = static_cast<int>(rng() & 1u);

    core::Simulator sim;
    sim.sink.sink_type = io::Sink::TypeNone;
    sim.sink.max_events = 0;
    if (swap == 0) {
        sim.sides[0] = side_a;
        sim.sides[1] = side_b;
    } else {
        sim.sides[0] = side_b;
        sim.sides[1] = side_a;
    }
    sim.rng.Seed(static_cast<int>(rng()));
    core::InitializeSimulator(sim);
    int w = sim.Run(true);

    auto map_winner_to_a = [&](int win_sim) -> int {
        if (swap == 0) return win_sim;
        return 1 - win_sim;
    };

    if (w >= 0) return map_winner_to_a(w);

    const int h0 = HpShieldTotal(sim.sides[0]);
    const int h1 = HpShieldTotal(sim.sides[1]);
    if (h0 > h1) return map_winner_to_a(0);
    if (h1 > h0) return map_winner_to_a(1);
    return static_cast<int>(rng() & 1u);  // BO 单局绝对平局随机
}

int BattleEvaluator::RunBoNStream(const DeckRep& a, const DeckRep& b, std::optional<unsigned> stream_seed) {
    std::mt19937 stream;
    if (stream_seed.has_value()) stream.seed(*stream_seed);
    else {
        std::random_device rd;
        stream.seed(rd());
    }
    const int need = (best_of_ / 2) + 1;
    int wins_a = 0;
    int wins_b = 0;
    while (wins_a < need && wins_b < need) {
        const unsigned gseed = stream();
        const int g = PlaySingleGameForBoN(a, b, gseed);
        if (g == 0) wins_a++;
        else wins_b++;
    }
    return wins_a > wins_b ? 0 : 1;
}

int BattleEvaluator::PlayBoN(const DeckRep& a, const DeckRep& b) { return RunBoNStream(a, b, std::nullopt); }

std::vector<int> BattleEvaluator::PlayBoNBatch(const std::vector<std::pair<DeckRep, DeckRep>>& pairs) {
    std::vector<int> winners(pairs.size());
    if (pairs.empty()) return winners;

    std::mt19937 order_rng;
    if (deterministic_battle_seed_.has_value()) order_rng.seed(static_cast<unsigned>(*deterministic_battle_seed_));
    else {
        std::random_device rd;
        order_rng.seed(rd());
    }

    if (workers_ <= 1 || static_cast<int>(pairs.size()) < kParallelPairsMin) {
        for (size_t i = 0; i < pairs.size(); i++) winners[i] = PlayBoN(pairs[i].first, pairs[i].second);
        return winners;
    }

    const auto order = CreateShuffledOrder(pairs.size(), order_rng);
    const long long batch_id = ++parallel_batch_seq_;
    std::vector<std::thread> threads;
    const int wcount = std::min(workers_, static_cast<int>(pairs.size()));
    const size_t chunk = (pairs.size() + static_cast<size_t>(wcount) - 1) / static_cast<size_t>(wcount);
    for (int t = 0; t < wcount; t++) {
        const size_t beg = static_cast<size_t>(t) * chunk;
        if (beg >= pairs.size()) break;
        const size_t end = std::min(pairs.size(), beg + chunk);
        threads.emplace_back([&, beg, end]() {
            for (size_t k = beg; k < end; k++) {
                const size_t j = order[k];
                std::optional<unsigned> stream;
                if (deterministic_battle_seed_.has_value()) {
                    const int s = MixSeed(*deterministic_battle_seed_, batch_id, static_cast<int>(j), kSaltPlayBoNBatch);
                    stream = static_cast<unsigned>(s);
                }
                winners[j] = RunBoNStream(pairs[j].first, pairs[j].second, stream);
            }
        });
    }
    for (auto& th : threads) th.join();
    return winners;
}

MatchPoints BattleEvaluator::PlaySeriesPointsCore(const DeckRep& a, const DeckRep& b, int game_count, std::optional<unsigned> dedicated_seed) {
    const int rounds = std::max(1, game_count);
    double points_a = 0;
    double points_b = 0;
    std::mt19937 local;
    if (dedicated_seed.has_value()) local.seed(*dedicated_seed);
    else {
        std::random_device rd;
        local.seed(rd());
    }
    for (int i = 0; i < rounds; i++) {
        const unsigned seed = local();
        const int r = PlaySingleGameForSeries(a, b, seed);
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
    return PlaySeriesPointsCore(a, b, game_count, std::nullopt);
}

std::vector<MatchPoints> BattleEvaluator::PlaySeriesBatch(const std::vector<std::pair<DeckRep, DeckRep>>& pairs, int game_count) {
    std::vector<MatchPoints> results(pairs.size());
    if (pairs.empty()) return results;

    std::mt19937 order_rng;
    if (deterministic_battle_seed_.has_value()) order_rng.seed(static_cast<unsigned>(*deterministic_battle_seed_));
    else {
        std::random_device rd;
        order_rng.seed(rd());
    }

    if (workers_ <= 1 || static_cast<int>(pairs.size()) < kParallelPairsMin) {
        for (size_t i = 0; i < pairs.size(); i++) results[i] = PlaySeriesPointsCore(pairs[i].first, pairs[i].second, game_count, std::nullopt);
        return results;
    }

    const auto order = CreateShuffledOrder(pairs.size(), order_rng);
    const long long batch_id = ++parallel_batch_seq_;
    std::vector<std::thread> threads;
    const int wcount = std::min(workers_, static_cast<int>(pairs.size()));
    const size_t chunk = (pairs.size() + static_cast<size_t>(wcount) - 1) / static_cast<size_t>(wcount);
    for (int t = 0; t < wcount; t++) {
        const size_t beg = static_cast<size_t>(t) * chunk;
        if (beg >= pairs.size()) break;
        const size_t end = std::min(pairs.size(), beg + chunk);
        threads.emplace_back([&, beg, end]() {
            for (size_t k = beg; k < end; k++) {
                const size_t j = order[k];
                std::optional<unsigned> ded;
                if (deterministic_battle_seed_.has_value()) {
                    const int s = MixSeed(*deterministic_battle_seed_, batch_id, static_cast<int>(j), kSaltPlaySeriesBatch);
                    ded = static_cast<unsigned>(s);
                }
                results[j] = PlaySeriesPointsCore(pairs[j].first, pairs[j].second, game_count, ded);
            }
        });
    }
    for (auto& th : threads) th.join();
    return results;
}

}  // namespace bazaararena::gdf
