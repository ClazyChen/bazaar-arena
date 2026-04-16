#include <bazaararena/gdf_pa/GdfPaGraph.hpp>
#include <bazaararena/gdf_pa/GdfPaCsv.hpp>

#include <bazaararena/gdf/DeckRep.hpp>

#include <cmath>
#include <fstream>
#include <iomanip>
#include <limits>
#include <string>
#include <unordered_map>

namespace bazaararena::gdf_pa {
namespace {

/// 对称差权重和 S = sum(w(item)*|Δcount|)，其中 w(小/中/大)=1/2/3。
/// - 若 S 为奇数，说明“替换成本”不是整数（例如 1 槽换 2 槽），按当前设计拒绝连边。
/// - 成本 = S/2；非全同需成本>=1。
static bool MultisetWeightedSymmetricDiffAccept(const bazaararena::gdf::DeckRep& a, const bazaararena::gdf::DeckRep& b, int replacement_cost_max,
    const std::unordered_map<std::string, int>& item_weights) {
    std::unordered_map<std::string, int> ca;
    for (const auto& n : a.item_names) ca[n]++;
    for (const auto& n : b.item_names) ca[n]--;
    long long S = 0;
    for (const auto& kv : ca) {
        const int d = std::abs(kv.second);
        if (d <= 0) continue;
        int w = 1;
        const auto it = item_weights.find(kv.first);
        if (it != item_weights.end() && it->second > 0) w = it->second;
        S += static_cast<long long>(w) * static_cast<long long>(d);
    }
    if (S < 2) return false;
    if ((S & 1LL) != 0) return false;
    const int cost = static_cast<int>(S / 2);
    return cost <= replacement_cost_max;
}

}  // namespace

bool RunDiffOneBattles(const std::vector<bazaararena::gdf::DeckRep>& nodes, bazaararena::gdf::BattleEvaluator& eval,
    int symmetric_diff_max, const std::unordered_map<std::string, int>& item_weights, std::vector<DirectedEdge>& edges_out, std::string& err) {
    err.clear();
    edges_out.clear();
    if (symmetric_diff_max < 1) {
        err = "symmetric_diff_max must be >= 1";
        return false;
    }
    const int n = static_cast<int>(nodes.size());
    for (int i = 0; i < n; ++i) {
        for (int j = i + 1; j < n; ++j) {
            if (!MultisetWeightedSymmetricDiffAccept(nodes[static_cast<size_t>(i)], nodes[static_cast<size_t>(j)], symmetric_diff_max, item_weights))
                continue;
            bazaararena::gdf::MatchPoints m100 = eval.PlaySeriesPoints(nodes[static_cast<size_t>(i)], nodes[static_cast<size_t>(j)], 100);
            double pa = m100.a;
            double pb = m100.b;
            int games = 100;
            const double tot = pa + pb;
            if (tot > 1e-9) {
                const double p = pa / tot;
                if (std::fabs(p - 0.5) < 0.05) {
                    bazaararena::gdf::MatchPoints m1000 = eval.PlaySeriesPoints(nodes[static_cast<size_t>(i)], nodes[static_cast<size_t>(j)], 1000);
                    pa = m1000.a;
                    pb = m1000.b;
                    games = 1000;
                }
            }
            DirectedEdge e;
            const double t2 = pa + pb;
            if (t2 < 1e-9) {
                e.from = i;
                e.to = j;
                e.weight = 0;
                e.games_used = games;
                edges_out.push_back(e);
                continue;
            }
            const double pwin = pa / t2;
            if (pwin >= 0.5) {
                e.from = i;
                e.to = j;
                e.weight = std::fabs(pwin - 0.5) * 2.0;
            } else {
                e.from = j;
                e.to = i;
                e.weight = std::fabs((1.0 - pwin) - 0.5) * 2.0;
            }
            e.games_used = games;
            edges_out.push_back(e);
        }
    }
    return true;
}

bool WriteEdgesCsv(const std::string& path, const std::vector<DirectedEdge>& edges, const std::vector<std::string>& node_sigs,
    std::string& err) {
    std::ofstream f(path, std::ios::binary);
    if (!f) {
        err = "cannot write: " + path;
        return false;
    }
    f << "from_sig,to_sig,weight,games\n";
    f << std::setprecision(17);
    for (const auto& e : edges) {
        f << CsvEscapeField(node_sigs[static_cast<size_t>(e.from)]) << ',' << CsvEscapeField(node_sigs[static_cast<size_t>(e.to)]) << ','
          << e.weight << ',' << e.games_used << '\n';
    }
    return true;
}

namespace {

static void ComputeDegrees(int n, const std::vector<DirectedEdge>& edges, const std::vector<bool>& alive, std::vector<int>& indeg,
    std::vector<int>& outdeg) {
    indeg.assign(static_cast<size_t>(n), 0);
    outdeg.assign(static_cast<size_t>(n), 0);
    for (const auto& e : edges) {
        if (e.from < 0 || e.from >= n || e.to < 0 || e.to >= n) continue;
        if (!alive[static_cast<size_t>(e.from)] || !alive[static_cast<size_t>(e.to)]) continue;
        outdeg[static_cast<size_t>(e.from)]++;
        indeg[static_cast<size_t>(e.to)]++;
    }
}

}  // namespace

std::vector<PeelStep> PeelGraphStructured(int n, const std::vector<DirectedEdge>& edges) {
    std::vector<PeelStep> steps;
    if (n <= 0) return steps;
    std::vector<bool> alive(static_cast<size_t>(n), true);
    std::vector<int> indeg;
    std::vector<int> outdeg;

    auto count_alive = [&]() {
        int c = 0;
        for (int i = 0; i < n; ++i) {
            if (alive[static_cast<size_t>(i)]) c++;
        }
        return c;
    };

    while (count_alive() > 0) {
        bool progressed = false;

        for (;;) {
            ComputeDegrees(n, edges, alive, indeg, outdeg);
            int pick = -1;
            for (int v = 0; v < n; ++v) {
                if (!alive[static_cast<size_t>(v)]) continue;
                if (indeg[static_cast<size_t>(v)] > 0 && outdeg[static_cast<size_t>(v)] == 0) {
                    if (pick < 0 || v < pick) pick = v;
                }
            }
            if (pick < 0) break;
            alive[static_cast<size_t>(pick)] = false;
            steps.push_back({PeelPhase::Sink, pick});
            progressed = true;
        }

        for (;;) {
            ComputeDegrees(n, edges, alive, indeg, outdeg);
            int pick = -1;
            for (int v = 0; v < n; ++v) {
                if (!alive[static_cast<size_t>(v)]) continue;
                if (indeg[static_cast<size_t>(v)] == 0 && outdeg[static_cast<size_t>(v)] == 0) {
                    if (pick < 0 || v < pick) pick = v;
                }
            }
            if (pick < 0) break;
            alive[static_cast<size_t>(pick)] = false;
            steps.push_back({PeelPhase::SourceQuality, pick});
            progressed = true;
        }

        if (count_alive() == 0) break;

        double best_w = std::numeric_limits<double>::infinity();
        int pick = -1;
        for (int v = 0; v < n; ++v) {
            if (!alive[static_cast<size_t>(v)]) continue;
            double out_sum = 0;
            double in_sum = 0;
            int out_cnt = 0;
            int in_cnt = 0;
            for (const auto& e : edges) {
                if (e.from < 0 || e.from >= n || e.to < 0 || e.to >= n) continue;
                if (!alive[static_cast<size_t>(e.from)] || !alive[static_cast<size_t>(e.to)]) continue;
                if (e.from == v) {
                    out_sum += e.weight;
                    out_cnt++;
                } else if (e.to == v) {
                    in_sum += e.weight;
                    in_cnt++;
                }
            }
            const int deg = out_cnt + in_cnt;
            const double sw = (deg > 0) ? ((out_sum - in_sum) / static_cast<double>(deg)) : 0.0;
            if (sw < best_w || (sw == best_w && (pick < 0 || v < pick))) {
                best_w = sw;
                pick = v;
            }
        }
        if (pick >= 0) {
            alive[static_cast<size_t>(pick)] = false;
            steps.push_back({PeelPhase::CycleBreak, pick});
            progressed = true;
        }

        if (!progressed) {
            for (int v = 0; v < n; ++v) {
                if (alive[static_cast<size_t>(v)]) {
                    alive[static_cast<size_t>(v)] = false;
                    steps.push_back({PeelPhase::CycleBreak, v});
                }
            }
            break;
        }
    }

    return steps;
}

}  // namespace bazaararena::gdf_pa
