#include "bazaararena/io/SideStateBuilder.hpp"

#include <bazaararena/core/HpTable.hpp>
#include <bazaararena/core/ItemKey.hpp>
#include <bazaararena/core/ItemTier.hpp>
#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/data/ItemDatabase.hpp>

namespace bazaararena::io {
namespace core = bazaararena::core;

namespace {

static std::optional<int> ParseTier(std::string_view s) {
    if (s == "bronze") return core::ItemTier::Bronze;
    if (s == "silver") return core::ItemTier::Silver;
    if (s == "gold") return core::ItemTier::Gold;
    if (s == "diamond") return core::ItemTier::Diamond;
    return std::nullopt;
}

static bool ApplySideOverride(core::SideState& out, const SideSpec& spec, std::string& err) {
    auto& a = out.attrs;
    if (spec.id) a[core::SideKey::Id] = *spec.id;
    if (spec.maxHp) a[core::SideKey::MaxHp] = *spec.maxHp;
    if (spec.hp) a[core::SideKey::Hp] = *spec.hp;
    if (spec.shield) a[core::SideKey::Shield] = *spec.shield;
    if (spec.burn) a[core::SideKey::Burn] = *spec.burn;
    if (spec.poison) a[core::SideKey::Poison] = *spec.poison;
    if (spec.regen) a[core::SideKey::Regen] = *spec.regen;
    if (spec.resistance) a[core::SideKey::Resistance] = *spec.resistance;
    if (spec.gold) a[core::SideKey::Gold] = *spec.gold;
    if (spec.income) a[core::SideKey::Income] = *spec.income;

    const int maxHp = a[core::SideKey::MaxHp];
    const int hp = a[core::SideKey::Hp];
    if (!(maxHp >= hp && hp > 0)) {
        err = "side attrs constraint violated: MaxHp >= Hp > 0";
        return false;
    }
    if (a[core::SideKey::Income] < 7) {
        err = "side attrs constraint violated: Income >= 7";
        return false;
    }
    auto nonneg = [](int v) { return v >= 0; };
    if (!nonneg(a[core::SideKey::Shield]) || !nonneg(a[core::SideKey::Burn]) || !nonneg(a[core::SideKey::Poison]) ||
        !nonneg(a[core::SideKey::Regen]) || !nonneg(a[core::SideKey::Resistance]) || !nonneg(a[core::SideKey::Gold])) {
        err = "side attrs constraint violated: other attrs must be >= 0";
        return false;
    }
    return true;
}

static void ApplyItemCustomOverrides(core::ItemState& item, const ItemSpec& spec) {
    if (spec.custom_0) item.attrs[core::ItemKey::Custom_0] = *spec.custom_0;
    if (spec.custom_1) item.attrs[core::ItemKey::Custom_1] = *spec.custom_1;
    if (spec.custom_2) item.attrs[core::ItemKey::Custom_2] = *spec.custom_2;
    if (spec.custom_3) item.attrs[core::ItemKey::Custom_3] = *spec.custom_3;
}

}  // namespace

BuildSideStateResult BuildSideState(const SideSpec& spec) {
    if (spec.level < 1 || spec.level > core::HpTable::MaxLevel) {
        return {.side = std::nullopt, .error = "side.level out of range"};
    }
    if (spec.items.size() > static_cast<size_t>(core::SideState::MaxItems)) {
        return {.side = std::nullopt, .error = "side.items exceeds SideState::MaxItems"};
    }

    core::SideState out{};

    // Default init from level
    const int hp = core::HpTable::ByLevel[spec.level];
    out.attrs.fill(0);
    out.attrs[core::SideKey::Id] = spec.sideId;
    out.attrs[core::SideKey::MaxHp] = hp;
    out.attrs[core::SideKey::Hp] = hp;
    out.attrs[core::SideKey::Shield] = 0;
    out.attrs[core::SideKey::Burn] = 0;
    out.attrs[core::SideKey::Poison] = 0;
    out.attrs[core::SideKey::Regen] = 0;
    out.attrs[core::SideKey::Gold] = 0;
    out.attrs[core::SideKey::Income] = 7;
    out.attrs[core::SideKey::Resistance] = 0;
    out.attrs[core::SideKey::ItemCount] = static_cast<int>(spec.items.size());

    std::string err;
    if (!ApplySideOverride(out, spec, err)) {
        return {.side = std::nullopt, .error = err};
    }
    // ItemCount is always computed from deck; force it after overrides.
    out.attrs[core::SideKey::ItemCount] = static_cast<int>(spec.items.size());

    // Fill items
    for (size_t i = 0; i < spec.items.size(); i++) {
        const auto& it = spec.items[i];
        const auto tierOpt = ParseTier(it.tier);
        if (!tierOpt) {
            return {.side = std::nullopt, .error = "invalid item tier"};
        }
        const core::ItemTemplate* templConst = bazaararena::data::GetItemByKey(it.key);
        if (!templConst) {
            return {.side = std::nullopt, .error = "unknown item key: " + it.key};
        }
        auto& item = out.items[i];
        item.templ = const_cast<core::ItemTemplate*>(templConst);
        item.attrs = templConst->attributes[*tierOpt];

        item.attrs[core::ItemKey::SideIndex] = spec.sideId;
        item.attrs[core::ItemKey::ItemIndex] = static_cast<int>(i);
        item.attrs[core::ItemKey::Tier] = *tierOpt;

        ApplyItemCustomOverrides(item, it);
    }

    return {.side = out, .error = ""};
}

}  // namespace bazaararena::io

