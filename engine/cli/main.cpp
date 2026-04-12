#include <algorithm>
#include <array>
#include <atomic>
#include <fstream>
#include <iostream>
#include <random>
#include <sstream>
#include <string>
#include <string_view>
#include <thread>
#include <vector>

#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/core/SimulatorInit.hpp>
#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/engine.hpp>
#include <bazaararena/io/JsonLite.hpp>
#include <bazaararena/io/SideStateBuilder.hpp>
#include <bazaararena/io/SinkExport.hpp>
#include <bazaararena/io/SimulateJob.hpp>

namespace io = bazaararena::io;
namespace core = bazaararena::core;

namespace {

#ifdef _WIN32
#include <windows.h>
static void EnableVirtualTerminalIfPossible() {
    HANDLE h = GetStdHandle(STD_OUTPUT_HANDLE);
    if (h == INVALID_HANDLE_VALUE || h == nullptr) return;
    DWORD mode = 0;
    if (!GetConsoleMode(h, &mode)) return;
    mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
    SetConsoleMode(h, mode);
}
#else
static void EnableVirtualTerminalIfPossible() {}
#endif

struct Rgb {
    int r = 255;
    int g = 255;
    int b = 255;
};

static std::string AnsiFg(const Rgb& c) {
    std::ostringstream os;
    os << "\x1b[38;2;" << c.r << ';' << c.g << ';' << c.b << 'm';
    return os.str();
}

static constexpr const char* AnsiReset() { return "\x1b[0m"; }

static std::string ColorizeSummaryLine(std::string_view line) {
    // Expect "[3.00s] " prefix; colorize only the message part for readability.
    const size_t prefixEnd = line.find("] ");
    const size_t msgStart = (prefixEnd == std::string_view::npos) ? 0 : (prefixEnd + 2);
    const std::string_view prefix = line.substr(0, msgStart);
    const std::string_view msg = line.substr(msgStart);

    struct Rule {
        std::string_view needle;
        Rgb color;
    };
    // Match by keywords in current Chinese summary lines.
    static const Rule rules[] = {
        { "治疗", {97, 176, 60} },
        { "生命上限", {97, 176, 60} },
        { "生命再生", {142, 234, 49} },
        { "弹药", {255, 142, 0} },
        { "装填", {255, 142, 0} },
        { "加速", {0, 236, 195} },
        { "充能", {0, 236, 195} },
        { "冻结", {63, 200, 247} },
        { "护盾", {244, 207, 32} },
        { "飞行", {244, 207, 32} },
        { "伤害", {245, 80, 61} },
        { "暴击率", {245, 80, 61} },
        { "暴击伤害", {245, 80, 61} },
        { "摧毁", {255, 50, 120} },
        { "剧毒", {14, 190, 79} },
        { "修复", {143, 252, 188} },
        { "减速", {203, 159, 110} },
        { "灼烧", {255, 159, 69} },
        { "价值", {255, 215, 0} },
        { "生命窃取", {157, 74, 111} },
        { "多重释放", {178, 228, 228} },
        { "自定义", {180, 180, 200} },
    };

    for (const auto& r : rules) {
        if (msg.find(r.needle) != std::string_view::npos) {
            std::string out;
            out.reserve(line.size() + 32);
            out.append(prefix);
            out.append(AnsiFg(r.color));
            out.append(msg);
            out.append(AnsiReset());
            return out;
        }
    }
    return std::string(line);
}

struct Args {
    std::string inputPath;
    std::string outputPath;
};

enum class CliMode { Simulate, Version, Help };

struct CliInvocation {
    CliMode mode = CliMode::Simulate;
    Args simulate;
};

/** 与 HTTP 层协同：用于确认 exe 为本仓库「对战 JSON」CLI，而非其它同名程序。 */
static constexpr int kCliContractVersion = 1;

static void PrintUsage(std::ostream& os) {
    os << "usage: bazaararena_cli --input <input.json> --output <output.json>\n"
          "       bazaararena_cli --version\n"
          "       bazaararena_cli --help\n";
}

static void PrintVersion(std::ostream& os) {
    const bazaararena::EngineVersion v = bazaararena::GetEngineVersion();
    os << "bazaararena_cli mode=simulate+json contract=" << kCliContractVersion << "\n"
       << "engine_version=" << v.major << '.' << v.minor << '.' << v.patch << "\n";
}

std::optional<CliInvocation> ParseInvocation(int argc, char** argv) {
    for (int i = 1; i < argc; i++) {
        const std::string_view tok = argv[i];
        if (tok == "--version") {
            return CliInvocation{CliMode::Version, {}};
        }
    }
    for (int i = 1; i < argc; i++) {
        const std::string_view tok = argv[i];
        if (tok == "--help" || tok == "-h") {
            return CliInvocation{CliMode::Help, {}};
        }
    }

    Args a;
    for (int i = 1; i < argc; i++) {
        const std::string_view tok = argv[i];
        if (tok == "--input" && i + 1 < argc) {
            a.inputPath = argv[++i];
        } else if (tok == "--output" && i + 1 < argc) {
            a.outputPath = argv[++i];
        }
    }
    if (a.inputPath.empty() || a.outputPath.empty()) return std::nullopt;
    return CliInvocation{CliMode::Simulate, std::move(a)};
}

std::optional<std::string> ReadAllText(const std::string& path, std::string& err) {
    std::ifstream ifs(path, std::ios::binary);
    if (!ifs) {
        err = "failed to open input: " + path;
        return std::nullopt;
    }
    std::ostringstream ss;
    ss << ifs.rdbuf();
    return ss.str();
}

bool WriteAllText(const std::string& path, const std::string& text, std::string& err) {
    if (path == "-") {
        std::cout.write(text.data(), static_cast<std::streamsize>(text.size()));
        if (!std::cout) {
            err = "failed to write stdout";
            return false;
        }
        return true;
    }
    std::ofstream ofs(path, std::ios::binary);
    if (!ofs) {
        err = "failed to open output: " + path;
        return false;
    }
    ofs.write(text.data(), static_cast<std::streamsize>(text.size()));
    if (!ofs) {
        err = "failed to write output: " + path;
        return false;
    }
    return true;
}

io::JsonObject MakeErrorOut(int schemaVersion, const std::string& jobId, const std::string& error) {
    io::JsonObject root;
    root["schemaVersion"] = static_cast<double>(schemaVersion);
    if (!jobId.empty()) root["jobId"] = jobId;
    root["ok"] = false;
    root["error"] = error;
    root["result"] = io::JsonObject{};
    return root;
}

static void RunBatchSlice(
    const io::SimulateJob& job,
    const std::array<core::SideState, 2>& templateSides,
    int baseSeed,
    int begin,
    int end,
    std::atomic<uint64_t>& wins0,
    std::atomic<uint64_t>& wins1,
    std::atomic<uint64_t>& draws) {
    core::Simulator sim;
    sim.sink.sink_type = io::Sink::TypeNone;
    sim.sink.max_events = job.debug.maxEvents;
    for (int i = begin; i < end; ++i) {
        sim.sides[0] = templateSides[0];
        sim.sides[1] = templateSides[1];
        sim.sandstorm = {};
        sim.sink.Clear();
        const int64_t combined = static_cast<int64_t>(baseSeed) + static_cast<int64_t>(i);
        sim.rng.Seed(static_cast<int>(combined));
        core::InitializeSimulator(sim);
        const int w = sim.Run(job.allowTie);
        if (w < 0) {
            draws.fetch_add(1, std::memory_order_relaxed);
        } else if (w == 0) {
            wins0.fetch_add(1, std::memory_order_relaxed);
        } else {
            wins1.fetch_add(1, std::memory_order_relaxed);
        }
    }
}

}  // namespace

