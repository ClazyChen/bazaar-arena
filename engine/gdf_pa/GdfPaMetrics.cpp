#include <bazaararena/gdf_pa/GdfPaMetrics.hpp>
#include <bazaararena/gdf_pa/GdfPaCsv.hpp>

#include <algorithm>
#include <cmath>
#include <fstream>
#include <iomanip>
#include <unordered_map>
#include <unordered_set>

namespace bazaararena::gdf_pa {
namespace {

static std::vector<std::string> SplitDeckSig(std::string_view sig) {
    std::vector<std::string> out;
    size_t i = 0;
    while (i < sig.size()) {
        while (i < sig.size() && (sig[i] == ',' || sig[i] == ' ' || sig[i] == '\t')) i++;
        if (i >= sig.size()) break;
        size_t j = i;
        while (j < sig.size() && sig[j] != ',') j++;
        std::string s(sig.substr(i, j - i));
        while (!s.empty() && (s.back() == ' ' || s.back() == '\t')) s.pop_back();
        if (!s.empty()) out.push_back(std::move(s));
        i = j + 1;
    }
    return out;
}

static double HarmonicK(int k) {
    double h = 0;
    for (int i = 1; i <= k; ++i) h += 1.0 / static_cast<double>(i);
    return h;
}

}  // namespace

bool ComputeGeneralityTable(const std::vector<FullTopkAnchorBlock>& blocks, int top_k,
    const std::vector<std::string>& pool_items, std::vector<GeneralityRow>& out, std::string& err) {
    err.clear();
    out.clear();
    if (top_k <= 0) {
        err = "top_k must be positive";
        return false;
    }
    const double hk = HarmonicK(top_k);

    std::unordered_map<std::string, std::unordered_map<std::string, double>> alpha;
    // alpha[item][anchor] = S_ij / H_k

    for (const auto& block : blocks) {
        const std::string& anchor = block.anchor_item;
        for (const auto& row : block.ranks) {
            if (row.rank > top_k) continue;
            if (row.rank <= 0) continue;
            const double contrib = (1.0 / static_cast<double>(row.rank)) / hk;
            const auto items = SplitDeckSig(row.deck_signature);
            std::unordered_set<std::string> seen;
            for (const auto& n : items) seen.insert(n);
            for (const auto& item : pool_items) {
                if (item == anchor) continue;
                if (seen.count(item)) alpha[item][anchor] += contrib;
            }
        }
    }

    for (const auto& item : pool_items) {
        GeneralityRow gr;
        gr.item = item;
        double sum = 0;
        int cnt = 0;
        for (const auto& block : blocks) {
            const std::string& anchor = block.anchor_item;
            if (anchor == item) continue;
            const auto it = alpha[item].find(anchor);
            const double v = (it == alpha[item].end()) ? 0 : it->second;
            gr.anchor_alphas.push_back({anchor, v});
            sum += v;
            cnt++;
        }
        std::sort(gr.anchor_alphas.begin(), gr.anchor_alphas.end(),
            [](const std::pair<std::string, double>& a, const std::pair<std::string, double>& b) {
                if (a.second != b.second) return a.second > b.second;
                return a.first < b.first;
            });
        if (cnt > 0) gr.gen = sum / static_cast<double>(cnt);
        out.push_back(std::move(gr));
    }
    return true;
}

bool ComputeSpecialtyTable(const std::vector<FullTopkAnchorBlock>& blocks, std::vector<SpecialtyRow>& out,
    std::vector<std::string>& rr_skip_log, std::string& err) {
    err.clear();
    out.clear();
    rr_skip_log.clear();
    for (const auto& block : blocks) {
        SpecialtyRow sr;
        sr.anchor = block.anchor_item;
        double num = 0;
        double den = 0;
        for (const auto& row : block.ranks) {
            if (row.rank <= 0) continue;
            if (row.rr == 0.0) {
                rr_skip_log.push_back("skip RR=0 anchor=" + block.anchor_item + " rank=" + std::to_string(row.rank));
                continue;
            }
            const double wl = 1.0 / static_cast<double>(row.rank);
            const double f = std::pow(row.anchor_m / row.rr, 3.0);
            num += wl * f;
            den += wl;
        }
        if (den > 0) sr.spec = num / den;
        out.push_back(std::move(sr));
    }
    return true;
}

void SortAndRankGeneralityForOutput(std::vector<GeneralityRow>& rows) {
    std::sort(rows.begin(), rows.end(), [](const GeneralityRow& a, const GeneralityRow& b) {
        if (a.gen != b.gen) return a.gen > b.gen;
        return a.item < b.item;
    });
    int r = 1;
    for (size_t i = 0; i < rows.size(); ++i) {
        if (i > 0 && rows[i].gen != rows[i - 1].gen) r = static_cast<int>(i) + 1;
        rows[i].rank = r;
    }
}

void SortAndRankSpecialtyForOutput(std::vector<SpecialtyRow>& rows) {
    std::sort(rows.begin(), rows.end(), [](const SpecialtyRow& a, const SpecialtyRow& b) {
        if (a.spec != b.spec) return a.spec > b.spec;
        return a.anchor < b.anchor;
    });
    int r = 1;
    for (size_t i = 0; i < rows.size(); ++i) {
        if (i > 0 && rows[i].spec != rows[i - 1].spec) r = static_cast<int>(i) + 1;
        rows[i].rank = r;
    }
}

bool WriteGeneralityCsv(const std::string& path, const std::vector<GeneralityRow>& rows, std::string& err) {
    std::ofstream f(path, std::ios::binary);
    if (!f) {
        err = "cannot write: " + path;
        return false;
    }
    f << "rank,item,generality\n";
    f << std::setprecision(17);
    for (const auto& r : rows) {
        f << r.rank << ',' << CsvEscapeField(r.item) << ',' << r.gen << '\n';
    }
    return true;
}

bool WriteGeneralityPerAnchorCsv(const std::string& path, const std::vector<GeneralityRow>& rows, std::string& err) {
    std::ofstream f(path, std::ios::binary);
    if (!f) {
        err = "cannot write: " + path;
        return false;
    }
    f << "item_rank,item,anchor,alpha_ij\n";
    f << std::setprecision(17);
    for (const auto& r : rows) {
        for (const auto& pa : r.anchor_alphas) {
            f << r.rank << ',' << CsvEscapeField(r.item) << ',' << CsvEscapeField(pa.first) << ',' << pa.second << '\n';
        }
    }
    return true;
}

bool WriteSpecialtyCsv(const std::string& path, const std::vector<SpecialtyRow>& rows, std::string& err) {
    std::ofstream f(path, std::ios::binary);
    if (!f) {
        err = "cannot write: " + path;
        return false;
    }
    f << "rank,anchor,specialty\n";
    f << std::setprecision(17);
    for (const auto& r : rows) {
        f << r.rank << ',' << CsvEscapeField(r.anchor) << ',' << r.spec << '\n';
    }
    return true;
}

}  // namespace bazaararena::gdf_pa
