#include <bazaararena/gdf/BattleEvaluator.hpp>
#include <bazaararena/gdf/DeckRep.hpp>
#include <bazaararena/gdf/GdfItemPrototypeCache.hpp>
#include <bazaararena/gdf/GdfLevelRules.hpp>
#include <bazaararena/gdf/GdfLoadYamlPool.hpp>
#include <bazaararena/gdf/ItemPool.hpp>
#include <bazaararena/gdf_pa/GdfPaBracket.hpp>
#include <bazaararena/gdf_pa/GdfPaFullTopk.hpp>
#include <bazaararena/gdf_pa/GdfPaGraph.hpp>
#include <bazaararena/gdf_pa/GdfPaMetrics.hpp>
#include <bazaararena/gdf_pa/GdfPaRoundRobin.hpp>
#include <bazaararena/gdf_pa/GdfPaCsv.hpp>

#include <bazaararena/engine.hpp>

#include <algorithm>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <unordered_map>
#include <thread>
#include <vector>

namespace {

static int DefaultWorkers() {
    const unsigned hc = std::thread::hardware_concurrency();
    return hc > 0 ? static_cast<int>(hc) : 4;
}

static void CollectAllPoolKeys(const bazaararena::gdf::ItemPool& pool, std::vector<std::string>& keys) {
    for (const auto& v : {pool.SmallNames(), pool.MediumNames(), pool.LargeNames()}) {
        for (const auto& k : v) keys.push_back(k);
    }
    std::sort(keys.begin(), keys.end());
    keys.erase(std::unique(keys.begin(), keys.end()), keys.end());
}

static std::unordered_map<std::string, int> BuildItemSizeWeights(const bazaararena::gdf::ItemPool& pool) {
    std::unordered_map<std::string, int> w;
    for (const auto& n : pool.SmallNames()) w[n] = 1;
    for (const auto& n : pool.MediumNames()) w[n] = 2;
    for (const auto& n : pool.LargeNames()) w[n] = 3;
    return w;
}

static void RemoveItemFromPoolKeys(std::vector<std::string>& keys, std::unordered_map<std::string, int>& weights, std::string_view item) {
    keys.erase(std::remove(keys.begin(), keys.end(), std::string(item)), keys.end());
    weights.erase(std::string(item));
}

static bazaararena::gdf::DeckRep DeckFromSignature(const std::string& sig) {
    bazaararena::gdf::DeckRep r;
    size_t i = 0;
    while (i < sig.size()) {
        while (i < sig.size() && (sig[i] == ',' || sig[i] == ' ' || sig[i] == '\t')) i++;
        if (i >= sig.size()) break;
        size_t j = i;
        while (j < sig.size() && sig[j] != ',') j++;
        std::string s = sig.substr(i, j - i);
        while (!s.empty() && (s.back() == ' ' || s.back() == '\t')) s.pop_back();
        if (!s.empty()) r.item_names.push_back(std::move(s));
        i = j + 1;
    }
    return r;
}

static void PrintUsage(std::ostream& os) {
    os << "bazaararena_gdf_pa --data-dir <items> --full-topk-path <path> --out-dir <dir> [options]\n"
          "  --pool-hero Vanessa|Mak|Common|All   (default Vanessa)\n"
          "  --level <2-20>                       (default 2)\n"
          "  --workers <n>                        battle threads (omit=CPU cores; 0=serial)\n"
          "  --bo <n>                             odd BoN length (default 5)\n"
          "  --top-k <K>                          H_K 与泛用度截断 (default 10)\n"
          "  --max-anchors <n>                    仅处理前 n 个锚点块（开发用；0=不限制）\n"
          "  --graph-symmetric-diff-max <c>       建图：替换成本上限 c（>=1）。定义：S=sum(w*|Delta count|)，w(小/中/大)=1/2/3；若 S 为偶数则成本=S/2（默认 2）\n"
          "  --cluster-specialty-min <x>          簇收录仅看锚点 specialty>=x（不再筛 top1 的 (anchor_m/rr)^3）；达标收录 top3，未达整块不收（默认 0.5）\n"
          "  --quality-rr-games <n>               剥点来源排名：每对打 n 局系列赛；按 BO_N 总局分排名（默认 100）\n"
          "  --help\n"
          "\n"
          "输入：gdf_enumerate_anchor_top1.py 生成的 full_topk 文本。\n"
          "输出：generality.csv、specialty.csv（含 rank；字段按需 RFC4180 引号）；\n"
          "      quality_decks.csv、quality_ranking.csv（quality_peel_sources 卡组系列赛循环）；clusters.csv、graph_edges.csv、\n"
          "      peel_order.csv（含 phase）、quality_peel_sources.csv（仅 phase=source_quality）、run_log.txt\n";
}

struct Args {
    std::string data_items_dir = "data/items";
    std::string full_topk_path;
    std::string out_dir;
    std::string pool_hero = "Vanessa";
    int level = 2;
    int workers = -1;
    int best_of = 5;
    int top_k = 10;
    int max_anchors = 0;
    int graph_symmetric_diff_max = 2;
    double cluster_specialty_min = 0.5;
    int quality_rr_games = 100;
};

static bool ParseArgs(int argc, char** argv, Args& out, std::string& err) {
    err.clear();
    for (int i = 1; i < argc; i++) {
        std::string_view tok = argv[i];
        if (tok == "--help" || tok == "-h") return false;
        auto need = [&](const char* name) -> bool {
            if (i + 1 >= argc) {
                err = std::string("missing value for ") + name;
                return false;
            }
            return true;
        };
        if (tok == "--data-dir" && need("--data-dir")) {
            out.data_items_dir = argv[++i];
        } else if (tok == "--full-topk-path" && need("--full-topk-path")) {
            out.full_topk_path = argv[++i];
        } else if (tok == "--out-dir" && need("--out-dir")) {
            out.out_dir = argv[++i];
        } else if (tok == "--pool-hero" && need("--pool-hero")) {
            out.pool_hero = argv[++i];
        } else if (tok == "--level" && need("--level")) {
            out.level = std::atoi(argv[++i]);
        } else if (tok == "--workers" && need("--workers")) {
            out.workers = std::atoi(argv[++i]);
        } else if (tok == "--bo" && need("--bo")) {
            out.best_of = std::atoi(argv[++i]);
        } else if (tok == "--top-k" && need("--top-k")) {
            out.top_k = std::atoi(argv[++i]);
        } else if (tok == "--max-anchors" && need("--max-anchors")) {
            out.max_anchors = std::atoi(argv[++i]);
        } else if (tok == "--graph-symmetric-diff-max" && need("--graph-symmetric-diff-max")) {
            out.graph_symmetric_diff_max = std::atoi(argv[++i]);
        } else if (tok == "--cluster-specialty-min" && need("--cluster-specialty-min")) {
            const char* p = argv[++i];
            char* endptr = nullptr;
            const double v = std::strtod(p, &endptr);
            if (endptr == p) {
                err = "bad value for --cluster-specialty-min";
                return false;
            }
            out.cluster_specialty_min = v;
        } else if (tok == "--quality-rr-games" && need("--quality-rr-games")) {
            out.quality_rr_games = std::atoi(argv[++i]);
        } else {
            err = std::string("unknown argument: ") + std::string(tok);
            return false;
        }
    }
    if (out.full_topk_path.empty() || out.out_dir.empty()) {
        err = "--full-topk-path and --out-dir are required";
        return false;
    }
    if (out.level < bazaararena::gdf::GdfLevelRules::MinPlayerLevel || out.level > bazaararena::gdf::GdfLevelRules::MaxPlayerLevel) {
        err = "level out of range";
        return false;
    }
    if (out.top_k <= 0) out.top_k = 10;
    if (out.best_of <= 0 || out.best_of % 2 == 0) out.best_of = 5;
    if (out.workers < 0) out.workers = DefaultWorkers();
    if (out.graph_symmetric_diff_max < 1) out.graph_symmetric_diff_max = 1;
    if (out.graph_symmetric_diff_max > 128) {
        err = "--graph-symmetric-diff-max must be <= 128";
        return false;
    }
    if (out.quality_rr_games <= 0) out.quality_rr_games = 100;
    return true;
}

}  // namespace

