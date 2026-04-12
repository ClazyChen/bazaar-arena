#include <bazaararena/engine.hpp>
#include <bazaararena/gdf/BattleEvaluator.hpp>
#include <bazaararena/gdf/GdfLevelRules.hpp>
#include <bazaararena/gdf/GdfLoadYamlPool.hpp>
#include <bazaararena/gdf/GreedySearcher.hpp>
#include <bazaararena/gdf/ItemPool.hpp>

#include <algorithm>
#include <chrono>
#include <cstdlib>
#include <fstream>
#include <iostream>
#include <optional>
#include <random>
#include <sstream>
#include <string>
#include <string_view>
#include <unordered_set>
#include <thread>
#include <vector>

namespace {

static int DefaultWorkerThreads() {
    const unsigned hc = std::thread::hardware_concurrency();
    return hc > 0 ? static_cast<int>(hc) : 4;
}

struct Args {
    std::string data_items_dir = "data/items";
    std::string anchor_item;
    std::vector<std::string> seed_items;
    bool enumerate_anchors = false;
    int top_k = 10;
    int top_multiplier = 3;
    int best_of = 5;
    /// -1：未指定 `--workers` 时使用硬件并发（极致性能）；>=0 为显式值（0=强制单线程对战）。
    int workers = -1;
    std::optional<int> seed;
    int level = 2;
    std::string pool_hero = "Vanessa";
    std::unordered_set<std::string> excluded;
    std::string output_path;
    double lambda_anchor = 0;
    double mu_diversity = 0;
    bool diversity_exclude_seeds = false;
};

static void PrintUsage(std::ostream& os) {
    os << "bazaararena_gdf --data-dir <path/to/data/items> [options]\n"
          "  --anchor-item <name>       single anchor (exclusive with --seed-items)\n"
          "  --seed-items a,b,c         ordered seed deck\n"
          "  --enumerate-anchors        run once per pool item as single-item anchor\n"
          "  --level <2-20>             player level (default 2)\n"
          "  --top-k <k>                beam output size (default 10)\n"
          "  --top-multiplier <M>       Swiss stage = k*M (default 3)\n"
          "  --bo <n>                   odd series length per pair (default 5)\n"
          "  --workers <n>              battle threads (omit=CPU cores; 0=serial battles)\n"
          "  --seed <int>               search RNG seed (optional; battles are not reproducible)\n"
          "  --pool-hero Vanessa|Mak|Common|All\n"
          "  --exclude-item a,b         repeatable / comma-separated\n"
          "  --lambda-anchor <x>        weight for paired anchor margin (0 skips augment)\n"
          "  --mu-diversity <x>         MMR diversity penalty on Jaccard similarity\n"
          "  --diversity-exclude-seeds  Jaccard ignores seed item names\n"
          "  --output <path>            write text summary\n"
          "  --help\n";
}

static std::vector<std::string> SplitCsv(std::string_view input) {
    std::vector<std::string> out;
    size_t i = 0;
    while (i < input.size()) {
        while (i < input.size() && (input[i] == ',' || input[i] == ' ' || input[i] == '\t')) i++;
        if (i >= input.size()) break;
        size_t j = i;
        while (j < input.size() && input[j] != ',') j++;
        std::string s(input.substr(i, j - i));
        while (!s.empty() && (s.back() == ' ' || s.back() == '\t')) s.pop_back();
        if (!s.empty()) out.push_back(std::move(s));
        i = j + 1;
    }
    return out;
}

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
        } else if (tok == "--anchor-item" && need("--anchor-item")) {
            out.anchor_item = argv[++i];
        } else if (tok == "--seed-items" && need("--seed-items")) {
            for (const auto& s : SplitCsv(argv[++i])) out.seed_items.push_back(s);
        } else if (tok == "--enumerate-anchors") {
            out.enumerate_anchors = true;
        } else if (tok == "--level" && need("--level")) {
            out.level = std::atoi(argv[++i]);
        } else if (tok == "--top-k" && need("--top-k")) {
            out.top_k = std::atoi(argv[++i]);
        } else if (tok == "--top-multiplier" && need("--top-multiplier")) {
            out.top_multiplier = std::atoi(argv[++i]);
        } else if (tok == "--bo" && need("--bo")) {
            out.best_of = std::atoi(argv[++i]);
        } else if (tok == "--workers" && need("--workers")) {
            out.workers = std::atoi(argv[++i]);
        } else if (tok == "--seed" && need("--seed")) {
            out.seed = std::atoi(argv[++i]);
        } else if (tok == "--pool-hero" && need("--pool-hero")) {
            out.pool_hero = argv[++i];
        } else if (tok == "--exclude-item" && need("--exclude-item")) {
            for (const auto& s : SplitCsv(argv[++i])) out.excluded.insert(s);
        } else if (tok == "--lambda-anchor" && need("--lambda-anchor")) {
            out.lambda_anchor = std::atof(argv[++i]);
        } else if (tok == "--mu-diversity" && need("--mu-diversity")) {
            out.mu_diversity = std::atof(argv[++i]);
        } else if (tok == "--diversity-exclude-seeds") {
            out.diversity_exclude_seeds = true;
        } else if (tok == "--output" && need("--output")) {
            out.output_path = argv[++i];
        } else {
            err = std::string("unknown argument: ") + std::string(tok);
            return false;
        }
    }

    if (out.enumerate_anchors) {
        if (!out.anchor_item.empty() || !out.seed_items.empty()) {
            err = "do not combine --enumerate-anchors with --anchor-item / --seed-items";
            return false;
        }
    } else {
        const bool has_anchor = !out.anchor_item.empty();
        const bool has_seed = !out.seed_items.empty();
        if (has_anchor == has_seed) {
            err = "provide exactly one of --anchor-item or --seed-items (or use --enumerate-anchors)";
            return false;
        }
        if (has_anchor) out.seed_items = {out.anchor_item};
    }

    if (out.level < bazaararena::gdf::GdfLevelRules::MinPlayerLevel || out.level > bazaararena::gdf::GdfLevelRules::MaxPlayerLevel) {
        err = "level out of range";
        return false;
    }
    if (out.top_k <= 0) out.top_k = 10;
    if (out.top_multiplier <= 0) out.top_multiplier = 3;
    if (out.best_of <= 0 || out.best_of % 2 == 0) out.best_of = 5;
    if (out.workers < 0) out.workers = DefaultWorkerThreads();

    for (const auto& s : out.seed_items) {
        if (out.excluded.count(s)) {
            err = "seed item is excluded: " + s;
            return false;
        }
    }
    return true;
}

