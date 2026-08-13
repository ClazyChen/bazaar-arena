// ReasonMine — 理由图引擎挖掘（C++ 层，bazaararena_meta --mine-engines）。
//
// 输入：scripts/meta_search/reason_graph.py 导出的静态画像 JSON
// （提取规则的唯一事实来源在 Python 侧；本文件只做边重建与闭包挖掘）。
// 算法与 scripts/meta_search/mine_engines.py 的 mine_engines_v2 对齐：
// 核心播种 → 共享邻域闭包扩展（新成员须与 ≥2 个现有成员有边）→
// 评分 = 闭包加权分 + core_bonus_weight × 核心强度分 → Jaccard 去重 + 两级配额。

#pragma once

#include <bazaararena/io/JsonLite.hpp>

#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace bazaararena::meta {

struct ReasonProfile {
    int size = 1;
    std::unordered_set<std::string> tags;
    std::unordered_set<std::string> produces;
    std::unordered_set<std::string> consumes_trigger;
    std::unordered_set<std::string> consumes_resource;
    std::unordered_set<std::string> selects_tags;
    std::unordered_set<std::string> feeds;
    double core_score = 0;
    std::string core_channel;
};

struct MineParams {
    int max_members = 4;
    int max_slots = 8;
    double min_pair = 1.0;
    double per_member = 1.5;
    double core_bonus_weight = 8.0;
    int family_quota = 3;
    int channel_quota = 8;
};

struct MinedEngine {
    double score = 0;
    std::vector<std::string> members;
    std::string core;
    std::string channel;
};

bool LoadReasonProfiles(const std::string& path,
                        std::unordered_map<std::string, ReasonProfile>& out,
                        std::string& error);

std::vector<MinedEngine> MineEngines(const std::unordered_map<std::string, ReasonProfile>& profiles,
                                     const MineParams& params);

/// 输出 JSON：[{"score":..,"members":[..],"core":"..","channel":".."}]
std::string MinedEnginesToJson(const std::vector<MinedEngine>& engines);

}  // namespace bazaararena::meta
