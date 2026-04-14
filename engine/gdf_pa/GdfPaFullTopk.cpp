#include <bazaararena/gdf_pa/GdfPaFullTopk.hpp>

#include <cctype>
#include <fstream>
#include <sstream>

namespace bazaararena::gdf_pa {
namespace {

static std::string TrimCopy(std::string_view s) {
    size_t i = 0;
    while (i < s.size() && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r')) i++;
    size_t j = s.size();
    while (j > i && (s[j - 1] == ' ' || s[j - 1] == '\t' || s[j - 1] == '\r')) j--;
    return std::string(s.substr(i, j - i));
}

static bool StartsWith(std::string_view line, std::string_view pfx) {
    return line.size() >= pfx.size() && line.compare(0, pfx.size(), pfx) == 0;
}

static bool ParseRankLine(std::string_view line, FullTopkRankRow& row, std::string& err) {
    // "  1. RR=... anchor_m=... Swiss=... | sig"
    size_t i = 0;
    while (i < line.size() && std::isspace(static_cast<unsigned char>(line[i]))) i++;
    if (i >= line.size()) {
        err = "empty rank line";
        return false;
    }
    int rank = 0;
    while (i < line.size() && std::isdigit(static_cast<unsigned char>(line[i]))) {
        rank = rank * 10 + (line[i] - '0');
        i++;
    }
    if (rank <= 0 || i >= line.size() || line[i] != '.') {
        err = "bad rank prefix";
        return false;
    }
    i++;
    while (i < line.size() && std::isspace(static_cast<unsigned char>(line[i]))) i++;
    if (!StartsWith(line.substr(i), "RR=")) {
        err = "expected RR=";
        return false;
    }
    i += 3;
    size_t rr_end = line.find(' ', i);
    if (rr_end == std::string::npos) {
        err = "RR parse";
        return false;
    }
    row.rr = std::stod(std::string(line.substr(i, rr_end - i)));
    i = rr_end + 1;
    while (i < line.size() && std::isspace(static_cast<unsigned char>(line[i]))) i++;
    if (!StartsWith(line.substr(i), "anchor_m=")) {
        err = "expected anchor_m=";
        return false;
    }
    i += 9;
    size_t am_end = line.find(' ', i);
    if (am_end == std::string::npos) {
        err = "anchor_m parse";
        return false;
    }
    row.anchor_m = std::stod(std::string(line.substr(i, am_end - i)));
    i = am_end + 1;
    while (i < line.size() && std::isspace(static_cast<unsigned char>(line[i]))) i++;
    if (!StartsWith(line.substr(i), "Swiss=")) {
        err = "expected Swiss=";
        return false;
    }
    i += 6;
    size_t sw_end = line.find(' ', i);
    if (sw_end == std::string::npos) {
        err = "Swiss parse";
        return false;
    }
    row.swiss = std::stod(std::string(line.substr(i, sw_end - i)));
    i = sw_end + 1;
    while (i < line.size() && std::isspace(static_cast<unsigned char>(line[i]))) i++;
    if (i >= line.size() || line[i] != '|') {
        err = "expected |";
        return false;
    }
    i++;
    row.deck_signature = TrimCopy(line.substr(i));
    row.rank = rank;
    return true;
}

[[nodiscard]] bool ParseFullTopkLines(std::vector<std::string> lines, std::vector<FullTopkAnchorBlock>& out, std::string& err) {
    out.clear();
    FullTopkAnchorBlock* cur = nullptr;

    auto flush_anchor = [&]() {
        cur = nullptr;
    };

    for (std::string& raw : lines) {
        if (!raw.empty() && raw.back() == '\r') raw.pop_back();
        std::string_view line = raw;
        if (line.empty()) continue;
        if (line[0] == '#') continue;
        if (StartsWith(line, "[GDF] seeds:")) {
            flush_anchor();
            FullTopkAnchorBlock block;
            block.anchor_item = TrimCopy(line.substr(std::string_view("[GDF] seeds:").size()));
            out.push_back(std::move(block));
            cur = &out.back();
            continue;
        }
        if (StartsWith(line, "[GDF] size=")) {
            if (!cur) {
                err = "[GDF] size= without anchor";
                return false;
            }
            cur->size_line = std::string(line);
            cur->ranks.clear();
            continue;
        }
        if (cur && !cur->size_line.empty()) {
            FullTopkRankRow row;
            if (!ParseRankLine(line, row, err)) return false;
            cur->ranks.push_back(std::move(row));
        }
    }
    return true;
}

}  // namespace

bool ParseFullTopkString(std::string_view text, std::vector<FullTopkAnchorBlock>& out, std::string& err) {
    std::vector<std::string> lines;
    std::string acc;
    for (char c : text) {
        if (c == '\n') {
            lines.push_back(std::move(acc));
            acc.clear();
        } else {
            acc.push_back(c);
        }
    }
    if (!acc.empty()) lines.push_back(std::move(acc));
    return ParseFullTopkLines(std::move(lines), out, err);
}

bool ParseFullTopkFile(const std::string& path_utf8, std::vector<FullTopkAnchorBlock>& out, std::string& err) {
    std::ifstream in(path_utf8, std::ios::binary);
    if (!in) {
        err = "cannot open: " + path_utf8;
        return false;
    }
    std::ostringstream ss;
    ss << in.rdbuf();
    return ParseFullTopkString(ss.str(), out, err);
}

}  // namespace bazaararena::gdf_pa
