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
    // 按 BattleContext.hpp 的约定：读取某件物品属性时，item/source/target 都应指向该“被读属性主体”。
    // 否则在某个物品 A 的上下文中读取物品 B 的属性时（例如光环 value 里读 Caster<Value>），
    // B 的光环条件里若使用 SameAsCaster / SameAsSource 等，会错误地拿 A 当作 Item 进行判断，导致光环失效。
    ctx.item = item;
    ctx.source = item;
    ctx.target = item;
    int percent_sum = 0;
    int additive_sum = 0;
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
                percent_sum += aura_value;
            } else {
                additive_sum += aura_value;
            }
        }
    }
    // 飞行的物品：冻结/减速减免百分比 +50（例如 1s → 0.5s）。
    // 注意这两个 ItemKey 本身就是“百分比值”，应按加法累加，而不是按 base_value 的百分比叠加；
    // 否则当基础值为 0 时（常见）会被 PercentFloor 吃掉，导致减免不生效。
    if (item->attrs[ItemKey::InFlight] == 1 &&
        (key == ItemKey::PercentFreezeReduction || key == ItemKey::PercentSlowReduction)) {
        additive_sum += 50;
    }
    base_value += additive_sum;
    base_value += formula::PercentFloor(base_value, percent_sum);
    base_value = std::max(base_value, 0);
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

// 计算满足某个条件的物品数量（双方所有物品；条件内可用 SameSide / DifferentSide 等）
int BattleContext::CountItems(Formula condition) const {
    BattleContext ctx = *this;
    int count = 0;
    for (int sj = 0; sj < Simulator::SideCount; sj++) {
        const int n = simulator->sides[sj].attrs[SideKey::ItemCount];
        for (int i = 0; i < n; i++) {
            auto& item = simulator->sides[sj].items[i];
            if (item.attrs[ItemKey::Destroyed] == 1) continue;
            ctx.item = &item;
            if (condition(ctx) != 0) count++;
        }
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

// 满足某个条件的最右侧的物品（扫描顺序与 IsLeftmostWith 相同，取最后一次命中）
int BattleContext::IsRightmostWith(Formula condition) const {
    BattleContext ctx = *this;
    const ItemState* last = nullptr;
    for (int sj = 0; sj < Simulator::SideCount; sj++) {
        const int n = simulator->sides[sj].attrs[SideKey::ItemCount];
        for (int ii = 0; ii < n; ii++) {
            auto& it = simulator->sides[sj].items[ii];
            if (it.attrs[ItemKey::Destroyed] == 1) continue;
            ctx.item = &it;
            if (condition(ctx) != 0) {
                last = &it;
            }
        }
    }
    if (last == nullptr) return 0;
    return this->item == last ? 1 : 0;
}

} // namespace bazaararena::core