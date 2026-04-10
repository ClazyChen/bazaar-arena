#pragma once

#include <bazaararena/core/Simulator.hpp>

namespace bazaararena::core {

inline void InitializeSimulator(Simulator& sim) {
    sim.time = 0;
    sim.ability_queue.queue_size = 0;
    sim.ability_queue.next_legal_trigger_time.fill(0);

    sim.ability_bitmap.fill(0);
    sim.aura_bitmap.fill(0);
    sim.crit_bitmap = 0;
    sim.crit_checked_bitmap = 0;

    // Build ability bitmap & aura bitmap from current sides/items.
    for (int side = 0; side < Simulator::SideCount; side++) {
        const int itemCount = sim.sides[side].attrs[SideKey::ItemCount];
        for (int itemIndex = 0; itemIndex < itemCount; itemIndex++) {
            const auto& item = sim.sides[side].items[itemIndex];
            if (!item.templ) continue;

            const unsigned int bit = 1u << ((side << 4) | itemIndex);

            // ability triggers
            for (int ai = 0; ai < item.templ->ability_count; ai++) {
                const auto& ab = item.templ->abilities[ai];
                for (int ti = 0; ti < ab.trigger_entry_count; ti++) {
                    const int tr = ab.trigger_entries[ti].trigger;
                    if (tr >= 0 && tr < Trigger::Count) {
                        sim.ability_bitmap[tr] |= bit;
                    }
                }
            }

            // auras
            for (int ui = 0; ui < item.templ->aura_count; ui++) {
                const auto& aura = item.templ->auras[ui];
                const int key = aura.attribute;
                if (key >= 0 && key < ItemKey::Count) {
                    sim.aura_bitmap[key] |= bit;
                }
            }
        }
    }
}

}  // namespace bazaararena::core

