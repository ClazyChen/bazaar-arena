#pragma once

#include <bazaararena/gdf_pa/GdfPaFullTopk.hpp>

#include <string>
#include <utility>
#include <vector>

namespace bazaararena::gdf_pa {

struct GeneralityRow {
    int rank = 0;
    std::string item;
    double gen = 0;
    /// 除自身外每个锚点的 α_{item,anchor}（无出现为 0），按 α 降序、锚点名升序。
    std::vector<std::pair<std::string, double>> anchor_alphas;
};

struct SpecialtyRow {
    int rank = 0;
    std::string anchor;
    double spec = 0;
};

/// `top_k` 用于 H_K 与截断；RR=0 行跳过（专用度）。
[[nodiscard]] bool ComputeGeneralityTable(const std::vector<FullTopkAnchorBlock>& blocks, int top_k,
    const std::vector<std::string>& pool_items, std::vector<GeneralityRow>& out, std::string& err);

[[nodiscard]] bool ComputeSpecialtyTable(const std::vector<FullTopkAnchorBlock>& blocks,
    std::vector<SpecialtyRow>& out, std::vector<std::string>& rr_skip_log, std::string& err);

/// 按指标降序赋 `rank`（同分并列名次，下一名跳号）。
void SortAndRankGeneralityForOutput(std::vector<GeneralityRow>& rows);
void SortAndRankSpecialtyForOutput(std::vector<SpecialtyRow>& rows);

[[nodiscard]] bool WriteGeneralityCsv(const std::string& path, const std::vector<GeneralityRow>& rows, std::string& err);

/// 长表：每个 (item, anchor) 一行，α_{item,anchor}。
[[nodiscard]] bool WriteGeneralityPerAnchorCsv(const std::string& path, const std::vector<GeneralityRow>& rows, std::string& err);

[[nodiscard]] bool WriteSpecialtyCsv(const std::string& path, const std::vector<SpecialtyRow>& rows, std::string& err);

}  // namespace bazaararena::gdf_pa
