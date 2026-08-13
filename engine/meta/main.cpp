// bazaararena_meta — 批量对战评估 CLI（meta_search 管线的 C++ 计算层）。
//
// 目的：把「数以万计的系列赛」从 Python 子进程调度（每局一个 CLI 进程）迁移到
// 单进程批量执行：GDF 物品规则（quest 覆写/overridable 缩放/魂石变体）+ deck 缓存 +
// thread_local Simulator + 工作线程池。与 engine/gdf 平行、不改 Simulator 核心语义。
//
// 与 bazaararena_gdf 的 BattleEvaluator 的关键差别：**完全可复现**——
// 每局使用任务单中显式给出的 seed 与左右交换规则（前半不换、后半换），
// 不使用任何时间熵/批盐。同一任务单两次运行结果逐局一致。
//
// 协议（docs 见 scripts/meta_search/README.md）：
//   bazaararena_meta --input job.json --output results.jsonl   # 文件批量模式
//   bazaararena_meta --serve                                   # 常驻服务模式：
//     stdin 每行一个 JSON：{"id":"...","a":[...],"b":[...],"seeds":[...]}
//     stdout 每行一个结果：{"id":"...","results":[0|1|2,...]}（每行 flush）
//     EOF 退出。整个管线一次启动，进程/物品池/规则加载成本只付一次。
// 输入：
//   { "data_dir": "data/items",   // 可选
//     "hero": "Mak",              // 可选，默认 Mak
//     "level": 8,                 // 可选，默认 8
//     "workers": 0,               // 可选，0 = 硬件并发
//     "battles": [ {"id": "...", "a": ["物品名",...], "b": [...], "seeds": [1000,...]} ] }
// 输出（JSONL，每行一个 battle 结果）：
//   {"id":"...","wins_a":x,"wins_b":y,"ties":z,"results":[0|1|2,...]}
//   results[j]：0 = a 胜，1 = b 胜，2 = 平局（第 j 个 seed；j >= len/2 时双方交换物理侧）。

#include <bazaararena/core/ItemKey.hpp>
#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/core/SideState.hpp>
#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/core/SimulatorInit.hpp>
#include <bazaararena/gdf/DeckRep.hpp>
#include <bazaararena/gdf/GdfItemPrototypeCache.hpp>
#include <bazaararena/gdf/GdfLoadYamlPool.hpp>
#include <bazaararena/gdf/ItemPool.hpp>
#include <bazaararena/io/JsonLite.hpp>
#include <bazaararena/io/Sink.hpp>

#include "ReasonMine.hpp"

#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <iostream>
#include <mutex>
#include <optional>
#include <shared_mutex>
#include <sstream>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>

namespace core = bazaararena::core;
namespace io = bazaararena::io;
namespace gdf = bazaararena::gdf;

