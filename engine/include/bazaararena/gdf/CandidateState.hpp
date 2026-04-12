#pragma once

#include <bazaararena/gdf/DeckRep.hpp>

#include <string>
#include <unordered_set>

namespace bazaararena::gdf {

struct CandidateState {
    std::string combo_key;
    DeckRep representative;
    int size_sum = 0;
    double swiss_score = 0;
    double round_robin_score = 0;
    /// 成对对照锚点边际（λ=0 时可不计算，保持 0）。
    double anchor_margin = 0;
    std::unordered_set<std::string> played_opponents;
};

}  // namespace bazaararena::gdf
