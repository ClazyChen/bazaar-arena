#include <bazaararena/gdf/BattleEvaluator.hpp>
#include <bazaararena/gdf_pa/GdfPaRoundRobin.hpp>
#include <bazaararena/gdf_pa/GdfPaCsv.hpp>

#include <algorithm>
#include <cmath>
#include <fstream>
#include <iomanip>
#include <numeric>

namespace bazaararena::gdf_pa {

namespace {

constexpr double kPtsEps = 1e-12;

}  // namespace

void RunBoNRoundRobin(const std::vector<bazaararena::gdf::DeckRep>& decks, bazaararena::gdf::BattleEvaluator& eval, std::vector<int>& wins_out) {
    const int n = static_cast<int>(decks.size());
    wins_out.assign(static_cast<size_t>(n), 0);
    if (n <= 1) return;

    std::vector<std::pair<bazaararena::gdf::DeckRep, bazaararena::gdf::DeckRep>> pairs;
    const size_t pc = static_cast<size_t>(n) * static_cast<size_t>(n - 1) / 2;
    pairs.reserve(pc);
    for (int i = 0; i < n; ++i) {
        for (int j = i + 1; j < n; ++j) {
            pairs.push_back({decks[static_cast<size_t>(i)], decks[static_cast<size_t>(j)]});
        }
    }
    const std::vector<int> res = eval.PlayBoNBatch(pairs);
    size_t k = 0;
    for (int i = 0; i < n; ++i) {
        for (int j = i + 1; j < n; ++j) {
            const int w = res[k++];
            if (w == 0) wins_out[static_cast<size_t>(i)]++;
            else wins_out[static_cast<size_t>(j)]++;
        }
    }
}

void RunPeelSourcesRoundRobin(const std::vector<bazaararena::gdf::DeckRep>& decks, bazaararena::gdf::BattleEvaluator& eval, int series_games,
    std::vector<double>& total_points_out, std::vector<int>& matchup_wins_out, std::vector<int>& matchup_draws_out,
    std::vector<int>& matchup_losses_out, std::vector<std::vector<double>>* symm_delta_out) {
    const int n = static_cast<int>(decks.size());
    const int games = std::max(1, series_games);
    total_points_out.assign(static_cast<size_t>(n), 0.0);
    matchup_wins_out.assign(static_cast<size_t>(n), 0);
    matchup_draws_out.assign(static_cast<size_t>(n), 0);
    matchup_losses_out.assign(static_cast<size_t>(n), 0);
    if (symm_delta_out != nullptr) {
        symm_delta_out->assign(static_cast<size_t>(n), std::vector<double>(static_cast<size_t>(n), 0.0));
    }
    if (n <= 1) return;

    std::vector<std::pair<bazaararena::gdf::DeckRep, bazaararena::gdf::DeckRep>> pairs;
    const size_t pc = static_cast<size_t>(n) * static_cast<size_t>(n - 1) / 2;
    pairs.reserve(pc);
    for (int i = 0; i < n; ++i) {
        for (int j = i + 1; j < n; ++j) {
            pairs.push_back({decks[static_cast<size_t>(i)], decks[static_cast<size_t>(j)]});
        }
    }
    const std::vector<bazaararena::gdf::MatchPoints> series = eval.PlaySeriesBatch(pairs, games);
    size_t k = 0;
    for (int i = 0; i < n; ++i) {
        for (int j = i + 1; j < n; ++j) {
            const bazaararena::gdf::MatchPoints& m = series[k++];
            const double dab = m.a - m.b;
            if (symm_delta_out != nullptr) {
                (*symm_delta_out)[static_cast<size_t>(i)][static_cast<size_t>(j)] = dab;
                (*symm_delta_out)[static_cast<size_t>(j)][static_cast<size_t>(i)] = -dab;
            }
            // BO_N 中每局分总和恒为 1，因此一场对决两边总局分之和恒为 games：
            // points_i = (games + (points_i - points_j)) / 2
            const double pi_pts = (static_cast<double>(games) + dab) * 0.5;
            const double pj_pts = static_cast<double>(games) - pi_pts;
            total_points_out[static_cast<size_t>(i)] += pi_pts;
            total_points_out[static_cast<size_t>(j)] += pj_pts;
            if (dab > kPtsEps) {
                matchup_wins_out[static_cast<size_t>(i)]++;
                matchup_losses_out[static_cast<size_t>(j)]++;
            } else if (dab < -kPtsEps) {
                matchup_wins_out[static_cast<size_t>(j)]++;
                matchup_losses_out[static_cast<size_t>(i)]++;
            } else {
                matchup_draws_out[static_cast<size_t>(i)]++;
                matchup_draws_out[static_cast<size_t>(j)]++;
            }
        }
    }
}

std::vector<int> FilterWinlessStrictIterative(const std::vector<std::vector<double>>& symm_delta) {
    const int n = static_cast<int>(symm_delta.size());
    if (n == 0) return {};
    std::vector<bool> alive(static_cast<size_t>(n), true);
    while (true) {
        int ac = 0;
        for (int i = 0; i < n; ++i) {
            if (alive[static_cast<size_t>(i)]) ac++;
        }
        if (ac <= 1) break;

        std::vector<int> remove;
        for (int v = 0; v < n; ++v) {
            if (!alive[static_cast<size_t>(v)]) continue;
            bool any_win = false;
            for (int u = 0; u < n; ++u) {
                if (u == v || !alive[static_cast<size_t>(u)]) continue;
                if (symm_delta[static_cast<size_t>(v)][static_cast<size_t>(u)] > kPtsEps) any_win = true;
            }
            if (!any_win) remove.push_back(v);
        }
        if (remove.empty()) break;
        if (static_cast<int>(remove.size()) == ac) break;

        for (int v : remove) alive[static_cast<size_t>(v)] = false;
    }
    std::vector<int> kept;
    kept.reserve(static_cast<size_t>(n));
    for (int i = 0; i < n; ++i) {
        if (alive[static_cast<size_t>(i)]) kept.push_back(i);
    }
    return kept;
}

void AggregateLeagueFromDeltaSubset(const std::vector<int>& subset_idx, const std::vector<std::vector<double>>& symm_delta, int series_games,
    std::vector<double>& total_points_out, std::vector<int>& matchup_wins_out, std::vector<int>& matchup_draws_out,
    std::vector<int>& matchup_losses_out) {
    const int k = static_cast<int>(subset_idx.size());
    const int games = std::max(1, series_games);
    total_points_out.assign(static_cast<size_t>(k), 0.0);
    matchup_wins_out.assign(static_cast<size_t>(k), 0);
    matchup_draws_out.assign(static_cast<size_t>(k), 0);
    matchup_losses_out.assign(static_cast<size_t>(k), 0);
    for (int pi = 0; pi < k; ++pi) {
        const int gi = subset_idx[static_cast<size_t>(pi)];
        for (int pj = 0; pj < k; ++pj) {
            if (pi == pj) continue;
            const int gj = subset_idx[static_cast<size_t>(pj)];
            const double dab = symm_delta[static_cast<size_t>(gi)][static_cast<size_t>(gj)];
            const double pi_pts = (static_cast<double>(games) + dab) * 0.5;
            total_points_out[static_cast<size_t>(pi)] += pi_pts;
            if (dab > kPtsEps) {
                matchup_wins_out[static_cast<size_t>(pi)]++;
            } else if (dab < -kPtsEps) {
                matchup_losses_out[static_cast<size_t>(pi)]++;
            } else {
                matchup_draws_out[static_cast<size_t>(pi)]++;
            }
        }
    }
}

std::vector<QualityLeagueStanding> BuildQualityLeagueStandings(const std::vector<bazaararena::gdf::DeckRep>& decks, const std::vector<double>& total_points,
    const std::vector<int>& matchup_wins, const std::vector<int>& matchup_draws, const std::vector<int>& matchup_losses) {
    const int n = static_cast<int>(decks.size());
    std::vector<int> ord(static_cast<size_t>(n));
    std::iota(ord.begin(), ord.end(), 0);
    std::sort(ord.begin(), ord.end(), [&](int a, int b) {
        const double pa = total_points[static_cast<size_t>(a)];
        const double pb = total_points[static_cast<size_t>(b)];
        if (std::abs(pa - pb) > kPtsEps) return pa > pb;
        return decks[static_cast<size_t>(a)].Signature() < decks[static_cast<size_t>(b)].Signature();
    });

    std::vector<QualityLeagueStanding> out;
    out.reserve(static_cast<size_t>(n));
    int r = 1;
    for (int i = 0; i < n; ++i) {
        const int idx = ord[static_cast<size_t>(i)];
        if (i > 0) {
            const int pi = ord[static_cast<size_t>(i - 1)];
            const double cur = total_points[static_cast<size_t>(idx)];
            const double prev = total_points[static_cast<size_t>(pi)];
            if (std::abs(cur - prev) > kPtsEps) r = i + 1;
        }
        QualityLeagueStanding row;
        row.final_rank = r;
        row.signature = decks[static_cast<size_t>(idx)].Signature();
        row.total_points = total_points[static_cast<size_t>(idx)];
        row.matchup_wins = matchup_wins[static_cast<size_t>(idx)];
        row.matchup_draws = matchup_draws[static_cast<size_t>(idx)];
        row.matchup_losses = matchup_losses[static_cast<size_t>(idx)];
        out.push_back(std::move(row));
    }
    return out;
}

bool WriteQualityRankingCsv(const std::string& path, const std::vector<QualityLeagueStanding>& rows, std::string& err) {
    std::ofstream f(path, std::ios::binary);
    if (!f) {
        err = "cannot write: " + path;
        return false;
    }
    f << "final_rank,signature,total_points,matchup_wins,matchup_draws,matchup_losses\n";
    f << std::setprecision(17);
    for (const auto& r : rows) {
        f << r.final_rank << ',' << CsvEscapeField(r.signature) << ',' << r.total_points << ',' << r.matchup_wins << ',' << r.matchup_draws << ','
          << r.matchup_losses << '\n';
    }
    return true;
}

}  // namespace bazaararena::gdf_pa
