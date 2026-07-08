#pragma once

#include <bazaararena/core/BattleContext.hpp>
#include <bazaararena/core/ItemKey.hpp>
#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/formula/Percent.hpp>

namespace bazaararena::core {

/// 光环 `attribute` 为 Base* 键时，在 `InitializeSimulator` 写入的阵营 `SideKey`；无映射返回 -1。
inline int SideKeyForBaseAttributeAura(int aura_attribute) {
    switch (aura_attribute) {
        case ItemKey::BaseRegen:
            return SideKey::Regen;
        default:
            return -1;
    }
}

/// 将各物品上 Base* 光环的数值累加到对应阵营属性（不触发 Regen 等能力链）。
inline void ApplyBaseAttributeAuras(Simulator& sim) {
    BattleContext ctx = {&sim, nullptr, nullptr, nullptr, nullptr};
    for (int side = 0; side < Simulator::SideCount; side++) {
        const int item_count = sim.sides[side].attrs[SideKey::ItemCount];
        for (int item_index = 0; item_index < item_count; item_index++) {
            auto& item = sim.sides[side].items[item_index];
            if (!item.templ || item.attrs[ItemKey::Destroyed] == 1) continue;
            ctx.caster = &item;
            ctx.item = &item;
            ctx.source = &item;
            ctx.target = &item;
            for (int ui = 0; ui < item.templ->aura_count; ui++) {
                const auto& aura = item.templ->auras[ui];
                const int side_key = SideKeyForBaseAttributeAura(aura.attribute);
                if (side_key < 0) continue;
                if (aura.condition(ctx) == 0) continue;
                const int v = aura.value(ctx);
                auto& side_attrs = sim.sides[side].attrs;
                if (aura.percent) {
                    side_attrs[side_key] += formula::PercentFloor(side_attrs[side_key], v);
                } else {
                    side_attrs[side_key] += v;
                }
            }
        }
    }
}

}  // namespace bazaararena::core
