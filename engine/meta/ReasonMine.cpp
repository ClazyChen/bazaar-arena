#include "ReasonMine.hpp"

#include <algorithm>
#include <cmath>
#include <fstream>
#include <sstream>

namespace bazaararena::meta {
namespace io = bazaararena::io;

namespace {

void ReadStringSet(const io::JsonValue& obj, const char* key, std::unordered_set<std::string>& out) {
    const auto* arr = io::GetObjectField(obj, key);
    if (!arr || !arr->IsArray()) return;
    for (const auto& e : *arr->AsArray()) {
        if (auto s = io::GetString(e)) out.emplace(*s);
    }
}

int CountIntersect(const std::unordered_set<std::string>& a,
                   const std::unordered_set<std::string>& b) {
    int n = 0;
    for (const auto& x : a) {
        if (b.count(x)) n++;
    }
    return n;
}

struct Edges {
    // edges[a] 中每个 b 的加权理由分（通道封顶：trigger≤2, resource≤2, selector≤1, feed≤0.5）
    std::unordered_map<std::string, std::unordered_map<std::string, double>> w;
    std::unordered_map<std::string, std::unordered_set<std::string>> neigh;
};

Edges BuildEdges(const std::unordered_map<std::string, ReasonProfile>& profiles) {
    Edges ed;
    for (const auto& [a, pa] : profiles) {
        const bool a_active = pa.produces.count("UseItem") > 0;
        for (const auto& [b, pb] : profiles) {
            if (a == b) continue;
            int n_tr = CountIntersect(pa.consumes_trigger, pb.produces);
            if (pa.consumes_trigger.count("UseItem") && pb.produces.count("UseItem")) n_tr--;
            const int n_re = CountIntersect(pa.consumes_resource, pb.produces);
            const int n_se = CountIntersect(pa.selects_tags, pb.tags);
            double w = std::min(n_tr, 2) + std::min(n_re, 2) + std::min(n_se, 1);
            if (a_active && !pb.feeds.empty()) w += 0.5;  // feed 弱边（每条至多 0.5）
            if (w > 0) {
                ed.w[a][b] = w;
                ed.neigh[a].insert(b);
                ed.neigh[b].insert(a);
            }
        }
    }
    return ed;
}

double InternalEdges(const Edges& ed, const std::vector<std::string>& sub) {
    double total = 0;
    for (size_t i = 0; i < sub.size(); i++) {
        for (size_t j = 0; j < sub.size(); j++) {
            if (i == j) continue;
            const auto ita = ed.w.find(sub[i]);
            if (ita == ed.w.end()) continue;
            const auto itb = ita->second.find(sub[j]);
            if (itb != ita->second.end()) total += itb->second;
        }
    }
    return total;
}

bool ClosureOk(const Edges& ed, const std::vector<std::string>& sub) {
    for (size_t i = 0; i < sub.size(); i++) {
        bool any = false;
        for (size_t j = 0; j < sub.size(); j++) {
            if (i == j) continue;
            const auto ita = ed.w.find(sub[i]);
            if (ita != ed.w.end() && ita->second.count(sub[j])) { any = true; break; }
            const auto itb = ed.w.find(sub[j]);
            if (itb != ed.w.end() && itb->second.count(sub[i])) { any = true; break; }
        }
        if (!any) return false;
    }
    return true;
}

std::string EscapeJson(const std::string& s) {
    std::string out;
    for (char c : s) {
        switch (c) {
            case '"': out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            default: out += c;
        }
    }
    return out;
}

}  // namespace

bool LoadReasonProfiles(const std::string& path,
                        std::unordered_map<std::string, ReasonProfile>& out,
                        std::string& error) {
    std::ifstream in(path, std::ios::binary);
    if (!in) {
        error = "cannot open profiles: " + path;
        return false;
    }
    std::stringstream ss;
    ss << in.rdbuf();
    io::JsonParseError perr;
    auto root = io::ParseJson(ss.str(), perr);
    if (!root || !root->IsObject()) {
        error = "profiles JSON parse error: " + perr.message;
        return false;
    }
    for (const auto& [name, v] : *root->AsObject()) {
        ReasonProfile p;
        if (const auto* f = io::GetObjectField(v, "size")) {
            if (auto n = io::GetInt(*f)) p.size = *n;
        }
        ReadStringSet(v, "tags", p.tags);
        ReadStringSet(v, "produces", p.produces);
        ReadStringSet(v, "consumes_trigger", p.consumes_trigger);
        ReadStringSet(v, "consumes_resource", p.consumes_resource);
        ReadStringSet(v, "selects_tags", p.selects_tags);
        ReadStringSet(v, "feeds", p.feeds);
        if (const auto* f = io::GetObjectField(v, "core_score")) {
            if (auto n = io::GetNumber(*f)) p.core_score = *n;
        }
        if (const auto* f = io::GetObjectField(v, "core_channel")) {
            if (auto s = io::GetString(*f)) p.core_channel = std::string(*s);
        }
        out[name] = std::move(p);
    }
    return true;
}

std::vector<MinedEngine> MineEngines(const std::unordered_map<std::string, ReasonProfile>& profiles,
                                     const MineParams& params) {
    const Edges ed = BuildEdges(profiles);
    std::vector<std::string> names;
    names.reserve(profiles.size());
    for (const auto& [n, _] : profiles) names.push_back(n);
    std::sort(names.begin(), names.end());

    auto size_of = [&](const std::vector<std::string>& sub) {
        int s = 0;
        for (const auto& n : sub) s += profiles.at(n).size;
        return s;
    };
    auto core_of = [&](const std::vector<std::string>& sub) {
        return *std::max_element(sub.begin(), sub.end(), [&](const std::string& x, const std::string& y) {
            return profiles.at(x).core_score < profiles.at(y).core_score;
        });
    };
    auto score_of = [&](const std::vector<std::string>& sub) {
        double core_bonus = 0;
        for (const auto& n : sub) core_bonus = std::max(core_bonus, profiles.at(n).core_score);
        return InternalEdges(ed, sub) + params.core_bonus_weight * core_bonus;
    };
    auto has_core = [&](const std::vector<std::string>& sub) {
        for (const auto& n : sub) {
            if (profiles.at(n).core_score > 0) return true;
        }
        return false;
    };

    std::unordered_map<std::string, double> found;  // key = sorted members join
    auto key_of = [](const std::vector<std::string>& sub) {
        std::string k;
        for (size_t i = 0; i < sub.size(); i++) {
            if (i) k += "\x1";
            k += sub[i];
        }
        return k;
    };

    // 2 件：核心件 × 其邻域
    for (const auto& a : names) {
        if (profiles.at(a).core_score <= 0) continue;
        const auto it = ed.neigh.find(a);
        if (it == ed.neigh.end()) continue;
        for (const auto& b : it->second) {
            std::vector<std::string> sub{a, b};
            std::sort(sub.begin(), sub.end());
            if (size_of(sub) > params.max_slots) continue;
            const double sc = score_of(sub);
            if (sc >= params.min_pair && ClosureOk(ed, sub)) {
                found[key_of(sub)] = sc;
            }
        }
    }
    // 3..max_members：共享邻域扩展（新成员须与 ≥2 个现有成员有边）
    std::vector<std::vector<std::string>> frontier;
    frontier.reserve(found.size());
    for (const auto& [k, _] : found) {
        std::vector<std::string> sub;
        size_t pos = 0;
        while (true) {
            const size_t sep = k.find('\x1', pos);
            if (sep == std::string::npos) { sub.push_back(k.substr(pos)); break; }
            sub.push_back(k.substr(pos, sep - pos));
            pos = sep + 1;
        }
        frontier.push_back(std::move(sub));
    }
    for (int ksize = 3; ksize <= params.max_members && !frontier.empty(); ksize++) {
        std::unordered_map<std::string, double> nxt;
        for (const auto& sub : frontier) {
            std::unordered_map<std::string, int> votes;
            for (const auto& m : sub) {
                const auto it = ed.neigh.find(m);
                if (it == ed.neigh.end()) continue;
                for (const auto& c : it->second) {
                    if (std::find(sub.begin(), sub.end(), c) == sub.end()) votes[c]++;
                }
            }
            for (const auto& [c, v] : votes) {
                if (v < 2) continue;
                std::vector<std::string> newsub = sub;
                newsub.push_back(c);
                std::sort(newsub.begin(), newsub.end());
                const std::string k = key_of(newsub);
                if (nxt.count(k) || size_of(newsub) > params.max_slots || !ClosureOk(ed, newsub)) continue;
                const double sc = score_of(newsub);
                if (sc >= params.per_member * ksize) nxt[k] = sc;
            }
        }
        for (const auto& [k, sc] : nxt) found[k] = sc;
        frontier.clear();
        frontier.reserve(nxt.size());
        for (const auto& [k, _] : nxt) {
            std::vector<std::string> sub;
            size_t pos = 0;
            while (true) {
                const size_t sep = k.find('\x1', pos);
                if (sep == std::string::npos) { sub.push_back(k.substr(pos)); break; }
                sub.push_back(k.substr(pos, sep - pos));
                pos = sep + 1;
            }
            frontier.push_back(std::move(sub));
        }
    }

    // 排序、Jaccard 去重、两级配额
    std::vector<std::pair<std::string, double>> ranked(found.begin(), found.end());
    std::sort(ranked.begin(), ranked.end(), [](const auto& x, const auto& y) { return x.second > y.second; });
    std::vector<MinedEngine> kept;
    std::unordered_map<std::string, int> family_count, channel_count;
    auto split = [](const std::string& k) {
        std::vector<std::string> sub;
        size_t pos = 0;
        while (true) {
            const size_t sep = k.find('\x1', pos);
            if (sep == std::string::npos) { sub.push_back(k.substr(pos)); break; }
            sub.push_back(k.substr(pos, sep - pos));
            pos = sep + 1;
        }
        return sub;
    };
    for (const auto& [k, sc] : ranked) {
        const auto sub = split(k);
        bool dup = false;
        for (const auto& e : kept) {
            int inter = 0;
            for (const auto& m : sub) {
                if (std::find(e.members.begin(), e.members.end(), m) != e.members.end()) inter++;
            }
            const int uni = static_cast<int>(sub.size() + e.members.size() - inter);
            if (uni > 0 && static_cast<double>(inter) / uni >= 0.75) { dup = true; break; }
        }
        if (dup) continue;
        const std::string fam = core_of(sub);
        const std::string ch = profiles.at(fam).core_channel;
        if (family_count[fam] >= params.family_quota || channel_count[ch] >= params.channel_quota) continue;
        family_count[fam]++;
        channel_count[ch]++;
        kept.push_back({sc, sub, fam, ch});
    }
    return kept;
}

std::string MinedEnginesToJson(const std::vector<MinedEngine>& engines) {
    std::string out = "[";
    for (size_t i = 0; i < engines.size(); i++) {
        if (i) out += ",";
        out += "{\"score\":" + std::to_string(engines[i].score) + ",\"members\":[";
        for (size_t j = 0; j < engines[i].members.size(); j++) {
            if (j) out += ",";
            out += "\"" + EscapeJson(engines[i].members[j]) + "\"";
        }
        out += "],\"core\":\"" + EscapeJson(engines[i].core) + "\",\"channel\":\"" +
               EscapeJson(engines[i].channel) + "\"}";
    }
    out += "]";
    return out;
}

}  // namespace bazaararena::meta
