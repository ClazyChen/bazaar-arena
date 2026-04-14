#pragma once

#include <bazaararena/gdf/BattleEvaluator.hpp>
#include <bazaararena/gdf/DeckRep.hpp>

#include <string>
#include <vector>

namespace bazaararena::gdf_pa {

struct DirectedEdge {
    int from = 0;
    int to = 0;
    double weight = 0;
    int games_used = 0;
};

/// 同槽 multiset 下，对称差 s = sum|Delta count|（偶数；s/2 为置换次数）。2 <= s <= symmetric_diff_max 则连边（默认 max=2 等价于原 max_replacements=1）。
/// 定向为 BoN 胜者 -> 败者；先 100 局计分，若接近 0.5 再打到累计 1000 局。
[[nodiscard]] bool RunDiffOneBattles(const std::vector<bazaararena::gdf::DeckRep>& nodes, bazaararena::gdf::BattleEvaluator& eval,
    int symmetric_diff_max, std::vector<DirectedEdge>& edges_out, std::string& err);

[[nodiscard]] bool WriteEdgesCsv(const std::string& path, const std::vector<DirectedEdge>& edges, const std::vector<std::string>& node_sigs,
    std::string& err);

enum class PeelPhase {
    Sink,           ///< 有入度且无出度，自叶向根删
    SourceQuality,  ///< 无入度且无出度（存活子图中的孤立点），记入优质来源序后删除
    CycleBreak      ///< 余下为环时删权重和最小的点（出边权之和；并列取编号小）
};

[[nodiscard]] inline const char* PeelPhaseLabel(PeelPhase p) {
    switch (p) {
        case PeelPhase::Sink: return "sink";
        case PeelPhase::SourceQuality: return "source_quality";
        case PeelPhase::CycleBreak: return "cycle_break";
    }
    return "sink";
}

struct PeelStep {
    PeelPhase phase = PeelPhase::Sink;
    int node_index = -1;
};

/// 剥点：1) 反复删「有入度无出度」直至无；2) 反复删「无入度且无出度」并记入优质来源序；3) 若仍有顶点则在剩余点中选「出边权之和」最小者删以破环，再回到 1。
[[nodiscard]] std::vector<PeelStep> PeelGraphStructured(int n, const std::vector<DirectedEdge>& edges);

}  // namespace bazaararena::gdf_pa