int main(int argc, char** argv) {
    const auto invOpt = ParseInvocation(argc, argv);
    if (!invOpt) {
        PrintUsage(std::cerr);
        return 2;
    }
    const CliInvocation& inv = *invOpt;
    if (inv.mode == CliMode::Version) {
        PrintVersion(std::cout);
        return 0;
    }
    if (inv.mode == CliMode::Help) {
        PrintUsage(std::cout);
        return 0;
    }

    const Args& args = inv.simulate;
    if (args.outputPath == "-") {
        EnableVirtualTerminalIfPossible();
    }

    std::string ioErr;
    const auto textOpt = ReadAllText(args.inputPath, ioErr);
    if (!textOpt) {
        std::cerr << ioErr << "\n";
        return 2;
    }

    auto parsed = io::ParseSimulateJobJson(*textOpt);
    if (!parsed.job) {
        auto out = MakeErrorOut(1, "", parsed.error);
        const std::string outText = io::StringifyJson(out);
        if (!WriteAllText(args.outputPath, outText, ioErr)) {
            std::cerr << ioErr << "\n";
            return 2;
        }
        return 1;
    }
    const auto& job = *parsed.job;

    std::array<core::SideState, 2> builtSides{};
    for (int si = 0; si < 2; si++) {
        auto built = io::BuildSideState(job.sides[si]);
        if (!built.side) {
            auto out = MakeErrorOut(job.schemaVersion, job.jobId, built.error);
            const std::string outText = io::StringifyJson(out);
            if (!WriteAllText(args.outputPath, outText, ioErr)) {
                std::cerr << ioErr << "\n";
                return 2;
            }
            return 1;
        }
        builtSides[si] = *built.side;
    }

    if (job.isSimulateBatch) {
        const int n = job.batchCount;
        const int tCount = (std::min)(job.batchThreads, n);
        const int baseSeed = job.seed.value_or(static_cast<int>(std::random_device()()));
        std::atomic<uint64_t> wins0{0};
        std::atomic<uint64_t> wins1{0};
        std::atomic<uint64_t> draws{0};
        std::vector<std::thread> workers;
        workers.reserve(static_cast<size_t>(tCount));
        for (int t = 0; t < tCount; ++t) {
            const int begin = t * n / tCount;
            const int end = (t + 1) * n / tCount;
            workers.emplace_back([&, begin, end]() {
                RunBatchSlice(job, builtSides, baseSeed, begin, end, wins0, wins1, draws);
            });
        }
        for (auto& w : workers) {
            w.join();
        }

        io::JsonObject result;
        result["mode"] = std::string("batch");
        result["totalRuns"] = static_cast<double>(n);
        result["threadsUsed"] = static_cast<double>(tCount);
        result["baseSeed"] = static_cast<double>(baseSeed);
        result["allowTie"] = job.allowTie;
        result["winsSide0"] = static_cast<double>(wins0.load());
        result["winsSide1"] = static_cast<double>(wins1.load());
        result["draws"] = static_cast<double>(draws.load());

        io::JsonObject outRoot;
        outRoot["schemaVersion"] = static_cast<double>(job.schemaVersion);
        if (!job.jobId.empty()) outRoot["jobId"] = job.jobId;
        outRoot["ok"] = true;
        outRoot["error"] = "";
        outRoot["result"] = std::move(result);

        const std::string outText = io::StringifyJson(outRoot);
        if (!WriteAllText(args.outputPath, outText, ioErr)) {
            std::cerr << ioErr << "\n";
            return 2;
        }
        return 0;
    }

    core::Simulator sim;
    sim.sides[0] = builtSides[0];
    sim.sides[1] = builtSides[1];

    // RNG seed
    int seed = job.seed.value_or(static_cast<int>(std::random_device()()));
    sim.rng.Seed(seed);

    core::InitializeSimulator(sim);

    // debug sink
    sim.sink.Clear();
    sim.sink.max_events = job.debug.maxEvents;
    if (!job.debug.enabled || job.debug.level == "none") {
        sim.sink.sink_type = io::Sink::TypeNone;
    } else if (job.debug.level == "summary") {
        sim.sink.sink_type = io::Sink::TypeSummary;
    } else if (job.debug.level == "detailed") {
        sim.sink.sink_type = io::Sink::TypeDetailed;
    } else {
        sim.sink.sink_type = io::Sink::TypeNone;
    }

    int winner = -1;
    bool isDraw = false;

    io::JsonObject result;
    winner = sim.Run(job.allowTie);
    isDraw = (winner < 0);

    result["winner"] = static_cast<double>(winner);
    result["isDraw"] = isDraw;
    result["endTimeMs"] = static_cast<double>(sim.time);

    io::JsonObject finalObj;
    io::JsonArray sidesArr;
    for (int si = 0; si < 2; si++) {
        io::JsonObject s;
        const auto& a = sim.sides[si].attrs;
        s["maxHp"] = static_cast<double>(a[core::SideKey::MaxHp]);
        s["hp"] = static_cast<double>(a[core::SideKey::Hp]);
        s["shield"] = static_cast<double>(a[core::SideKey::Shield]);
        s["burn"] = static_cast<double>(a[core::SideKey::Burn]);
        s["poison"] = static_cast<double>(a[core::SideKey::Poison]);
        s["regen"] = static_cast<double>(a[core::SideKey::Regen]);
        s["resistance"] = static_cast<double>(a[core::SideKey::Resistance]);
        s["gold"] = static_cast<double>(a[core::SideKey::Gold]);
        s["income"] = static_cast<double>(a[core::SideKey::Income]);
        if (job.debug.enabled && job.debug.level == "detailed") {
            io::AppendDetailedSideItemsJson(s, sim, si);
        }
        sidesArr.emplace_back(std::move(s));
    }
    finalObj["sides"] = std::move(sidesArr);
    result["final"] = std::move(finalObj);

    if (job.debug.enabled && job.debug.level != "none") {
        io::JsonObject debugObj;
        io::FillDebugJson(debugObj, sim.sink);
        result["debug"] = std::move(debugObj);
    }

    io::JsonObject outRoot;
    outRoot["schemaVersion"] = static_cast<double>(job.schemaVersion);
    if (!job.jobId.empty()) outRoot["jobId"] = job.jobId;
    outRoot["ok"] = true;
    outRoot["error"] = "";
    outRoot["result"] = std::move(result);

    // Console mode: in summary level, print colorized lines directly.
    if (args.outputPath == "-" && job.debug.enabled && job.debug.level == "summary") {
        for (const auto& s : sim.sink.lines) {
            const std::string colored = ColorizeSummaryLine(s);
            std::cout << colored << "\n";
        }
        return 0;
    }

    const std::string outText = io::StringifyJson(outRoot);
    if (!WriteAllText(args.outputPath, outText, ioErr)) {
        std::cerr << ioErr << "\n";
        return 2;
    }
    return 0;
}

