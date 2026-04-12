#include <bazaararena/gdf/GdfLoadYamlPool.hpp>

#include <filesystem>
#include <fstream>
#include <sstream>
#include <string_view>

namespace fs = std::filesystem;

namespace bazaararena::gdf {
namespace {

static std::string Trim(std::string_view s) {
    while (!s.empty() && (s.front() == ' ' || s.front() == '\t')) s.remove_prefix(1);
    while (!s.empty() && (s.back() == ' ' || s.back() == '\t' || s.back() == '\r')) s.remove_suffix(1);
    return std::string(s);
}

static bool ReadFile(const fs::path& p, std::string& out, std::string& err) {
    std::ifstream ifs(p, std::ios::binary);
    if (!ifs) {
        err = "cannot open: " + p.string();
        return false;
    }
    std::ostringstream ss;
    ss << ifs.rdbuf();
    out = ss.str();
    return true;
}

}  // namespace

bool LoadItemHeroByKeyFromDataDir(const std::string& data_items_dir, std::unordered_map<std::string, std::string>& out_key_to_hero,
    std::string& error) {
    out_key_to_hero.clear();
    fs::path dir(data_items_dir);
    if (!fs::is_directory(dir)) {
        error = "data items path is not a directory: " + data_items_dir;
        return false;
    }
    for (const auto& ent : fs::directory_iterator(dir)) {
        if (!ent.is_regular_file()) continue;
        if (ent.path().extension() != ".yaml" && ent.path().extension() != ".yml") continue;
        std::string text;
        if (!ReadFile(ent.path(), text, error)) return false;
        std::string file_hero;
        std::istringstream iss(text);
        std::string line;
        while (std::getline(iss, line)) {
            auto t = Trim(line);
            if (t.rfind("hero:", 0) == 0) {
                std::string rest = Trim(t.substr(5));
                file_hero = rest;
            }
        }
        if (file_hero.empty()) {
            error = "missing hero: in " + ent.path().string();
            return false;
        }
        // 提取 Name: "..."（与 YAML 生成约定一致）
        const std::string key = "Name:";
        size_t pos = 0;
        while (pos < text.size()) {
            size_t at = text.find(key, pos);
            if (at == std::string::npos) break;
            size_t q1 = text.find('"', at + key.size());
            if (q1 == std::string::npos) break;
            size_t q2 = text.find('"', q1 + 1);
            if (q2 == std::string::npos) break;
            std::string name = text.substr(q1 + 1, q2 - q1 - 1);
            if (!name.empty()) out_key_to_hero[std::move(name)] = file_hero;
            pos = q2 + 1;
        }
    }
    if (out_key_to_hero.empty()) {
        error = "no items loaded from " + data_items_dir;
        return false;
    }
    error.clear();
    return true;
}

}  // namespace bazaararena::gdf
