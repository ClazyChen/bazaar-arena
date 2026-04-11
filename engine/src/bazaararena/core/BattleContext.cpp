#include <bazaararena/core/BattleContext.hpp>
#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/formula/Percent.hpp>
#include <bit>

namespace bazaararena::core {

// 读取物品的某个属性（不受光环的影响）
int BattleContext::GetItemIntRaw(const ItemState* item, int key) const {
    if (item == nullptr) return 0;
    return item->attrs[key];
}

// 读取物品的某个属性（会受到光环的影响）
int BattleContext::GetItemInt(const ItemState* item, int key) const {
    if (item == nullptr) return 0;
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
            // 只累计「目标属性为当前读取的 key」的光环；否则多光环物品（如同时改 Heal 与 Value）
            // 会在读 Value 时仍执行 Heal 光环公式，而 Heal 公式含 Caster<Value>，导致无限递归与栈溢出。
            if (aura.attribute != key) continue;
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

// 计算满足某个条件的物品数量
int BattleContext::CountItems(Formula condition) const {
    BattleContext ctx = *this;
    int count = 0;
    const int side_ix = caster->attrs[ItemKey::SideIndex];
    const int n = simulator->sides[side_ix].attrs[SideKey::ItemCount];
    for (int i = 0; i < n; i++) {
        auto& item = simulator->sides[side_ix].items[i];
        if (item.attrs[ItemKey::Destroyed] == 1) continue;
        ctx.item = &item;
        if (condition(ctx) != 0) count++;
    }
    return count;
}

// 满足某个条件的最左侧的物品（扫描顺序：阵营 0→1，每阵营内物品槽 0→ItemCount-1）
int BattleContext::IsLeftmostWith(Formula condition) const {
    BattleContext ctx = *this;
    const ItemState* first = nullptr;
    for (int sj = 0; sj < Simulator::SideCount && first == nullptr; sj++) {
        const int n = simulator->sides[sj].attrs[SideKey::ItemCount];
        for (int ii = 0; ii < n; ii++) {
            auto& it = simulator->sides[sj].items[ii];
            if (it.attrs[ItemKey::Destroyed] == 1) continue;
            ctx.item = &it;
            if (condition(ctx) != 0) {
                first = &it;
                break;
            }
        }
    }
    if (first == nullptr) return 0;
    return this->item == first ? 1 : 0;
}

} // namespace bazaararena::core