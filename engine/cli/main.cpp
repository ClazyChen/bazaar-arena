#include <fstream>
#include <iostream>
#include <random>
#include <sstream>
#include <string>
#include <string_view>
#include <vector>

#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/core/SimulatorInit.hpp>
#include <bazaararena/core/SideKey.hpp>
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

std::optional<Args> ParseArgs(int argc, char** argv) {
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
    return a;
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

}  // namespace

int main(int argc, char** argv) {
    const auto argsOpt = ParseArgs(argc, argv);
    if (!argsOpt) {
        std::cerr << "usage: bazaararena_cli --input <input.json> --output <output.json>\n";
        return 2;
    }
    const auto& args = *argsOpt;
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

    core::Simulator sim;
    // sides
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
        sim.sides[si] = *built.side;
    }

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

