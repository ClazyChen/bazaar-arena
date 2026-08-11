#include <bazaararena/core/BattleContext.hpp>
#include <bazaararena/core/ItemKey.hpp>
#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/core/Simulator.hpp>
#include <bazaararena/formula/Percent.hpp>
#include <bit>
#include <array>
#include <cstdint>

namespace bazaararena::core {

namespace {

thread_local std::array<uint32_t, 32> g_get_item_int_stack{};
thread_local int g_get_item_int_depth = 0;

uint32_t PackItemAttrToken(const ItemState* item, int key) {
    const int si = item->attrs[ItemKey::SideIndex];
    const int ii = item->attrs[ItemKey::ItemIndex];
    return (static_cast<uint32_t>(key) << 16)
         | (static_cast<uint32_t>(si) << 8)
         | static_cast<uint32_t>(ii);
}

bool IsItemAttrOnStack(uint32_t token) {
    for (int d = 0; d < g_get_item_int_depth; ++d) {
        if (g_get_item_int_stack[d] == token) return true;
    }
    return false;
}

struct GetItemIntDepthGuard {
    explicit GetItemIntDepthGuard(uint32_t token) {
        g_get_item_int_stack[g_get_item_int_depth++] = token;
    }
    ~GetItemIntDepthGuard() { --g_get_item_int_depth; }
};

int FinalizeItemIntValue(const ItemState* item, int key, int value, const BattleContext& ctx) {
    if (item->attrs[ItemKey::InFlight] == 1 &&
        (key == ItemKey::PercentFreezeReduction || key == ItemKey::PercentSlowReduction)) {
        value += 50;
    }
    value = std::max(value, 0);
    if (key == ItemKey::Cooldown) {
        int cooldown_reduction = ctx.GetItemInt(item, ItemKey::CooldownReduction);
        int cooldown_reduction_percent = formula::PercentFloor(value, ctx.GetItemInt(item, ItemKey::CooldownReductionPercent));
        value -= cooldown_reduction + cooldown_reduction_percent;
        value = std::max(value, 1_s);
    }
    return value;
}

} // namespace

// 读取物品的某个属性（不受光环的影响）
int BattleContext::GetItemIntRaw(const ItemState* item, int key) const {
    if (item == nullptr) return 0;
    return item->attrs[key];
}

// 读取物品的某个属性（会受到光环的影响）
int BattleContext::GetItemInt(const ItemState* item, int key) const {
    if (item == nullptr) return 0;
    int base_value = item->attrs[key];
    const uint32_t token = PackItemAttrToken(item, key);
    if (IsItemAttrOnStack(token) || g_get_item_int_depth >= static_cast<int>(g_get_item_int_stack.size())) {
        return FinalizeItemIntValue(item, key, base_value, *this);
    }
    GetItemIntDepthGuard depth_guard(token);

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
    int tags_or_sum = 0; // Tags 为位图，光环贡献须按位或合并（坩埚 SideItemTypes 等），勿用算术加
    while (aura_bitmap != 0) {
        int index = static_cast<int>(std::countr_zero(aura_bitmap));
        aura_bitmap &= ~(1 << index);
        auto side_index = index >> 4;
        auto item_index = index & 0x0F;
        if (side_index < 0 || side_index >= Simulator::SideCount) continue;
        if (item_index < 0 || item_index >= simulator->sides[side_index].attrs[SideKey::ItemCount]) continue;
        auto& aura_caster = simulator->sides[side_index].items[item_index];
        if (aura_caster.templ == nullptr) continue;
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
            } else if (key == ItemKey::Tags) {
                tags_or_sum |= aura_value;
            } else {
                additive_sum += aura_value;
            }
        }
    }
    if (key == ItemKey::Tags) {
        base_value |= tags_or_sum;
    } else {
        base_value += additive_sum;
        base_value += formula::PercentFloor(base_value, percent_sum);
    }
    return FinalizeItemIntValue(item, key, base_value, *this);
}

// 读取能力/光环释放者所在阵营的某个属性
int BattleContext::GetSideInt(int key) const {
    const int si = caster->attrs[ItemKey::SideIndex];
    if (key == SideKey::Resistance) {
        const auto& side = simulator->sides[si];
        if (side.attrs[SideKey::ItemCount] <= 0) return 0;
        return GetItemInt(&side.items[0], ItemKey::Resistance);
    }
    return simulator->sides[si].attrs[key];
}

// 读取能力/光环释放者所在阵营的对手阵营的某个属性
int BattleContext::GetOppInt(int key) const {
    const int si = 1 - caster->attrs[ItemKey::SideIndex];
    if (key == SideKey::Resistance) {
        const auto& side = simulator->sides[si];
        if (side.attrs[SideKey::ItemCount] <= 0) return 0;
        return GetItemInt(&side.items[0], ItemKey::Resistance);
    }
    return simulator->sides[si].attrs[key];
}

// 获取能力/光环释放者所在阵营的对手阵营的指定字段的最大值
int BattleContext::GetOppMaxInt(int key) const {
    const int si = 1 - caster->attrs[ItemKey::SideIndex];
    const auto& side = simulator->sides[si];
    if (side.attrs[SideKey::ItemCount] <= 0) return 0;
    int max_value = 0;
    for (int i = 0; i < side.attrs[SideKey::ItemCount]; i++) {
        auto& item = side.items[i];
        if (item.attrs[ItemKey::Destroyed] == 1) continue;
        int value = GetItemInt(&item, key);
        if (value > max_value) max_value = value;
    }
    return max_value;
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

int BattleContext::SumItemsInt(int key, Formula condition) const {
    BattleContext ctx = *this;
    int sum = 0;
    for (int sj = 0; sj < Simulator::SideCount; sj++) {
        const int n = simulator->sides[sj].attrs[SideKey::ItemCount];
        for (int i = 0; i < n; i++) {
            auto& item = simulator->sides[sj].items[i];
            if (item.attrs[ItemKey::Destroyed] == 1) continue;
            ctx.item = &item;
            if (condition(ctx) != 0) {
                sum += GetItemInt(&item, key);
            }
        }
    }
    return sum;
}

// 己方所有物品的类型
int BattleContext::GetSideItemTypes() const {
    int types = 0;
    for (int i = 0; i < simulator->sides[caster->attrs[ItemKey::SideIndex]].attrs[SideKey::ItemCount]; i++) {
        auto& item = simulator->sides[caster->attrs[ItemKey::SideIndex]].items[i];
        types |= item.attrs[ItemKey::Tags];
    }
    return types;
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

int BattleContext::IsBurnTick() const {
    return simulator->time % Simulator::BurnTickInterval == 0;
}

int BattleContext::IsPoisonTick() const {
    return simulator->time % Simulator::PoisonTickInterval == 0;
}

int BattleContext::IsSandstormTick() const {
    return simulator->time >= simulator->sandstorm.next_tick;
}

int BattleContext::GetTime() const {
    return simulator->time;
}

} // namespace bazaararena::core
