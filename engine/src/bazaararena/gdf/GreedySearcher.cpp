#include <bazaararena/gdf/GreedySearcher.hpp>

#include <bazaararena/data/ItemDatabase.hpp>
#include <bazaararena/gdf/DeckRep.hpp>
#include <bazaararena/gdf/GdfLevelRules.hpp>
#include <bazaararena/gdf/SwissTournament.hpp>

#include <algorithm>
#include <cmath>
#include <stdexcept>
#include <unordered_map>

namespace bazaararena::gdf {
namespace {

/// 对「已给定的一组槽位顺序」（例如同 multiset 下的插入位变体，或多路径合并后的各路代表）做循环赛取代表；并列两人 BoN，多于两人则对并列子集递归，深度上限后备字典序最小 Signature。
static DeckRep PickBestByRoundRobinAmongDeckRepsImpl(const std::vector<DeckRep>& reps, BattleEvaluator& eval, int games_per_pair, int depth) {
    if (reps.empty()) throw std::runtime_error("PickBestByRoundRobinAmongDeckReps: empty");
    if (reps.size() == 1) return reps[0];

    std::vector<std::pair<DeckRep, DeckRep>> pairs;
    const size_t m = reps.size();
    for (size_t i = 0; i < m; i++) {
        for (size_t j = i + 1; j < m; j++) pairs.emplace_back(reps[i], reps[j]);
    }
    const auto pts = eval.PlaySeriesBatch(pairs, games_per_pair);
    std::vector<double> score(m, 0);
    size_t idx = 0;
    for (size_t i = 0; i < m; i++) {
        for (size_t j = i + 1; j < m; j++, idx++) {
            score[i] += pts[idx].a;
            score[j] += pts[idx].b;
        }
    }
    double top_s = score[0];
    for (size_t i = 1; i < m; i++) top_s = std::max(top_s, score[i]);
    std::vector<size_t> tops;
    for (size_t i = 0; i < m; i++) {
        if (std::abs(score[i] - top_s) <= 1e-9) tops.push_back(i);
    }
    if (tops.size() == 1) return reps[tops[0]];
    if (tops.size() == 2) {
        const int w = eval.PlayBoN(reps[tops[0]], reps[tops[1]]);
        return w == 0 ? reps[tops[0]] : reps[tops[1]];
    }
    constexpr int kMaxTieMergeDepth = 12;
    if (depth < kMaxTieMergeDepth) {
        std::vector<DeckRep> sub;
        sub.reserve(tops.size());
        for (size_t ti : tops) sub.push_back(reps[ti]);
        return PickBestByRoundRobinAmongDeckRepsImpl(sub, eval, games_per_pair, depth + 1);
    }
    size_t pick = tops[0];
    for (size_t t = 1; t < tops.size(); t++) {
        const size_t ti = tops[t];
        if (reps[ti].Signature() < reps[pick].Signature()) pick = ti;
    }
    return reps[pick];
}

static DeckRep PickBestByRoundRobinAmongDeckReps(const std::vector<DeckRep>& reps, BattleEvaluator& eval, int games_per_pair) {
    return PickBestByRoundRobinAmongDeckRepsImpl(reps, eval, games_per_pair, 0);
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

    std::vector<CandidateState> out;
    out.reserve(entries.size());
    for (auto& [combo_key, bucket] : entries) {
        (void)combo_key;
        if (bucket.empty()) continue;
        if (bucket.size() == 1) {
            CandidateState one = std::move(bucket[0]);
            out.push_back(std::move(one));
            continue;
        }
        std::vector<DeckRep> reps;
        reps.reserve(bucket.size());
        for (const auto& c : bucket) reps.push_back(c.representative);
        DeckRep best = PickBestByRoundRobinAmongDeckReps(reps, evaluator_, config_.best_of);
        CandidateState merged;
        merged.combo_key = bucket[0].combo_key;
        merged.size_sum = bucket[0].size_sum;
        merged.representative = std::move(best);
        out.push_back(std::move(merged));
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
                std::vector<DeckRep> picked_reps;
                picked_reps.reserve(rep_jobs.size());
                for (const auto& insertions : rep_jobs) {
                    DeckRep pick = PickBestByRoundRobinAmongDeckReps(insertions, evaluator_, config_.best_of);
                    picked_reps.push_back(std::move(pick));
                }
                for (const auto& rep : picked_reps) {
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
        std::vector<CandidateState> stage2;
        if (config_.lambda_anchor == 0 && config_.mu_diversity == 0) {
            stage2 = SwissTournament::RunRoundRobinAndPickTop(std::move(stage1), config_.top_k, config_.best_of, evaluator_, rng_);
        } else {
            SwissTournament::RunRoundRobinAndAnchorMargin(stage1, config_.best_of, compute_anchor, seed_items_, evaluator_);
            stage2 = SwissTournament::GreedyPickByObjective(std::move(stage1), config_.top_k, config_.lambda_anchor, config_.mu_diversity,
                config_.diversity_exclude_seeds, seed_items_, rng_);
        }

        if (s == max_size_sum) stage2 = ResolveFinalTopTieByPlayoff(std::move(stage2));

        top_by_size[s] = std::move(stage2);
        if (on_size_completed) on_size_completed(s, top_by_size[s]);
    }

    return top_by_size;
}

}  // namespace bazaararena::gdf