namespace {

// ---- 对战执行（语义镜像 engine/cli/main.cpp 与 gdf/BattleEvaluator.cpp；见各函数注释）----

core::Simulator& TlsBattleSimulator() {
    thread_local core::Simulator sim;
    return sim;
}

/// 与 gdf/BattleEvaluator.cpp 的 PatchPhysicalSideSlot 一致：缓存模板 SideIndex 全 0，
/// 装入物理侧前必须改写，否则 AbilityApply 解析对手时会打到己方。
void PatchPhysicalSideSlot(core::SideState& side, int physical_slot) {
    if (physical_slot < 0 || physical_slot >= core::Simulator::SideCount) return;
    side.attrs[core::SideKey::Id] = physical_slot;
    const int n = side.attrs[core::SideKey::ItemCount];
    for (int i = 0; i < n && i < static_cast<int>(core::SideState::MaxItems); i++) {
        side.items[static_cast<size_t>(i)].attrs[core::ItemKey::SideIndex] = physical_slot;
    }
}

int HpShieldTotal(const core::SideState& s) {
    return s.attrs[core::SideKey::Hp] + s.attrs[core::SideKey::Shield];
}

/// 与 CLI 每局循环一致：装两侧、清 sink、播种、InitializeSimulator、Run(false)。
int RunSingleBattleReturn(core::Simulator& sim, const core::SideState& side_a,
                          const core::SideState& side_b, int swap, int rng_seed) {
    if (swap == 0) {
        sim.sides[0] = side_a;
        sim.sides[1] = side_b;
    } else {
        sim.sides[0] = side_b;
        sim.sides[1] = side_a;
    }
    sim.sandstorm = core::Simulator::SandStorm{};
    sim.sink.sink_type = io::Sink::TypeNone;
    sim.sink.max_events = 0;
    sim.sink.truncated = false;
    sim.sink.Clear();
    sim.rng.Seed(rng_seed);
    core::InitializeSimulator(sim);
    return sim.Run(true);  // 与 CLI 一致：allowTie=true，同帧双亡返回 -1 判平局
}

/// 单局：返回 0 = a 胜 / 1 = b 胜 / 2 = 平局（与 CLI allowTie=true 语义逐局一致）。
int PlayOneGame(const core::SideState& base_a, const core::SideState& base_b,
                int swap, int rng_seed) {
    core::SideState side_a = base_a;
    core::SideState side_b = base_b;
    if (swap == 0) {
        PatchPhysicalSideSlot(side_a, 0);
        PatchPhysicalSideSlot(side_b, 1);
    } else {
        PatchPhysicalSideSlot(side_b, 0);
        PatchPhysicalSideSlot(side_a, 1);
    }
    core::Simulator& sim = TlsBattleSimulator();
    const int w_run = RunSingleBattleReturn(sim, side_a, side_b, swap, rng_seed);
    if (w_run < 0) return 2;
    return swap == 0 ? w_run : 1 - w_run;
}

// ---- 任务单 ----

struct BattleReq {
    std::string id;
    std::vector<std::string> a;
    std::vector<std::string> b;
    std::vector<int64_t> seeds;
};

struct BattleResult {
    std::string id;
    std::vector<int> results;  // 0/1/2 per seed
};

struct Job {
    std::string data_dir = "data/items";
    std::string hero = "Mak";
    int level = 8;
    int workers = 0;
    std::vector<BattleReq> battles;
};

bool ParseBattleEntry(const io::JsonValue& e, BattleReq& req, std::string& err) {
    auto get_names = [](const io::JsonValue& obj, const char* key, std::vector<std::string>& out) -> bool {
        const auto* arr = io::GetObjectField(obj, key);
        if (!arr || !arr->IsArray()) return false;
        for (const auto& el : *arr->AsArray()) {
            auto s = io::GetString(el);
            if (!s) return false;
            out.emplace_back(*s);
        }
        return true;
    };
    if (const auto* v = io::GetObjectField(e, "id")) {
        if (auto s = io::GetString(*v)) req.id = std::string(*s);
    }
    if (!get_names(e, "a", req.a) || !get_names(e, "b", req.b)) {
        err = "battle entry missing a/b name arrays";
        return false;
    }
    if (const auto* v = io::GetObjectField(e, "seeds")) {
        if (v->IsArray()) {
            for (const auto& s : *v->AsArray()) {
                if (auto n = io::GetNumber(s)) req.seeds.push_back(static_cast<int64_t>(*n));
            }
        }
    }
    if (req.seeds.empty()) {
        err = "battle entry has empty seeds: " + req.id;
        return false;
    }
    return true;
}

bool ParseJob(const std::string& path, Job& job, std::string& err) {
    std::ifstream in(path, std::ios::binary);
    if (!in) {
        err = "cannot open input: " + path;
        return false;
    }
    std::stringstream ss;
    ss << in.rdbuf();
    io::JsonParseError perr;
    auto root = io::ParseJson(ss.str(), perr);
    if (!root) {
        err = "JSON parse error: " + perr.message;
        return false;
    }
    if (const auto* v = io::GetObjectField(*root, "data_dir")) {
        if (auto s = io::GetString(*v)) job.data_dir = std::string(*s);
    }
    if (const auto* v = io::GetObjectField(*root, "hero")) {
        if (auto s = io::GetString(*v)) job.hero = std::string(*s);
    }
    if (const auto* v = io::GetObjectField(*root, "level")) {
        if (auto n = io::GetInt(*v)) job.level = *n;
    }
    if (const auto* v = io::GetObjectField(*root, "workers")) {
        if (auto n = io::GetInt(*v)) job.workers = *n;
    }
    const auto* battles = io::GetObjectField(*root, "battles");
    if (!battles || !battles->IsArray()) {
        err = "missing battles array";
        return false;
    }
    for (const auto& e : *battles->AsArray()) {
        BattleReq req;
        if (!ParseBattleEntry(e, req, err)) return false;
        job.battles.push_back(std::move(req));
    }
    return true;
}

/// 单请求（一条 battle 的全部 seeds）串行执行：单局约 30µs，请求间无需并行。
std::string EscapeJson(const std::string& s);  // 定义见下文

std::vector<int> PlayRequest(const BattleReq& req, const core::SideState& base_a,
                             const core::SideState& base_b) {
    std::vector<int> out(req.seeds.size(), -1);
    const size_t n = req.seeds.size();
    for (size_t j = 0; j < n; j++) {
        const int swap = j * 2 >= n ? 1 : 0;  // 后半局数交换物理侧
        const int seed = static_cast<int>(req.seeds[j] & 0x7fffffff);
        out[j] = PlayOneGame(base_a, base_b, swap, seed);
    }
    return out;
}

std::string ResultLine(const std::string& id, const std::vector<int>& results) {
    std::string s = "{\"id\":\"" + EscapeJson(id) + "\",\"results\":[";
    for (size_t j = 0; j < results.size(); j++) {
        if (j) s += ",";
        s += std::to_string(results[j]);
    }
    s += "]}";
    return s;
}

// ---- deck 缓存（SideState 模板；BuildSide 由 GdfItemPrototypeCache 完成全部物品规则）----

class DeckCache {
public:
    DeckCache(const gdf::GdfItemPrototypeCache& protos, int level) : protos_(protos), level_(level) {}

