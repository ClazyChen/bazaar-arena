#include <bazaararena/gdf/GreedySearcher.hpp>

#include <bazaararena/data/ItemDatabase.hpp>
#include <bazaararena/gdf/DeckRep.hpp>
#include <bazaararena/gdf/GdfLevelRules.hpp>
#include <bazaararena/gdf/SwissTournament.hpp>

#include <cmath>
#include <stdexcept>
#include <unordered_map>

namespace bazaararena::gdf {

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

std::vector<DeckRep> GreedySearcher::RunDeckKnockoutMany(std::vector<std::vector<DeckRep>> sources) {
    struct Tourn {
        int index = 0;
        std::vector<DeckRep> alive;
    };
    std::vector<Tourn> tournaments;
    tournaments.reserve(sources.size());
    for (int i = 0; i < static_cast<int>(sources.size()); i++) {
        auto alive = std::move(sources[static_cast<size_t>(i)]);
        std::shuffle(alive.begin(), alive.end(), rng_);
        tournaments.push_back({i, std::move(alive)});
    }

    while (true) {
        std::vector<std::pair<DeckRep, DeckRep>> pairs;
        std::vector<std::tuple<int, size_t, size_t>> pair_members;

        int alive_tournament_count = 0;
        for (auto& t : tournaments) {
            if (t.alive.size() <= 1) continue;
            alive_tournament_count++;
            for (size_t i = 0; i + 1 < t.alive.size(); i += 2) {
                pair_members.emplace_back(t.index, i, i + 1);
                pairs.emplace_back(t.alive[i], t.alive[i + 1]);
            }
        }
        if (alive_tournament_count == 0) break;
        if (pairs.empty()) break;

        std::vector<int> winners = evaluator_.PlayBoNBatch(pairs);

        std::vector<std::vector<DeckRep>> next_by_t(tournaments.size());
        for (size_t pi = 0; pi < pairs.size(); pi++) {
            const auto& [t_idx, ia, ib] = pair_members[pi];
            const int w = winners[pi];
            auto& al = tournaments[static_cast<size_t>(t_idx)].alive;
            next_by_t[static_cast<size_t>(t_idx)].push_back(w == 0 ? al[ia] : al[ib]);
        }
        for (auto& t : tournaments) {
            if (t.alive.size() <= 1) continue;
            auto& nxt = next_by_t[static_cast<size_t>(t.index)];
            if ((t.alive.size() & 1u) != 0u) nxt.push_back(t.alive.back());
            t.alive = std::move(nxt);
        }
    }

    std::vector<DeckRep> result(tournaments.size());
    for (size_t i = 0; i < tournaments.size(); i++) result[i] = std::move(tournaments[i].alive[0]);
    return result;
}

std::vector<CandidateState> GreedySearcher::RunCandidateKnockoutMany(std::vector<std::vector<CandidateState>> sources) {
    struct Tourn {
        int index = 0;
        std::vector<CandidateState> alive;
    };
    std::vector<Tourn> tournaments;
    tournaments.reserve(sources.size());
    for (int i = 0; i < static_cast<int>(sources.size()); i++) {
        auto alive = std::move(sources[static_cast<size_t>(i)]);
        std::shuffle(alive.begin(), alive.end(), rng_);
        tournaments.push_back({i, std::move(alive)});
    }

    while (true) {
        std::vector<std::pair<DeckRep, DeckRep>> pairs;
        std::vector<std::tuple<int, size_t, size_t>> pair_members;

        int alive_tournament_count = 0;
        for (auto& t : tournaments) {
            if (t.alive.size() <= 1) continue;
            alive_tournament_count++;
            for (size_t i = 0; i + 1 < t.alive.size(); i += 2) {
                pair_members.emplace_back(t.index, i, i + 1);
                pairs.emplace_back(t.alive[i].representative, t.alive[i + 1].representative);
            }
        }
        if (alive_tournament_count == 0) break;
        if (pairs.empty()) break;

        std::vector<int> winners = evaluator_.PlayBoNBatch(pairs);

        std::vector<std::vector<CandidateState>> next_by_t(tournaments.size());
        for (size_t pi = 0; pi < pairs.size(); pi++) {
            const auto& [t_idx, ia, ib] = pair_members[pi];
            const int w = winners[pi];
            auto& al = tournaments[static_cast<size_t>(t_idx)].alive;
            next_by_t[static_cast<size_t>(t_idx)].push_back(w == 0 ? al[ia] : al[ib]);
        }
        for (auto& t : tournaments) {
            if (t.alive.size() <= 1) continue;
            auto& nxt = next_by_t[static_cast<size_t>(t.index)];
            if ((t.alive.size() & 1u) != 0u) nxt.push_back(t.alive.back());
            t.alive = std::move(nxt);
        }
    }

    std::vector<CandidateState> out;
    out.reserve(tournaments.size());
    for (auto& t : tournaments) out.push_back(std::move(t.alive[0]));
    return out;
}

std::vector<CandidateState> GreedySearcher::ResolveConflictBuckets(std::unordered_map<std::string, std::vector<CandidateState>>& buckets) {
    std::vector<CandidateState> singletons;
    std::vector<std::vector<CandidateState>> multi;
    for (auto& kv : buckets) {
        if (kv.second.size() == 1) singletons.push_back(std::move(kv.second[0]));
        else multi.push_back(std::move(kv.second));
    }
    if (multi.empty()) return singletons;
    auto winners = RunCandidateKnockoutMany(std::move(multi));
    singletons.insert(singletons.end(), std::make_move_iterator(winners.begin()), std::make_move_iterator(winners.end()));
    return singletons;
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
        std::unordered_map<std::string, std::vector<CandidateState>> candidate_buckets;

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
                std::vector<DeckRep> reps = RunDeckKnockoutMany(std::move(rep_jobs));
                for (const auto& rep : reps) {
                    CandidateState st;
                    st.combo_key = BuildComboKey(rep.item_names);
                    st.representative = rep;
                    st.size_sum = s;
                    candidate_buckets[st.combo_key].push_back(std::move(st));
                }
            }
        }

        auto candidates = ResolveConflictBuckets(candidate_buckets);
        if (candidates.empty()) throw std::runtime_error("no candidates at size " + std::to_string(s));

        const int rounds = static_cast<int>(std::ceil(std::log2(std::max(1, static_cast<int>(candidates.size())))));
        const int top_km = std::min(static_cast<int>(candidates.size()), config_.top_k * config_.top_multiplier);
        auto stage1 = SwissTournament::RunSwissAndPickTop(std::move(candidates), rounds, top_km, evaluator_, rng_);

        const bool compute_anchor = config_.lambda_anchor > 0;
        SwissTournament::RunRoundRobinAndAnchorMargin(stage1, config_.best_of, compute_anchor, seed_items_, evaluator_);

        std::vector<CandidateState> stage2 = SwissTournament::GreedyPickByObjective(std::move(stage1), config_.top_k, config_.lambda_anchor,
            config_.mu_diversity, config_.diversity_exclude_seeds, seed_items_, rng_);

        if (s == max_size_sum) stage2 = ResolveFinalTopTieByPlayoff(std::move(stage2));

        top_by_size[s] = std::move(stage2);
        if (on_size_completed) on_size_completed(s, top_by_size[s]);
    }

    return top_by_size;
}

}  // namespace bazaararena::gdf