int main(int argc, char** argv) {
    Args args;
    std::string err;
    if (!ParseArgs(argc, argv, args, err)) {
        if (!err.empty()) std::cerr << err << "\n";
        PrintUsage(std::cout);
        return err.empty() ? 0 : 2;
    }

    std::unordered_map<std::string, std::string> key_to_hero;
    if (!bazaararena::gdf::LoadItemHeroByKeyFromDataDir(args.data_items_dir, key_to_hero, err)) {
        std::cerr << "[gdf_pa] " << err << "\n";
        return 2;
    }

    std::vector<bazaararena::gdf_pa::FullTopkAnchorBlock> blocks;
    if (!bazaararena::gdf_pa::ParseFullTopkFile(args.full_topk_path, blocks, err)) {
        std::cerr << "[gdf_pa] parse full_topk: " << err << "\n";
        return 2;
    }

    if (args.max_anchors > 0 && static_cast<int>(blocks.size()) > args.max_anchors) {
        blocks.resize(static_cast<size_t>(args.max_anchors));
    }

    std::error_code ec;
    const std::filesystem::path out_path(args.out_dir);
    std::filesystem::create_directories(out_path, ec);
    if (ec) {
        std::cerr << "[gdf_pa] cannot create out-dir: " << args.out_dir << " (" << ec.message() << ")\n";
        return 2;
    }

    const bazaararena::EngineVersion v = bazaararena::GetEngineVersion();
    {
        std::ofstream log(out_path / "run_log.txt", std::ios::binary);
        log << "bazaararena_gdf_pa engine_version=" << v.major << '.' << v.minor << '.' << v.patch << "\n";
        log << "full_topk=" << args.full_topk_path << "\n";
        log << "data_dir=" << args.data_items_dir << " pool_hero=" << args.pool_hero << " level=" << args.level << " workers=" << args.workers
            << " bo=" << args.best_of << " top_k=" << args.top_k << " graph_symmetric_diff_max=" << args.graph_symmetric_diff_max
            << " cluster_specialty_min=" << args.cluster_specialty_min << " quality_rr_games=" << args.quality_rr_games << "\n";
    }

    bazaararena::gdf::ItemPool pool(args.level, args.pool_hero, {}, key_to_hero);
    const auto item_weights = BuildItemSizeWeights(pool);
    std::vector<std::string> pool_keys;
    CollectAllPoolKeys(pool, pool_keys);

    // GDF 输出仅包含「减速烙刀」「加速烙刀」两个展示名；GDF-PA 侧从分析宇宙中移除「烙刀」。
    auto item_weights_mut = item_weights;
    RemoveItemFromPoolKeys(pool_keys, item_weights_mut, "烙刀");

    std::vector<bazaararena::gdf_pa::GeneralityRow> gen_rows;
    if (!bazaararena::gdf_pa::ComputeGeneralityTable(blocks, args.top_k, pool_keys, gen_rows, err)) {
        std::cerr << "[gdf_pa] generality: " << err << "\n";
        return 2;
    }
    bazaararena::gdf_pa::SortAndRankGeneralityForOutput(gen_rows);
    if (!bazaararena::gdf_pa::WriteGeneralityCsv((out_path / "generality.csv").string(), gen_rows, err)) {
        std::cerr << "[gdf_pa] " << err << "\n";
        return 2;
    }

    std::vector<bazaararena::gdf_pa::SpecialtyRow> spec_rows;
    std::vector<std::string> rr_skip;
    if (!bazaararena::gdf_pa::ComputeSpecialtyTable(blocks, spec_rows, rr_skip, err)) {
        std::cerr << "[gdf_pa] specialty: " << err << "\n";
        return 2;
    }
    bazaararena::gdf_pa::SortAndRankSpecialtyForOutput(spec_rows);
    if (!bazaararena::gdf_pa::WriteSpecialtyCsv((out_path / "specialty.csv").string(), spec_rows, err)) {
        std::cerr << "[gdf_pa] " << err << "\n";
        return 2;
    }
    {
        std::ofstream sk(out_path / "rr_skipped.log", std::ios::binary);
        for (const auto& s : rr_skip) sk << s << '\n';
    }

    std::unordered_map<std::string, double> anchor_specialty;
    for (const auto& sr : spec_rows) {
        anchor_specialty[sr.anchor] = sr.spec;
    }

    bazaararena::gdf::GdfItemPrototypeCache protos(pool, args.level);
    bazaararena::gdf::BattleEvaluator eval(args.best_of, args.workers, args.level, nullptr, &protos);

    std::unordered_map<std::string, std::vector<bazaararena::gdf::DeckRep>> clusters;
    for (const auto& block : blocks) {
        if (block.ranks.empty()) continue;

        const double anchor_spec = [&]() -> double {
            const auto it = anchor_specialty.find(block.anchor_item);
            return it == anchor_specialty.end() ? 0.0 : it->second;
        }();
        if (anchor_spec < args.cluster_specialty_min) continue;

        const int n_take = std::min(3, static_cast<int>(block.ranks.size()));
        for (int i = 0; i < n_take; ++i) {
            bazaararena::gdf::DeckRep d = DeckFromSignature(block.ranks[static_cast<size_t>(i)].deck_signature);
            const std::string key = bazaararena::gdf::BuildComboKey(d.item_names);
            clusters[key].push_back(std::move(d));
        }
    }

    std::ofstream cl(out_path / "clusters.csv", std::ios::binary);
    cl << "combo_key,representative_signature,cluster_size\n";

    std::vector<bazaararena::gdf::DeckRep> graph_nodes;
    std::vector<std::string> graph_sigs;

    size_t cluster_i = 0;
    for (auto& kv : clusters) {
        cluster_i++;
        auto& decks = kv.second;
        if (decks.empty()) continue;
        std::sort(decks.begin(), decks.end(), [](const bazaararena::gdf::DeckRep& a, const bazaararena::gdf::DeckRep& b) {
            return a.Signature() < b.Signature();
        });
        decks.erase(std::unique(decks.begin(), decks.end(),
                     [](const bazaararena::gdf::DeckRep& a, const bazaararena::gdf::DeckRep& b) {
                         return a.Signature() == b.Signature();
                     }),
            decks.end());
        // 提前验证：full_topk 里若出现不在当前 pool(level/hero) 的物品展示名，会在对战构建 side 时抛异常并导致进程异常退出。
        // 这里把错误变成可读的报错，方便定位“输入 full_topk 与本次 pool 参数不匹配”的根因。
        try {
            for (const auto& d : decks) {
                for (const auto& name : d.item_names) {
                    (void)protos.At(name);
                }
            }
        } catch (...) {
            std::cerr << "[gdf_pa] cluster prototype validation failed (unknown exception)\n";
            return 2;
        }

        const int rep_i = bazaararena::gdf_pa::RunClusterRepresentative(decks, eval);
        const std::string rep_sig = decks[static_cast<size_t>(rep_i)].Signature();
        cl << bazaararena::gdf_pa::CsvEscapeField(kv.first) << ',' << bazaararena::gdf_pa::CsvEscapeField(rep_sig) << ',' << decks.size()
           << '\n';
        graph_nodes.push_back(decks[static_cast<size_t>(rep_i)]);
        graph_sigs.push_back(rep_sig);
    }

    {
        std::ofstream qd(out_path / "quality_decks.csv", std::ios::binary);
        qd << "index,signature\n";
        for (size_t i = 0; i < graph_sigs.size(); ++i) {
            qd << i << ',' << bazaararena::gdf_pa::CsvEscapeField(graph_sigs[i]) << '\n';
        }
    }

    std::vector<bazaararena::gdf_pa::DirectedEdge> edges;
    if (!bazaararena::gdf_pa::RunDiffOneBattles(graph_nodes, eval, args.graph_symmetric_diff_max, item_weights_mut, edges, err)) {
        std::cerr << "[gdf_pa] graph: " << err << "\n";
        return 2;
    }
    if (!bazaararena::gdf_pa::WriteEdgesCsv((out_path / "graph_edges.csv").string(), edges, graph_sigs, err)) {
        std::cerr << "[gdf_pa] " << err << "\n";
        return 2;
    }

    const auto peel_steps = bazaararena::gdf_pa::PeelGraphStructured(static_cast<int>(graph_nodes.size()), edges);
    {
        std::ofstream po(out_path / "peel_order.csv", std::ios::binary);
        po << "peel_index,phase,node_index,signature\n";
        for (size_t i = 0; i < peel_steps.size(); ++i) {
            const auto& st = peel_steps[i];
            const int idx = st.node_index;
            po << i << ',' << bazaararena::gdf_pa::PeelPhaseLabel(st.phase) << ',' << idx << ','
               << bazaararena::gdf_pa::CsvEscapeField(graph_sigs[static_cast<size_t>(idx)]) << '\n';
        }
    }
    {
        std::ofstream qs(out_path / "quality_peel_sources.csv", std::ios::binary);
        qs << "order,node_index,signature\n";
        int ord = 0;
        for (const auto& st : peel_steps) {
            if (st.phase != bazaararena::gdf_pa::PeelPhase::SourceQuality) continue;
            qs << ord << ',' << st.node_index << ',' << bazaararena::gdf_pa::CsvEscapeField(graph_sigs[static_cast<size_t>(st.node_index)])
               << '\n';
            ord++;
        }
    }

    std::vector<bazaararena::gdf::DeckRep> peel_source_decks;
    for (const auto& st : peel_steps) {
        if (st.phase != bazaararena::gdf_pa::PeelPhase::SourceQuality) continue;
        peel_source_decks.push_back(graph_nodes[static_cast<size_t>(st.node_index)]);
    }
    if (!peel_source_decks.empty()) {
        std::vector<double> tp;
        std::vector<int> mw, md, ml;
        std::vector<std::vector<double>> symm_delta;
        bazaararena::gdf_pa::RunPeelSourcesRoundRobin(peel_source_decks, eval, args.quality_rr_games, tp, mw, md, ml, &symm_delta);
        std::vector<int> kept = bazaararena::gdf_pa::FilterWinlessStrictIterative(symm_delta);
        if (kept.empty()) {
            kept.reserve(peel_source_decks.size());
            for (size_t i = 0; i < peel_source_decks.size(); ++i) kept.push_back(static_cast<int>(i));
        }
        std::vector<bazaararena::gdf::DeckRep> league_decks;
        league_decks.reserve(kept.size());
        for (int gi : kept) league_decks.push_back(peel_source_decks[static_cast<size_t>(gi)]);

        std::vector<double> tp_f;
        std::vector<int> mw_f, md_f, ml_f;
        bazaararena::gdf_pa::AggregateLeagueFromDeltaSubset(kept, symm_delta, args.quality_rr_games, tp_f, mw_f, md_f, ml_f);
        const auto standings = bazaararena::gdf_pa::BuildQualityLeagueStandings(league_decks, tp_f, mw_f, md_f, ml_f);
        if (!bazaararena::gdf_pa::WriteQualityRankingCsv((out_path / "quality_ranking.csv").string(), standings, err)) {
            std::cerr << "[gdf_pa] quality_ranking: " << err << "\n";
            return 2;
        }
    } else {
        std::ofstream qr(out_path / "quality_ranking.csv", std::ios::binary);
        qr << "final_rank,signature,total_points,matchup_wins,matchup_draws,matchup_losses\n";
    }

    std::cout << "bazaararena_gdf_pa ok. outputs in " << args.out_dir << "\n";
    return 0;
}