    const core::SideState& Get(const std::vector<std::string>& names) {
        const gdf::DeckRep rep{names};
        const std::string sig = rep.Signature();
        {
            std::shared_lock lk(mu_);
            const auto it = cache_.find(sig);
            if (it != cache_.end()) return it->second;
        }
        core::SideState side = protos_.BuildSide(rep, level_, 0);
        std::unique_lock lk(mu_);
        return cache_.emplace(sig, std::move(side)).first->second;
    }

private:
    const gdf::GdfItemPrototypeCache& protos_;
    int level_;
    std::shared_mutex mu_;
    std::unordered_map<std::string, core::SideState> cache_;
};

/// 自适应波次（C++ 内卷）：对一批 battle 逐波加局直到收敛（精度/符号停止或打满）。
/// 侧平衡约定：game i 的 swap = (i % 2 == 1)（交替换侧，偶数局严格各半；
/// 与 Python 历史的「后半换侧」不同，缓存键前缀 aw: 隔离）。
struct AdaptiveParams {
    double ci_tol = 0.05;
    int batch = 8;
    int max_games = 96;
    double sign_margin = 0.15;
    int64_t base_seed = 1000;
};

struct AdaptiveOutcome {
    std::string id;
    std::vector<int> results;  // 每局结果（按全局局序）
    int wins_a = 0, wins_b = 0, ties = 0;
    double ci_half = 1.0;
    bool decisive = false;
};

double WilsonHalf(double w, int n, double z = 1.96) {
    if (n <= 0) return 1.0;
    const double denom = 1 + z * z / n;
    const double half = z * std::sqrt(w * (1 - w) / n + z * z / (4.0 * n * n)) / denom;
    return half;
}

std::vector<AdaptiveOutcome> RunAdaptiveWave(const std::vector<BattleReq>& battles,
                                             DeckCache& deck_cache, int workers,
                                             const AdaptiveParams& params) {
    if (workers <= 0) {  // 防止零线程死循环（与 RunWave 一致默认硬件并发）
        const unsigned hc = std::thread::hardware_concurrency();
        workers = hc > 0 ? static_cast<int>(hc) : 4;
    }
    struct State {
        const core::SideState* a;
        const core::SideState* b;
        std::vector<int> results;
        bool done = false;
    };
    std::vector<State> states(battles.size());
    for (size_t i = 0; i < battles.size(); i++) {
        states[i].a = &deck_cache.Get(battles[i].a);
        states[i].b = &deck_cache.Get(battles[i].b);
    }
    auto converged = [&](const State& s) {
        const int n = static_cast<int>(s.results.size());
        if (n == 0) return false;
        int wa = 0, wb = 0, t = 0;
        for (int r : s.results) {
            if (r == 0) wa++;
            else if (r == 1) wb++;
            else t++;
        }
        const double wr = (wa + 0.5 * t) / n;
        const double half = WilsonHalf(wr, n);
        const double lo = wr - half, hi = wr + half;
        return half <= params.ci_tol || lo > 0.5 + params.sign_margin ||
               hi < 0.5 - params.sign_margin || n >= params.max_games;
    };
    for (;;) {
        // 组一波：所有未收敛 battle 的下一批局（扁平 (state, 局数) 任务）
        std::vector<std::pair<size_t, int>> tasks;  // (state_idx, games_this_wave)
        for (size_t i = 0; i < states.size(); i++) {
            if (states[i].done) continue;
            const int n = static_cast<int>(states[i].results.size());
            const int target = std::min(n + params.batch, params.max_games);
            if (target > n) tasks.emplace_back(i, target - n);
        }
        if (tasks.empty()) break;
        std::atomic<size_t> next{0};
        const size_t total = tasks.size();
        auto worker = [&] {
            for (;;) {
                const size_t idx = next.fetch_add(1, std::memory_order_relaxed);
                if (idx >= total) return;
                auto [si, cnt] = tasks[idx];
                State& s = states[si];
                const int base = static_cast<int>(s.results.size());
                std::vector<int> local;
                local.reserve(static_cast<size_t>(cnt));
                for (int k = 0; k < cnt; k++) {
                    const int game_i = base + k;
                    const int swap = game_i % 2;
                    const int seed = static_cast<int>((params.base_seed + game_i) & 0x7fffffff);
                    local.push_back(PlayOneGame(*s.a, *s.b, swap, seed));
                }
                // 单 battle 的结果向量为该线程独占（每个 state 至多一个任务），直接追加
                s.results.insert(s.results.end(), local.begin(), local.end());
            }
        };
        {
            std::vector<std::thread> pool;
            pool.reserve(static_cast<size_t>(workers));
            for (int w = 0; w < workers; w++) pool.emplace_back(worker);
            for (auto& t : pool) t.join();
        }
        for (auto& s : states) {
            if (!s.done && converged(s)) s.done = true;
        }
    }
    std::vector<AdaptiveOutcome> out(battles.size());
    for (size_t i = 0; i < battles.size(); i++) {
        out[i].id = battles[i].id;
        out[i].results = std::move(states[i].results);
        for (int r : out[i].results) {
            if (r == 0) out[i].wins_a++;
            else if (r == 1) out[i].wins_b++;
            else out[i].ties++;
        }
        const int n = static_cast<int>(out[i].results.size());
        const double wr = n ? (out[i].wins_a + 0.5 * out[i].ties) / n : 0.0;
        out[i].ci_half = WilsonHalf(wr, n);
        const double lo = wr - out[i].ci_half, hi = wr + out[i].ci_half;
        out[i].decisive = out[i].ci_half <= params.ci_tol || lo > 0.5 + params.sign_margin ||
                          hi < 0.5 - params.sign_margin;
    }
    return out;
}
std::vector<BattleResult> RunWave(const std::vector<BattleReq>& battles, DeckCache& deck_cache,
                                  int workers, double* elapsed_out = nullptr) {
    const size_t nb = battles.size();
    std::vector<const core::SideState*> sides_a(nb), sides_b(nb);
    std::vector<BattleResult> results(nb);
    for (size_t i = 0; i < nb; i++) {
        sides_a[i] = &deck_cache.Get(battles[i].a);
        sides_b[i] = &deck_cache.Get(battles[i].b);
        results[i].id = battles[i].id;
        results[i].results.assign(battles[i].seeds.size(), -1);
    }
    if (workers <= 0) {
        const unsigned hc = std::thread::hardware_concurrency();
        workers = hc > 0 ? static_cast<int>(hc) : 4;
    }
    const size_t total_games = [&] {
        size_t t = 0;
        for (const auto& b : battles) t += b.seeds.size();
        return t;
    }();
    std::atomic<size_t> next{0};
    auto worker = [&] {
        for (;;) {
            const size_t idx = next.fetch_add(1, std::memory_order_relaxed);
            if (idx >= total_games) return;
            size_t bi = 0, j = idx;
            while (j >= battles[bi].seeds.size()) {
                j -= battles[bi].seeds.size();
                bi++;
            }
            const auto& req = battles[bi];
            const int swap = j * 2 >= req.seeds.size() ? 1 : 0;
            const int seed = static_cast<int>(req.seeds[j] & 0x7fffffff);
            results[bi].results[j] = PlayOneGame(*sides_a[bi], *sides_b[bi], swap, seed);
        }
    };
    const auto t0 = std::chrono::steady_clock::now();
    {
        std::vector<std::thread> pool;
        pool.reserve(static_cast<size_t>(workers));
        for (int w = 0; w < workers; w++) pool.emplace_back(worker);
        for (auto& t : pool) t.join();
    }
    if (elapsed_out) {
        *elapsed_out = std::chrono::duration<double>(std::chrono::steady_clock::now() - t0).count();
    }
    return results;
}

std::string EscapeJson(const std::string& s) {
    std::string out;
    out.reserve(s.size() + 8);
    for (char c : s) {
        switch (c) {
            case '"': out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            case '\t': out += "\\t"; break;
            default: out += c;
        }
    }
    return out;
}

}  // namespace

