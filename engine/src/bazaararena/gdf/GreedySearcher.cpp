#include <bazaararena/gdf/GreedySearcher.hpp>

#include <bazaararena/data/ItemDatabase.hpp>
#include <bazaararena/gdf/DeckRep.hpp>
#include <bazaararena/gdf/GdfLevelRules.hpp>
#include <bazaararena/gdf/GdfRunTiming.hpp>
#include <bazaararena/gdf/SwissTournament.hpp>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <random>
#include <stdexcept>
#include <unordered_map>
#include <utility>

using SteadyClock = std::chrono::steady_clock;

namespace bazaararena::gdf {
namespace {

template <typename T>
static void ShuffleInPlace(std::vector<T>& list, std::mt19937& rng) {
    if (list.size() <= 1) return;
    for (size_t i = list.size() - 1; i > 0; --i) {
        std::uniform_int_distribution<size_t> dist(0, i);
        const size_t j = dist(rng);
        std::swap(list[i], list[j]);
    }
}

/// 与 legacy `RunBatchedKnockoutMany` 一致：多组并行单败淘汰，每波全局 `PlayBoNBatch`。
template <typename T, typename GetRep>
static std::vector<T> RunBatchedKnockoutMany(std::vector<std::vector<T>> sources, BattleEvaluator& eval, std::mt19937& rng, GetRep get_rep) {
    struct Tm {
        std::vector<T> alive;
    };
    std::vector<Tm> tournaments;
    tournaments.reserve(sources.size());
    for (auto& src : sources) {
        if (src.empty()) throw std::runtime_error("RunBatchedKnockoutMany: empty source list");
        Tm tm;
        tm.alive = std::move(src);
        ShuffleInPlace(tm.alive, rng);
        tournaments.push_back(std::move(tm));
    }

    while (true) {
        std::vector<std::pair<DeckRep, DeckRep>> batch;
        size_t alive_tournament_count = 0;
        for (auto& tm : tournaments) {
            if (tm.alive.size() <= 1) continue;
            ++alive_tournament_count;
            for (size_t i = 0; i + 1 < tm.alive.size(); i += 2) {
                batch.emplace_back(get_rep(tm.alive[i]), get_rep(tm.alive[i + 1]));
            }
        }
        if (alive_tournament_count == 0) break;

        const std::vector<int> wins = eval.PlayBoNBatch(batch);

        size_t wi = 0;
        for (auto& tm : tournaments) {
            if (tm.alive.size() <= 1) continue;
            std::vector<T> new_alive;
            new_alive.reserve((tm.alive.size() + 1) / 2);
            for (size_t i = 0; i + 1 < tm.alive.size(); i += 2) {
                const int w = wins[wi++];
                if (w == 0) new_alive.push_back(std::move(tm.alive[i]));
                else new_alive.push_back(std::move(tm.alive[i + 1]));
            }
            if ((tm.alive.size() & 1u) == 1u) new_alive.push_back(std::move(tm.alive.back()));
            tm.alive = std::move(new_alive);
        }
    }

    std::vector<T> result;
    result.reserve(tournaments.size());
    for (auto& tm : tournaments) {
        if (tm.alive.size() != 1) throw std::runtime_error("RunBatchedKnockoutMany: tournament did not resolve to one winner");
        result.push_back(std::move(tm.alive[0]));
    }
    return result;
}

}  // namespace

GreedySearcher::GreedySearcher(const ItemPool& pool, BattleEvaluator& evaluator, std::mt19937& rng, const GreedyConfig& config,
    const std::unordered_set<std::string>& seed_items)
    : pool_(pool), evaluator_(evaluator), rng_(rng), config_(config), seed_items_(seed_items) {}

std::vector<DeckRep> GreedySearcher::BuildInsertionReps(const DeckRep& prev, const std::string& new_item) {
    std::vector<DeckRep> reps;
    reps.reserve(prev.item_names.size() + 1);
    for (size_t pos = 0; pos <= prev.item_names.size(); pos++) {
        std::vector<std::string> items = prev.item_names;
        items.insert(items.begin() + static_cast<std::ptrdiff_t>(pos), new_item);
        reps.push_back(DeckRep{std::move(items)});
    }
    return reps;
}

std::vector<CandidateState> GreedySearcher::ResolveConflictBuckets(std::unordered_map<std::string, std::vector<CandidateState>>& buckets) {
    std::vector<std::pair<std::string, std::vector<CandidateState>>> entries;
    entries.reserve(buckets.size());
    for (auto& kv : buckets) entries.emplace_back(std::move(kv.first), std::move(kv.second));
    std::sort(entries.begin(), entries.end(), [](const auto& a, const auto& b) { return a.first < b.first; });

    std::vector<char> was_multi(entries.size(), 0);
    std::vector<std::vector<CandidateState>> multi_jobs;
    multi_jobs.reserve(entries.size());
    for (size_t i = 0; i < entries.size(); ++i) {
        auto& bucket = entries[i].second;
        if (bucket.size() > 1) {
            was_multi[i] = 1;
            multi_jobs.push_back(std::move(bucket));
        }
    }

    std::vector<CandidateState> multi_winners;
    if (!multi_jobs.empty()) {
        multi_winners = RunBatchedKnockoutMany(std::move(multi_jobs), evaluator_, rng_,
            [](const CandidateState& c) -> const DeckRep& { return c.representative; });
    }

    std::vector<CandidateState> out;
    out.reserve(entries.size());
    size_t mj = 0;
    for (size_t i = 0; i < entries.size(); ++i) {
        if (was_multi[i]) {
            out.push_back(std::move(multi_winners[mj++]));
        } else {
            auto& bucket = entries[i].second;
            if (!bucket.empty()) out.push_back(std::move(bucket[0]));
        }
    }
    return out;
}

std::vector<CandidateState> GreedySearcher::ResolveFinalTopTieByPlayoff(std::vector<CandidateState> stage2) {
    if (stage2.size() <= 1) return stage2;
    double top_score = stage2[0].round_robin_score;
    for (const auto& x : stage2) top_score = std::max(top_score, x.round_robin_score);
    std::vector<CandidateState> tied;
    std::vector<CandidateState> others;
    for (auto& c : stage2) {
        if (std::abs(c.round_robin_score - top_score) < 1e-9) tied.push_back(std::move(c));
        else others.push_back(std::move(c));
    }
    if (tied.size() <= 1) {
        std::vector<CandidateState> merged;
        merged.push_back(std::move(tied[0]));
        merged.insert(merged.end(), std::make_move_iterator(others.begin()), std::make_move_iterator(others.end()));
        return merged;
    }

    std::unordered_map<std::string, double> playoff_score;
    for (const auto& c : tied) playoff_score[c.combo_key] = 0;

    std::vector<std::pair<DeckRep, DeckRep>> pair_list;
    for (size_t i = 0; i < tied.size(); i++) {
        for (size_t j = i + 1; j < tied.size(); j++) pair_list.emplace_back(tied[i].representative, tied[j].representative);
    }
    if (!pair_list.empty()) {
        auto pts = evaluator_.PlaySeriesBatch(pair_list, 20);
        size_t idx = 0;
        for (size_t i = 0; i < tied.size(); i++) {
            for (size_t j = i + 1; j < tied.size(); j++) {
                playoff_score[tied[i].combo_key] += pts[idx].a;
                playoff_score[tied[j].combo_key] += pts[idx].b;
                idx++;
            }
        }
    }

    std::unordered_map<std::string, unsigned> tie_break;
    std::uniform_int_distribution<unsigned> u;
    for (const auto& c : tied) tie_break[c.combo_key] = u(rng_);

    std::sort(tied.begin(), tied.end(), [&](const CandidateState& a, const CandidateState& b) {
        if (playoff_score[a.combo_key] != playoff_score[b.combo_key]) return playoff_score[a.combo_key] > playoff_score[b.combo_key];
        if (a.swiss_score != b.swiss_score) return a.swiss_score > b.swiss_score;
        return tie_break[a.combo_key] < tie_break[b.combo_key];
    });

    std::vector<CandidateState> merged;
    merged.reserve(tied.size() + others.size());
    merged.insert(merged.end(), std::make_move_iterator(tied.begin()), std::make_move_iterator(tied.end()));
    merged.insert(merged.end(), std::make_move_iterator(others.begin()), std::make_move_iterator(others.end()));
    return merged;
}

std::unordered_map<int, std::vector<CandidateState>> GreedySearcher::Run(const std::vector<std::string>& seed_ordered_items,
    const std::function<void(int size, const std::vector<CandidateState>& top)>& on_size_completed) {
    if (seed_ordered_items.empty()) throw std::invalid_argument("seed_ordered_items empty");

    std::unordered_set<std::string> seen;
    int anchor_size = 0;
    std::vector<std::string> ordered_names;
    for (const auto& raw : seed_ordered_items) {
        if (!seen.insert(raw).second) throw std::runtime_error("duplicate seed item: " + raw);
        ResolvedItem ri = ResolveItemAlias(raw);
        const auto* templ = bazaararena::data::GetItemByKey(ri.db_key);
        if (!templ) throw std::runtime_error("unknown item: " + raw);
        const int sz = ItemPool::SizeOfItem(templ);
        if (sz <= 0) throw std::runtime_error("bad item size: " + raw);
        anchor_size += sz;
        ordered_names.push_back(raw);
    }

    const int max_size_sum = GdfLevelRules::MaxSlotsForLevel(config_.player_level);
    if (anchor_size > max_size_sum) throw std::runtime_error("anchor size exceeds max slots for level");

    std::unordered_map<int, std::vector<CandidateState>> top_by_size;
    DeckRep init_rep{ordered_names};
    CandidateState init_cs;
    init_cs.combo_key = BuildComboKey(init_rep.item_names);
    init_cs.representative = std::move(init_rep);
    init_cs.size_sum = anchor_size;
    top_by_size[anchor_size] = std::vector<CandidateState>{std::move(init_cs)};

    for (int s = anchor_size + 1; s <= max_size_sum; s++) {
        GdfRunTiming* const tr = config_.run_timing;

        std::unordered_map<std::string, std::vector<CandidateState>> candidate_buckets;

        const SteadyClock::time_point t_expand_beg = SteadyClock::now();
        for (int q = 1; q <= 3; q++) {
            const int p = s - q;
            if (p < anchor_size) continue;
            auto it_prev = top_by_size.find(p);
            if (it_prev == top_by_size.end()) continue;

            for (const auto& prev : it_prev->second) {
                std::unordered_set<std::string> used(prev.representative.item_names.begin(), prev.representative.item_names.end());
                const auto& names_q = pool_.NamesForSize(q);
                std::vector<std::vector<DeckRep>> rep_jobs;
                for (const auto& item : names_q) {
                    if (used.count(item)) continue;
                    rep_jobs.push_back(BuildInsertionReps(prev.representative, item));
                }
                if (rep_jobs.empty()) continue;
                std::vector<DeckRep> picked_reps = RunBatchedKnockoutMany(std::move(rep_jobs), evaluator_, rng_,
                    [](const DeckRep& d) -> const DeckRep& { return d; });
                for (const auto& rep : picked_reps) {
                    CandidateState st;
                    st.combo_key = BuildComboKey(rep.item_names);
                    st.representative = rep;
                    st.size_sum = s;
                    candidate_buckets[st.combo_key].push_back(std::move(st));
                }
            }
        }
        if (tr) {
            GdfRunTiming::add_ns(tr->expand_and_pick_ns,
                std::chrono::duration_cast<std::chrono::nanoseconds>(SteadyClock::now() - t_expand_beg));
        }

        const SteadyClock::time_point t_resolve_beg = SteadyClock::now();
        auto candidates = ResolveConflictBuckets(candidate_buckets);
        if (tr) {
            GdfRunTiming::add_ns(tr->resolve_buckets_ns,
                std::chrono::duration_cast<std::chrono::nanoseconds>(SteadyClock::now() - t_resolve_beg));
        }
        if (candidates.empty()) throw std::runtime_error("no candidates at size " + std::to_string(s));

        const int rounds = static_cast<int>(std::ceil(std::log2(std::max(1, static_cast<int>(candidates.size())))));
        const int top_km = std::min(static_cast<int>(candidates.size()), config_.top_k * config_.top_multiplier);
        const SteadyClock::time_point t_swiss_beg = SteadyClock::now();
        auto stage1 = SwissTournament::RunSwissAndPickTop(std::move(candidates), rounds, top_km, evaluator_, rng_);
        if (tr) {
            GdfRunTiming::add_ns(tr->swiss_ns, std::chrono::duration_cast<std::chrono::nanoseconds>(SteadyClock::now() - t_swiss_beg));
        }

        const bool compute_anchor = config_.lambda_anchor > 0;
        std::vector<CandidateState> stage2;
        const SteadyClock::time_point t_post_beg = SteadyClock::now();
        if (config_.lambda_anchor == 0 && config_.mu_diversity == 0) {
            stage2 = SwissTournament::RunRoundRobinAndPickTop(std::move(stage1), config_.top_k, config_.best_of, evaluator_, rng_);
        } else {
            SwissTournament::RunRoundRobinAndAnchorMargin(stage1, config_.best_of, compute_anchor, seed_items_, evaluator_);
            stage2 = SwissTournament::GreedyPickByObjective(std::move(stage1), config_.top_k, config_.lambda_anchor, config_.mu_diversity,
                config_.diversity_exclude_seeds, seed_items_, rng_);
        }
        if (tr) {
            GdfRunTiming::add_ns(tr->post_swiss_stage_ns,
                std::chrono::duration_cast<std::chrono::nanoseconds>(SteadyClock::now() - t_post_beg));
        }

        if (s == max_size_sum) {
            const SteadyClock::time_point t_fp_beg = SteadyClock::now();
            stage2 = ResolveFinalTopTieByPlayoff(std::move(stage2));
            if (tr) {
                GdfRunTiming::add_ns(tr->final_playoff_ns,
                    std::chrono::duration_cast<std::chrono::nanoseconds>(SteadyClock::now() - t_fp_beg));
            }
        }

        top_by_size[s] = std::move(stage2);
        if (on_size_completed) on_size_completed(s, top_by_size[s]);
    }

    return top_by_size;
}

}  // namespace bazaararena::gdf
