#pragma once

#include <string>
#include <utility>
#include <vector>

namespace bazaararena::gdf_pa {

/// 与 `scripts/gdf_enumerate_anchor_top1.py` 写入的 full_topk 一致：每锚点仅保留满槽最后一档。
struct FullTopkRankRow {
    int rank = 0;
    double rr = 0;
    double anchor_m = 0;
    double swiss = 0;
    std::string deck_signature;
};

struct FullTopkAnchorBlock {
    std::string anchor_item;
    std::string size_line;
    std::vector<FullTopkRankRow> ranks;
};

/// 读取 UTF-8 文本（文件路径或内存字符串）。忽略以 `#` 开头的说明行。
[[nodiscard]] bool ParseFullTopkFile(const std::string& path_utf8, std::vector<FullTopkAnchorBlock>& out, std::string& err);

[[nodiscard]] bool ParseFullTopkString(std::string_view text, std::vector<FullTopkAnchorBlock>& out, std::string& err);

}  // namespace bazaararena::gdf_pa