int main(int argc, char** argv) {
    std::string input_path, output_path;
    bool serve = false;
    bool mine_engines = false;
    std::string profiles_path = "out/meta_search/reason_profiles.json";
    bazaararena::meta::MineParams mine_params;
    Job job;
    for (int i = 1; i < argc; i++) {
        std::string_view tok = argv[i];
        if (tok == "--input" && i + 1 < argc) input_path = argv[++i];
        else if (tok == "--output" && i + 1 < argc) output_path = argv[++i];
        else if (tok == "--serve") serve = true;
        else if (tok == "--mine-engines") mine_engines = true;
        else if (tok == "--profiles" && i + 1 < argc) profiles_path = argv[++i];
        else if (tok == "--max-members" && i + 1 < argc) mine_params.max_members = std::atoi(argv[++i]);
        else if (tok == "--max-slots" && i + 1 < argc) mine_params.max_slots = std::atoi(argv[++i]);
        else if (tok == "--family-quota" && i + 1 < argc) mine_params.family_quota = std::atoi(argv[++i]);
        else if (tok == "--channel-quota" && i + 1 < argc) mine_params.channel_quota = std::atoi(argv[++i]);
        else if (tok == "--core-bonus-weight" && i + 1 < argc) mine_params.core_bonus_weight = std::atof(argv[++i]);
        else if (tok == "--data-dir" && i + 1 < argc) job.data_dir = argv[++i];
        else if (tok == "--hero" && i + 1 < argc) job.hero = argv[++i];
        else if (tok == "--level" && i + 1 < argc) job.level = std::atoi(argv[++i]);
        else if (tok == "--workers" && i + 1 < argc) job.workers = std::atoi(argv[++i]);
        else if (tok == "--version") {
            std::printf("bazaararena_meta mode=batch_battle+seeded contract=1\n");
            return 0;
        }
    }

    // ---- 引擎挖掘模式：静态画像 JSON → 闭包挖掘 → 引擎清单 JSON（毫秒级）----
    if (mine_engines) {
        std::unordered_map<std::string, bazaararena::meta::ReasonProfile> profiles;
        std::string err;
        if (!bazaararena::meta::LoadReasonProfiles(profiles_path, profiles, err)) {
            std::fprintf(stderr, "[meta] fatal: %s\n", err.c_str());
            return 2;
        }
        const auto engines = bazaararena::meta::MineEngines(profiles, mine_params);
        const std::string json = bazaararena::meta::MinedEnginesToJson(engines);
        if (output_path.empty()) {
            std::cout << json << "\n";
        } else {
            std::ofstream out(output_path, std::ios::binary);
            out << json << "\n";
        }
        std::fprintf(stderr, "[meta] mine-engines: %zu engines (profiles=%zu)\n",
                     engines.size(), profiles.size());
        return 0;
    }

    if (!serve && (input_path.empty() || output_path.empty())) {
        std::fprintf(stderr,
                     "usage: bazaararena_meta --input job.json --output results.jsonl\n"
                     "       bazaararena_meta --serve [--level 8 --hero Mak --data-dir data/items]\n");
        return 2;
    }

    std::string err;
    if (!serve && !ParseJob(input_path, job, err)) {
        std::fprintf(stderr, "[meta] fatal: %s\n", err.c_str());
        return 2;
    }

    // 物品池与规则（与 GDF 一致：YAML hero 过滤、Mak 排除清单、魂石变体注入、
    // quest 按等级覆写、overridable 缩放——全部在 ItemPool/GdfItemPrototypeCache 内）
    std::unordered_map<std::string, std::string> key_to_hero;
    if (!gdf::LoadItemHeroByKeyFromDataDir(job.data_dir, key_to_hero, err)) {
        std::fprintf(stderr, "[meta] fatal: %s\n", err.c_str());
        return 2;
    }
    gdf::ItemPool pool(job.level, job.hero, {}, key_to_hero);
    gdf::GdfItemPrototypeCache protos(pool, job.level);
    DeckCache deck_cache(protos, job.level);

    // ---- 常驻服务模式：stdin JSONL 请求 → stdout JSONL 结果（每行 flush）----
    if (serve) {
        std::string line;
        std::fprintf(stderr, "[meta] serve ready (level=%d hero=%s)\n", job.level, job.hero.c_str());
        std::fflush(stderr);
        while (std::getline(std::cin, line)) {
            if (line.empty()) continue;
            io::JsonParseError perr;
            auto root = io::ParseJson(line, perr);
            if (!root) {
                std::fprintf(stderr, "[meta] serve: bad json: %s\n", perr.message.c_str());
                continue;
            }
            // 波次行：一行含 battles 数组 → RunWave/RunAdaptiveWave 全线程分派，单行返回
            if (const auto* wave = io::GetObjectField(*root, "battles")) {
                if (!wave->IsArray()) continue;
                std::vector<BattleReq> reqs;
                bool ok = true;
                for (const auto& e : *wave->AsArray()) {
                    BattleReq req;
                    if (!ParseBattleEntry(e, req, err)) {
                        std::fprintf(stderr, "[meta] serve wave: bad request: %s\n", err.c_str());
                        ok = false;
                        break;
                    }
                    reqs.push_back(std::move(req));
                }
                if (!ok) continue;
                int wave_workers = job.workers;
                if (const auto* v = io::GetObjectField(*root, "workers")) {
                    if (auto n = io::GetInt(*v)) wave_workers = *n;
                }
                try {
                    if (const auto* ad = io::GetObjectField(*root, "adaptive")) {
                        // 自适应波次（C++ 内卷）：seeds 字段忽略，按 adaptive 参数加局
                        AdaptiveParams params;
                        if (const auto* v = io::GetObjectField(*ad, "ci_tol")) {
                            if (auto n = io::GetNumber(*v)) params.ci_tol = *n;
                        }
                        if (const auto* v = io::GetObjectField(*ad, "batch")) {
                            if (auto n = io::GetInt(*v)) params.batch = *n;
                        }
                        if (const auto* v = io::GetObjectField(*ad, "max_games")) {
                            if (auto n = io::GetInt(*v)) params.max_games = *n;
                        }
                        if (const auto* v = io::GetObjectField(*ad, "sign_margin")) {
                            if (auto n = io::GetNumber(*v)) params.sign_margin = *n;
                        }
                        if (const auto* v = io::GetObjectField(*ad, "base_seed")) {
                            if (auto n = io::GetNumber(*v)) params.base_seed = static_cast<int64_t>(*n);
                        }
                        // adaptive 模式下 seed 列表无意义，允许为空：补一个占位
                        for (auto& r : reqs) {
                            if (r.seeds.empty()) r.seeds.push_back(0);
                        }
                        const auto res = RunAdaptiveWave(reqs, deck_cache, wave_workers, params);
                        std::string out = "{\"adaptive\":[";
                        for (size_t i = 0; i < res.size(); i++) {
                            if (i) out += ",";
                            out += "{\"id\":\"" + EscapeJson(res[i].id) + "\",\"wins_a\":" +
                                   std::to_string(res[i].wins_a) + ",\"wins_b\":" +
                                   std::to_string(res[i].wins_b) + ",\"ties\":" +
                                   std::to_string(res[i].ties) + ",\"games\":" +
                                   std::to_string(res[i].results.size()) + ",\"ci_half\":" +
                                   std::to_string(res[i].ci_half) + ",\"decisive\":" +
                                   (res[i].decisive ? "true" : "false") + ",\"results\":[";
                            for (size_t j = 0; j < res[i].results.size(); j++) {
                                if (j) out += ",";
                                out += std::to_string(res[i].results[j]);
                            }
                            out += "]}";
                        }
                        out += "]}";
                        std::cout << out << "\n";
                        std::cout.flush();
                        std::fprintf(stderr, "[meta] serve adaptive wave: battles=%zu\n", res.size());
                        continue;
                    }
                    double el = 0;
                    const auto res = RunWave(reqs, deck_cache, wave_workers, &el);
                    std::string out = "{\"wave\":[";
                    for (size_t i = 0; i < res.size(); i++) {
                        if (i) out += ",";
                        out += ResultLine(res[i].id, res[i].results);
                    }
                    out += "]}";
                    std::cout << out << "\n";
                    std::cout.flush();
                    std::fprintf(stderr, "[meta] serve wave: battles=%zu elapsed=%.2fs\n",
                                 res.size(), el);
                } catch (const std::exception& e) {
                    std::fprintf(stderr, "[meta] serve wave: %s\n", e.what());
                }
                continue;
            }
            BattleReq req;
            if (!ParseBattleEntry(*root, req, err)) {
                std::fprintf(stderr, "[meta] serve: bad request: %s\n", err.c_str());
                continue;
            }
            try {
                const auto& sa = deck_cache.Get(req.a);
                const auto& sb = deck_cache.Get(req.b);
                const auto res = PlayRequest(req, sa, sb);
                std::cout << ResultLine(req.id, res) << "\n";
                std::cout.flush();
            } catch (const std::exception& e) {
                std::fprintf(stderr, "[meta] serve: deck build failed for %s: %s\n",
                             req.id.c_str(), e.what());
            }
        }
        return 0;
    }

    if (std::getenv("META_DEBUG_DECK")) {
        for (const auto& b : job.battles) {
            for (const auto* names : {&b.a, &b.b}) {
                const auto& side = deck_cache.Get(*names);
                std::fprintf(stderr, "[deck] n=%d hp=%d\n", side.attrs[core::SideKey::ItemCount],
                             side.attrs[core::SideKey::Hp]);
                for (int i = 0; i < side.attrs[core::SideKey::ItemCount]; i++) {
                    const auto& it = side.items[static_cast<size_t>(i)];
                    std::fprintf(stderr,
                                 "  item[%d] quest=%d custom1=%d dmg=%d burn=%d tier=%d cd=%d\n", i,
                                 it.attrs[core::ItemKey::Quest], it.attrs[core::ItemKey::Custom_1],
                                 it.attrs[core::ItemKey::Damage], it.attrs[core::ItemKey::Burn],
                                 it.attrs[core::ItemKey::Tier], it.attrs[core::ItemKey::Cooldown]);
                }
            }
        }
        return 0;
    }

    // 预解析全部卡组（先填缓存，避免竞态下重复构建）
    const size_t nb = job.battles.size();
    try {
        for (const auto& b : job.battles) {
            deck_cache.Get(b.a);
            deck_cache.Get(b.b);
        }
    } catch (const std::exception& e) {
        std::fprintf(stderr, "[meta] fatal: deck build failed: %s\n", e.what());
        return 2;
    }

    int workers = job.workers;
    if (workers <= 0) {
        const unsigned hc = std::thread::hardware_concurrency();
        workers = hc > 0 ? static_cast<int>(hc) : 4;
    }
    double elapsed = 0;
    const size_t total_games = [&] {
        size_t t = 0;
        for (const auto& b : job.battles) t += b.seeds.size();
        return t;
    }();
    const auto results = RunWave(job.battles, deck_cache, workers, &elapsed);

    std::ofstream out(output_path, std::ios::binary);
    if (!out) {
        std::fprintf(stderr, "[meta] fatal: cannot open output: %s\n", output_path.c_str());
        return 2;
    }
    for (const auto& r : results) {
        int wa = 0, wb = 0, ties = 0;
        for (int x : r.results) {
            if (x == 0) wa++;
            else if (x == 1) wb++;
            else ties++;
        }
        out << "{\"id\":\"" << EscapeJson(r.id) << "\",\"wins_a\":" << wa
            << ",\"wins_b\":" << wb << ",\"ties\":" << ties << ",\"results\":[";
        for (size_t j = 0; j < r.results.size(); j++) {
            if (j) out << ",";
            out << r.results[j];
        }
        out << "]}\n";
    }
    std::fprintf(stderr, "[meta] battles=%zu games=%zu workers=%d elapsed=%.1fs (%.0f games/s)\n",
                 nb, total_games, workers, elapsed, total_games / std::max(1e-9, elapsed));
    return 0;
}
