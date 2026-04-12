#include <bazaararena/gdf/SwissTournament.hpp>

#include <bazaararena/gdf/DeckRep.hpp>

#include <algorithm>
#include <cmath>
#include <map>
#include <map>
#include <unordered_map>
#include <unordered_set>

namespace bazaararena::gdf {

int SwissTournament::RemoveSwissImpossible(std::vector<CandidateState*>& active, int top_count, int remaining_rounds) {
    if (static_cast<int>(active.size()) <= top_count || top_count <= 0) return 0;
    std::unordered_set<std::string> remove_keys;
    for (CandidateState* c : active) {
        const double max_final = c->swiss_score + static_cast<double>(std::max(0, remaining_rounds));
        int strictly_ahead = 0;
        for (CandidateState* d : active) {
            if (d == c) continue;
            if (d->swiss_score > max_final) strictly_ahead++;
        }
        if (strictly_ahead >= top_count) remove_keys.insert(c->combo_key);
    }
    if (remove_keys.empty()) return 0;
    const auto old_sz = active.size();
    active.erase(std::remove_if(active.begin(), active.end(),
                     [&](CandidateState* p) { return remove_keys.count(p->combo_key) > 0; }),
        active.end());
    return static_cast<int>(old_sz - active.size());
}

std::vector<CandidateState> SwissTournament::RunSwissAndPickTop(std::vector<CandidateState> candidates, int rounds, int top_count,
    BattleEvaluator& evaluator, std::mt19937& main_rng) {
    std::vector<CandidateState*> active;
    active.reserve(candidates.size());
    for (auto& c : candidates) {
        c.swiss_score = 0;
        c.played_opponents.clear();
        active.push_back(&c);
    }

    std::uniform_int_distribution<unsigned> uni;
    const unsigned swiss_seed = uni(main_rng);
    std::mt19937 swiss_rng(swiss_seed);

    rounds = std::max(0, rounds);
    for (int r = 0; r < rounds; r++) {
        const int remaining_inclusive = rounds - r;
        RemoveSwissImpossible(active, top_count, remaining_inclusive);

        if (static_cast<int>(active.size()) <= top_count) break;

        std::map<double, std::vector<CandidateState*>, std::greater<>> groups;
        for (CandidateState* p : active) groups[p->swiss_score].push_back(p);

        struct Match {
            CandidateState* a = nullptr;
            CandidateState* b = nullptr;
            bool score_both = false;
        };
        std::vector<Match> round_matches;
        round_matches.reserve(std::max<size_t>(4, active.size() / 2));

        for (auto& kv : groups) {
            auto& bucket = kv.second;
            std::shuffle(bucket.begin(), bucket.end(), swiss_rng);
            std::unordered_set<std::string> used;
            for (size_t i = 0; i < bucket.size(); i++) {
                CandidateState* a = bucket[i];
                if (!used.insert(a->combo_key).second) continue;

                CandidateState* b = nullptr;
                for (size_t j = i + 1; j < bucket.size(); j++) {
                    CandidateState* x = bucket[j];
                    if (used.count(x->combo_key)) continue;
                    if (a->played_opponents.count(x->combo_key)) continue;
                    b = x;
                    break;
                }

                if (b == nullptr) {
                    std::vector<CandidateState*> opps;
                    for (CandidateState* x : bucket) {
                        if (x->combo_key != a->combo_key) opps.push_back(x);
                    }
                    if (opps.empty()) continue;
                    std::uniform_int_distribution<size_t> pick(0, opps.size() - 1);
                    CandidateState* ghost = opps[pick(swiss_rng)];
                    round_matches.push_back({a, ghost, false});
                    continue;
                }

                used.insert(b->combo_key);
                a->played_opponents.insert(b->combo_key);
                b->played_opponents.insert(a->combo_key);
                round_matches.push_back({a, b, true});
            }
        }

        if (!round_matches.empty()) {
            std::vector<std::pair<DeckRep, DeckRep>> pair_list;
            pair_list.reserve(round_matches.size());
            for (const auto& m : round_matches) pair_list.emplace_back(m.a->representative, m.b->representative);
            std::vector<int> winners = evaluator.PlayBoNBatch(pair_list);
            for (size_t i = 0; i < round_matches.size(); i++) {
                const auto& m = round_matches[i];
                const int win = winners[i];
                if (m.score_both) {
                    if (win == 0) m.a->swiss_score += 1;
                    else m.b->swiss_score += 1;
                } else {
                    if (win == 0) m.a->swiss_score += 1;
                }
            }
        }
    }

    std::map<CandidateState*, unsigned> tie_break;
    for (CandidateState* p : active) tie_break[p] = uni(swiss_rng);
    std::sort(active.begin(), active.end(), [&](CandidateState* x, CandidateState* y) {
        if (x->swiss_score != y->swiss_score) return x->swiss_score > y->swiss_score;
        return tie_break[x] < tie_break[y];
    });

    std::vector<CandidateState> out;
    const int take = std::min(top_count, static_cast<int>(active.size()));
    out.reserve(static_cast<size_t>(take));
    for (int i = 0; i < take; i++) out.push_back(*active[static_cast<size_t>(i)]);
    return out;
}

void SwissTournament::RunRoundRobinAndAnchorMargin(std::vector<CandidateState>& candidates, int games_per_pair, bool compute_anchor,
    const std::unordered_set<std::string>& seed_names, BattleEvaluator& evaluator) {
    for (auto& c : candidates) {
        c.round_robin_score = 0;
        c.anchor_margin = 0;
    }
    const size_t n = candidates.size();
    if (n < 2) return;

    std::vector<std::pair<DeckRep, DeckRep>> base_pairs;
    for (size_t i = 0; i < n; i++) {
        for (size_t j = i + 1; j < n; j++) base_pairs.emplace_back(candidates[i].representative, candidates[j].representative);
    }
    std::vector<MatchPoints> base_pts = evaluator.PlaySeriesBatch(base_pairs, games_per_pair);
    size_t idx = 0;
    for (size_t i = 0; i < n; i++) {
        for (size_t j = i + 1; j < n; j++) {
            candidates[i].round_robin_score += base_pts[idx].a;
            candidates[j].round_robin_score += base_pts[idx].b;
            idx++;
        }
    }

    if (!compute_anchor) return;

    std::vector<std::pair<DeckRep, DeckRep>> aug1;
    std::vector<std::pair<DeckRep, DeckRep>> aug2;
    aug1.reserve(base_pairs.size());
    aug2.reserve(base_pairs.size());
    for (size_t i = 0; i < n; i++) {
        for (size_t j = i + 1; j < n; j++) {
            DeckRep di_prime = StripSeeds(candidates[i].representative, seed_names);
            DeckRep dj_prime = StripSeeds(candidates[j].representative, seed_names);
            aug1.emplace_back(std::move(di_prime), candidates[j].representative);
            aug2.emplace_back(std::move(dj_prime), candidates[i].representative);
        }
    }
    std::vector<MatchPoints> p1 = evaluator.PlaySeriesBatch(aug1, games_per_pair);
    std::vector<MatchPoints> p2 = evaluator.PlaySeriesBatch(aug2, games_per_pair);
    idx = 0;
    for (size_t i = 0; i < n; i++) {
        for (size_t j = i + 1; j < n; j++) {
            candidates[i].anchor_margin += base_pts[idx].a - p1[idx].a;
            candidates[j].anchor_margin += base_pts[idx].b - p2[idx].a;
            idx++;
        }
    }
}

std::vector<std::string> SwissTournament::FilterSeedsCopy(const std::vector<std::string>& items, const std::unordered_set<std::string>& seeds) {
    std::vector<std::string> o;
    for (const auto& s : items) {
        if (!seeds.count(s)) o.push_back(s);
    }
    return o;
}

double SwissTournament::JaccardSimilarity(const std::vector<std::string>& a, const std::vector<std::string>& b) {
    std::unordered_set<std::string> sa(a.begin(), a.end());
    std::unordered_set<std::string> sb(b.begin(), b.end());
    if (sa.empty() && sb.empty()) return 0;
    size_t inter = 0;
    for (const auto& x : sa) {
        if (sb.count(x)) inter++;
    }
    const size_t uni = sa.size() + sb.size() - inter;
    if (uni == 0) return 0;
    return static_cast<double>(inter) / static_cast<double>(uni);
}

std::vector<CandidateState> SwissTournament::GreedyPickByObjective(std::vector<CandidateState> candidates, int top_k, double lambda_anchor,
    double mu_diversity, bool diversity_exclude_seeds, const std::unordered_set<std::string>& seed_names, std::mt19937& rng) {
    std::vector<CandidateState> selected;
    std::vector<char> taken(candidates.size(), 0);
    std::uniform_int_distribution<unsigned> uni;

    while (static_cast<int>(selected.size()) < top_k) {
        double best_obj = -1e300;
        std::vector<size_t> tied;
        for (size_t i = 0; i < candidates.size(); i++) {
            if (taken[i]) continue;
            double max_sim = 0;
            for (const auto& s : selected) {
                std::vector<std::string> va = diversity_exclude_seeds
                    ? FilterSeedsCopy(candidates[i].representative.item_names, seed_names)
                    : candidates[i].representative.item_names;
                std::vector<std::string> vb =
                    diversity_exclude_seeds ? FilterSeedsCopy(s.representative.item_names, seed_names) : s.representative.item_names;
                max_sim = std::max(max_sim, JaccardSimilarity(va, vb));
            }
            const double obj =
                candidates[i].round_robin_score + lambda_anchor * candidates[i].anchor_margin - mu_diversity * max_sim;
            if (obj > best_obj + 1e-12) {
                best_obj = obj;
                tied.clear();
                tied.push_back(i);
            } else if (std::abs(obj - best_obj) <= 1e-12) {
                tied.push_back(i);
            }
        }
        if (tied.empty()) break;
        size_t pick = tied[0];
        if (tied.size() > 1) {
            double best_sw = -1e300;
            std::vector<size_t> tied2;
            for (size_t ti : tied) {
                if (candidates[ti].swiss_score > best_sw + 1e-12) {
                    best_sw = candidates[ti].swiss_score;
                    tied2.clear();
                    tied2.push_back(ti);
                } else if (std::abs(candidates[ti].swiss_score - best_sw) <= 1e-12) {
                    tied2.push_back(ti);
                }
            }
            std::uniform_int_distribution<size_t> pickd(0, tied2.size() - 1);
            pick = tied2[pickd(rng)];
        }
        taken[pick] = 1;
        selected.push_back(std::move(candidates[pick]));
    }
    return selected;
}

}  // namespace bazaararena::gdf