static void CollectAllPoolKeys(const bazaararena::gdf::ItemPool& pool, std::vector<std::string>& keys) {
    for (const auto& v : {pool.SmallNames(), pool.MediumNames(), pool.LargeNames()}) {
        for (const auto& k : v) keys.push_back(k);
    }
    std::sort(keys.begin(), keys.end());
    keys.erase(std::unique(keys.begin(), keys.end()), keys.end());
}

static void RunOneSearch(bazaararena::gdf::ItemPool& pool, const Args& args, const std::vector<std::string>& seeds,
    const std::unordered_set<std::string>& seed_set, std::ostream& out) {
    bazaararena::gdf::GreedyConfig gcfg;
    gcfg.player_level = args.level;
    gcfg.top_k = args.top_k;
    gcfg.top_multiplier = args.top_multiplier;
    gcfg.best_of = args.best_of;
    gcfg.workers = args.workers;
    gcfg.seed = args.seed;
    gcfg.lambda_anchor = args.lambda_anchor;
    gcfg.mu_diversity = args.mu_diversity;
    gcfg.diversity_exclude_seeds = args.diversity_exclude_seeds;

    std::mt19937 rng;
    if (args.seed.has_value()) {
        rng.seed(static_cast<unsigned>(*args.seed));
    } else {
        const auto t = std::chrono::high_resolution_clock::now().time_since_epoch().count();
        rng.seed(static_cast<unsigned>(t) ^ static_cast<unsigned>(t >> 32));
    }

    bazaararena::gdf::BattleEvaluator evaluator(args.best_of, args.workers, args.level);
    bazaararena::gdf::GreedySearcher searcher(pool, evaluator, rng, gcfg, seed_set);

    out << "[GDF] seeds:";
    for (const auto& s : seeds) out << " " << s;
    out << "\n";

    searcher.Run(seeds, [&](int size, const std::vector<bazaararena::gdf::CandidateState>& top) {
        out << "[GDF] size=" << size << " top " << top.size() << "\n";
        for (size_t i = 0; i < top.size(); i++) {
            out << "  " << (i + 1) << ". RR=" << top[i].round_robin_score << " anchor_m=" << top[i].anchor_margin << " Swiss=" << top[i].swiss_score
                << " | " << top[i].representative.Signature() << "\n";
        }
    });
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
        std::cerr << "[GDF] " << err << "\n";
        return 2;
    }

    bazaararena::gdf::ItemPool pool(args.level, args.pool_hero, args.excluded, key_to_hero);

    std::ofstream fout;
    std::ostream* out_stream = &std::cout;
    if (!args.output_path.empty()) {
        fout.open(args.output_path, std::ios::binary);
        if (!fout) {
            std::cerr << "failed to open output: " << args.output_path << "\n";
            return 2;
        }
        out_stream = &fout;
    }

    const bazaararena::EngineVersion v = bazaararena::GetEngineVersion();
    *out_stream << "bazaararena_gdf engine_version=" << v.major << '.' << v.minor << '.' << v.patch << "\n";

    if (args.enumerate_anchors) {
        std::vector<std::string> keys;
        CollectAllPoolKeys(pool, keys);
        for (const auto& k : keys) {
            if (args.excluded.count(k)) continue;
            std::unordered_set<std::string> seed_set{k};
            RunOneSearch(pool, args, {k}, seed_set, *out_stream);
        }
    } else {
        std::unordered_set<std::string> seed_set(args.seed_items.begin(), args.seed_items.end());
        RunOneSearch(pool, args, args.seed_items, seed_set, *out_stream);
    }

    return 0;
}
