#pragma once

#include <bazaararena/core/SideState.hpp>
#include <bazaararena/gdf/DeckRep.hpp>

#include <string>
#include <unordered_map>

namespace bazaararena::gdf {

class ItemPool;

/// 单次 GDF 搜索内只读：池内每个展示名（含烙刀 Q1/Q2）预计算 `ItemState`，供 `BattleEvaluator::ToSide` 无锁读取并组装 `SideState`。
class GdfItemPrototypeCache {
public:
    explicit GdfItemPrototypeCache(const ItemPool& pool, int player_level);

    /// 从预计算原型拷贝槽位属性，并初始化侧属性（与 `BuildSideState` 默认侧一致）。
    [[nodiscard]] bazaararena::core::SideState BuildSide(const DeckRep& rep, int player_level, int side_id = 0) const;

    /// 调试用；缺失时抛异常。
    [[nodiscard]] const bazaararena::core::ItemState& At(std::string_view display_name) const;

private:
    std::unordered_map<std::string, bazaararena::core::ItemState> protos_;
};

}  // namespace bazaararena::gdf
