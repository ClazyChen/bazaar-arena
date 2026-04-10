#include <bazaararena/core/BattleContext.hpp>
#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/formula/Percent.hpp>
#include <bit>

namespace bazaararena::core {

// 读取物品的某个属性（会受到光环的影响）
int BattleContext::GetItemInt(const ItemState* item, int key) const {
    int base_value = item->attrs[key];
    auto aura_bitmap = simulator->aura_bitmap[key];
    BattleContext ctx = *this;
    while (aura_bitmap != 0) {
        int index = static_cast<int>(std::countr_zero(aura_bitmap));
        aura_bitmap &= ~(1 << index);
        auto side_index = index >> 4;
        auto item_index = index & 0x0F;
        auto& aura_caster = simulator->sides[side_index].items[item_index];
        if (aura_caster.attrs[ItemKey::Destroyed] == 1) continue;
        ctx.caster = &aura_caster;
        for (int i = 0; i < aura_caster.templ->aura_count; i++) {
            auto& aura = aura_caster.templ->auras[i];
            if (aura.condition(ctx) == 0) continue;
            auto aura_value = aura.value(ctx);
            if (aura.percent) {
                base_value += formula::PercentFloor(base_value, aura_value);
            } else {
                base_value += aura_value;
            }
        }
    }
    // 冷却时间是特判的，需要考虑冷却时间减少和冷却时间减少百分比，以及冷却时间最小为 1 秒
    if (key == ItemKey::Cooldown) {
        int cooldown_reduction = GetItemInt(item, ItemKey::CooldownReduction);
        int cooldown_reduction_percent = formula::PercentFloor(base_value, GetItemInt(item, ItemKey::CooldownReductionPercent));
        base_value -= cooldown_reduction + cooldown_reduction_percent;
        base_value = std::max(base_value, 1_s);
    }
    return base_value;
}

// 读取能力/光环释放者所在阵营的某个属性
int BattleContext::GetSideInt(int key) const {
    return simulator->sides[caster->attrs[ItemKey::SideIndex]].attrs[key];
}

// 读取能力/光环释放者所在阵营的对手阵营的某个属性
int BattleContext::GetOppInt(int key) const {
    return simulator->sides[1 - caster->attrs[ItemKey::SideIndex]].attrs[key];
}

} // namespace bazaararena::core