#pragma once

#include <string>
#include <unordered_map>
#include <vector>

namespace bazaararena::gdf {

/// 从 data/items/*.yaml 读取每条目的 hero（文件级）与物品 Name，构建 key → hero 名（如 Vanessa）。
/// 失败时 error 非空。
bool LoadItemHeroByKeyFromDataDir(const std::string& data_items_dir, std::unordered_map<std::string, std::string>& out_key_to_hero,
    std::string& error);

}  // namespace bazaararena::gdf
