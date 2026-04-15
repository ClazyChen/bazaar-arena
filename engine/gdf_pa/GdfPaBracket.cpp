#include <bazaararena/gdf_pa/GdfPaBracket.hpp>

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <functional>
#include <optional>
#include <stdexcept>
#include <unordered_map>
#include <vector>

namespace bazaararena::gdf_pa {
namespace {

static int NextPow2(int n) {
    int p = 1;
    while (p < n) p <<= 1;
    return p;
}

static void GenerateSeedPairs(int bracket_size, std::vector<std::pair<int, int>>& out) {
    out.clear();
    std::function<void(const std::vector<int>&)> build = [&](const std::vector<int>& positions) {
        if (positions.size() == 2) {
            out.push_back({positions[0], positions[1]});
            return;
        }
        const int half = static_cast<int>(positions.size()) / 2;
        std::vector<int> upper(positions.begin(), positions.begin() + half);
        std::vector<int> lower(positions.begin() + half, positions.end());
        for (int i = 0; i < half / 2; ++i) {
            build({upper[static_cast<size_t>(i)], lower[static_cast<size_t>(half - 1 - i)]});
            build({upper[static_cast<size_t>(half - 1 - i)], lower[static_cast<size_t>(i)]});
        }
    };
    std::vector<int> seeds(static_cast<size_t>(bracket_size));
    for (int i = 0; i < bracket_size; ++i) seeds[static_cast<size_t>(i)] = i + 1;
    build(seeds);
}

static int LosersMatchCount(int bracket_size, int round) {
    const int exp = static_cast<int>(std::ceil(static_cast<double>(round) / 2.0)) + 1;
    return bracket_size / (1 << exp);
}

struct BrMatch {
    bool winners_bracket = true;
    int round = 0;
    int pos = 0;
    int w_to = -1;
    int w_slot = 0;
    int l_to = -1;
    int l_slot = 0;
    std::optional<int> reg[2];
    bool played = false;
    std::optional<int> match_winner;
};

static long long Key(int round, int pos) {
    return (static_cast<long long>(round) << 32) ^ static_cast<uint32_t>(pos);
}

struct LoseRoute {
    int lb_round = 0;
    int lb_pos = 0;
    int slot_js = 1;
};

static std::optional<LoseRoute> GetLoserDestination(int wb_round, int wb_position, int total_wb_rounds) {
    if (wb_round == total_wb_rounds) return std::nullopt;
    if (wb_round == 1) {
        LoseRoute r;
        r.lb_round = 1;
        r.lb_pos = wb_position / 2;
        r.slot_js = (wb_position % 2) + 1;
        return r;
    }
    if (wb_round == 2) {
        LoseRoute r;
        r.lb_round = 2;
        r.lb_pos = wb_position;
        r.slot_js = 2;
        return r;
    }
    LoseRoute r;
    r.lb_round = (wb_round - 2) * 2 + 2;
    r.lb_pos = wb_position;
    r.slot_js = 2;
    return r;
}

static bool RunAllMatches(std::vector<BrMatch>& ms, const std::vector<bazaararena::gdf::DeckRep>& decks,
    bazaararena::gdf::BattleEvaluator& eval) {
    auto propagate = [&](BrMatch& m, int winner_idx, int loser_idx) {
        m.match_winner = winner_idx;
        if (m.w_to >= 0) {
            BrMatch& n = ms[static_cast<size_t>(m.w_to)];
            n.reg[static_cast<size_t>(m.w_slot)] = winner_idx;
        }
        if (m.l_to >= 0 && loser_idx >= 0) {
            BrMatch& n = ms[static_cast<size_t>(m.l_to)];
            n.reg[static_cast<size_t>(m.l_slot)] = loser_idx;
        }
        m.played = true;
    };

    auto try_bye = [&]() -> bool {
        bool any = false;
        for (BrMatch& m : ms) {
            if (m.played) continue;
            const bool h0 = m.reg[0].has_value();
            const bool h1 = m.reg[1].has_value();
            if (h0 == h1) continue;
            const int win = h0 ? *m.reg[0] : *m.reg[1];
            propagate(m, win, -1);
            any = true;
        }
        return any;
    };

    for (;;) {
        while (try_bye()) {}
        bool played_real = false;
        for (BrMatch& m : ms) {
            if (m.played) continue;
            if (!m.reg[0].has_value() || !m.reg[1].has_value()) continue;
            const int a = *m.reg[0];
            const int b = *m.reg[1];
            const int w = eval.PlayBoN(decks[static_cast<size_t>(a)], decks[static_cast<size_t>(b)]);
            const int win_idx = (w == 0) ? a : b;
            const int lose_idx = (w == 0) ? b : a;
            propagate(m, win_idx, lose_idx);
            played_real = true;
            break;
        }
        if (played_real) continue;
        if (!try_bye()) break;
    }

    for (const BrMatch& m : ms) {
        if (!m.played) return false;
    }
    return true;
}

}  // namespace

int RunClusterDoubleElimination(const std::vector<bazaararena::gdf::DeckRep>& decks, bazaararena::gdf::BattleEvaluator& eval) {
    const int m = static_cast<int>(decks.size());
    if (m <= 1) return 0;

    const int bracket_size = NextPow2(m);
    const int winners_rounds = static_cast<int>(std::log2(static_cast<double>(bracket_size)));
    const int losers_rounds = std::max(0, (winners_rounds - 1) * 2 - 1);

    std::vector<BrMatch> ms;
    std::unordered_map<long long, int> wb_id;
    std::unordered_map<long long, int> lb_id;

    for (int round = 1; round <= winners_rounds; ++round) {
        const int mc = bracket_size / (1 << round);
        for (int pos = 0; pos < mc; ++pos) {
            BrMatch bm;
            bm.winners_bracket = true;
            bm.round = round;
            bm.pos = pos;
            const int idx = static_cast<int>(ms.size());
            wb_id[Key(round, pos)] = idx;
            ms.push_back(bm);
        }
    }
    if (losers_rounds > 0) {
        for (int round = 1; round <= losers_rounds; ++round) {
            const int mc = LosersMatchCount(bracket_size, round);
            for (int pos = 0; pos < mc; ++pos) {
                BrMatch bm;
                bm.winners_bracket = false;
                bm.round = round;
                bm.pos = pos;
                const int idx = static_cast<int>(ms.size());
                lb_id[Key(round, pos)] = idx;
                ms.push_back(bm);
            }
        }
    }

    for (BrMatch& m : ms) {
        if (!m.winners_bracket) continue;
        if (m.round >= winners_rounds) continue;
        const int nround = m.round + 1;
        const int npos = m.pos / 2;
        m.w_to = wb_id[Key(nround, npos)];
        m.w_slot = m.pos % 2;
        const auto lr = GetLoserDestination(m.round, m.pos, winners_rounds);
        if (lr.has_value()) {
            const auto it = lb_id.find(Key(lr->lb_round, lr->lb_pos));
            if (it != lb_id.end()) {
                m.l_to = it->second;
                m.l_slot = lr->slot_js - 1;
            }
        }
    }

    if (losers_rounds > 0) {
    for (BrMatch& m : ms) {
        if (m.winners_bracket) continue;
        if (m.round >= losers_rounds) continue;
        const bool odd_round = (m.round % 2 == 1);
        if (odd_round) {
            m.w_to = lb_id[Key(m.round + 1, m.pos)];
            m.w_slot = 0;
        } else {
            m.w_to = lb_id[Key(m.round + 1, m.pos / 2)];
            m.w_slot = m.pos % 2;
        }
    }
    }

    std::vector<std::pair<int, int>> seed_pairs;
    GenerateSeedPairs(bracket_size, seed_pairs);
    const int r1_matches = bracket_size / 2;
    for (int i = 0; i < r1_matches; ++i) {
        const int mid = wb_id[Key(1, i)];
        BrMatch& mm = ms[static_cast<size_t>(mid)];
        auto seed_to = [&](int seed) -> std::optional<int> {
            if (seed >= 1 && seed <= m) return seed - 1;
            return std::nullopt;
        };
        mm.reg[0] = seed_to(seed_pairs[static_cast<size_t>(i)].first);
        mm.reg[1] = seed_to(seed_pairs[static_cast<size_t>(i)].second);
    }

    if (!RunAllMatches(ms, decks, eval)) {
        throw std::runtime_error("GdfPaBracket: bracket did not complete (wiring bug?)");
    }

    const int ub_mid = wb_id[Key(winners_rounds, 0)];
    const BrMatch& ub_final = ms[static_cast<size_t>(ub_mid)];
    if (!ub_final.match_winner.has_value()) {
        throw std::runtime_error("GdfPaBracket: UB final missing winner");
    }
    const int ub_champ = *ub_final.match_winner;

    if (losers_rounds <= 0) return ub_champ;

    const int lb_last_round = losers_rounds;
    const int lb_last_mc = LosersMatchCount(bracket_size, lb_last_round);
    if (lb_last_mc <= 0) return ub_champ;
    const int lb_mid = lb_id[Key(lb_last_round, 0)];
    const BrMatch& lb_final = ms[static_cast<size_t>(lb_mid)];
    if (!lb_final.match_winner.has_value()) {
        return ub_champ;
    }
    const int lb_champ = *lb_final.match_winner;

    if (lb_champ < 0 || lb_champ == ub_champ) return ub_champ;

    const int g1 = eval.PlayBoN(decks[static_cast<size_t>(ub_champ)], decks[static_cast<size_t>(lb_champ)]);
    if (g1 == 0) return ub_champ;
    const int g2 = eval.PlayBoN(decks[static_cast<size_t>(ub_champ)], decks[static_cast<size_t>(lb_champ)]);
    return g2 == 0 ? ub_champ : lb_champ;
}

int RunClusterRepresentative(const std::vector<bazaararena::gdf::DeckRep>& decks, bazaararena::gdf::BattleEvaluator& eval) {
    const int m = static_cast<int>(decks.size());
    if (m <= 1) return 0;
    if (m == 2) {
        return eval.PlayBoN(decks[0], decks[1]) == 0 ? 0 : 1;
    }
    if (m <= 8) {
        std::vector<int> wins(static_cast<size_t>(m), 0);
        for (int i = 0; i < m; ++i) {
            for (int j = i + 1; j < m; ++j) {
                const int r = eval.PlayBoN(decks[static_cast<size_t>(i)], decks[static_cast<size_t>(j)]);
                if (r == 0) wins[static_cast<size_t>(i)]++;
                else wins[static_cast<size_t>(j)]++;
            }
        }
        int best = 0;
        for (int i = 1; i < m; ++i) {
            if (wins[static_cast<size_t>(i)] > wins[static_cast<size_t>(best)]) best = i;
        }
        return best;
    }
    if (m == 3) {
        int wins[3] = {0, 0, 0};
        auto play = [&](int a, int b) {
            const int r = eval.PlayBoN(decks[static_cast<size_t>(a)], decks[static_cast<size_t>(b)]);
            if (r == 0) wins[a]++;
            else wins[b]++;
        };
        play(0, 1);
        play(0, 2);
        play(1, 2);
        const int mx = std::max({wins[0], wins[1], wins[2]});
        int cnt = 0;
        int sole = -1;
        for (int i = 0; i < 3; ++i) {
            if (wins[i] == mx) {
                cnt++;
                sole = i;
            }
        }
        if (cnt == 1) return sole;
        if (cnt == 2) {
            int a = -1, b = -1;
            for (int i = 0; i < 3; ++i) {
                if (wins[i] == mx) {
                    if (a < 0) a = i;
                    else b = i;
                }
            }
            return eval.PlayBoN(decks[static_cast<size_t>(a)], decks[static_cast<size_t>(b)]) == 0 ? a : b;
        }
        const int w01 = eval.PlayBoN(decks[0], decks[1]);
        const int mid = (w01 == 0) ? 0 : 1;
        return eval.PlayBoN(decks[static_cast<size_t>(mid)], decks[2]) == 0 ? mid : 2;
    }
    return RunClusterDoubleElimination(decks, eval);
}

}  // namespace bazaararena::gdf_pa
