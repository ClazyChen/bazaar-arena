#include <fstream>
#include <iostream>
#include <random>
#include <sstream>
#include <string>

#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/core/SimulatorInit.hpp>
#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/io/JsonLite.hpp>
#include <bazaararena/io/SideStateBuilder.hpp>
#include <bazaararena/io/SimulateJob.hpp>

namespace io = bazaararena::io;
namespace core = bazaararena::core;

namespace {

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

    int winner = -1;
    bool isDraw = false;

    io::JsonObject result;
    io::JsonObject debugObj;
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
        debugObj["level"] = job.debug.level;
        debugObj["events"] = io::JsonArray{};
        debugObj["truncated"] = false;
        result["debug"] = std::move(debugObj);
    }

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

