#include "bazaararena/data/ItemDatabase.hpp"

#include <algorithm>
#include <vector>

#include "bazaararena/data/GeneratedItems.hpp"
#include "bazaararena/core/AbilityType.hpp"
#include "bazaararena/core/DerivedTag.hpp"
#include "bazaararena/core/ItemKey.hpp"
#include "bazaararena/core/ItemTemplate.hpp"
#include "bazaararena/core/Trigger.hpp"

namespace bazaararena::data {
namespace core = bazaararena::core;

namespace {

bool AbilityTypeContributesCritEligible(int ability_type) {
    switch (ability_type) {
        case core::AbilityType::Damage:
        case core::AbilityType::Shield:
        case core::AbilityType::Heal:
        case core::AbilityType::Burn:
        case core::AbilityType::Poison:
        case core::AbilityType::Regen:
        case core::AbilityType::PoisonSelf:
            return true;
        default:
            return false;
    }
}

bool AbilityHasCastTrigger(const core::AbilityDefinition& ab) {
    for (int j = 0; j < ab.trigger_entry_count; j++) {
        if (ab.trigger_entries[j].trigger == core::Trigger::Cast) return true;
    }
    return false;
}

int ComputeDerivedTags(const core::ItemTemplate& templ) {
    int tags = 0;

    // 静态推导：DerivedTags 不考虑随 tier 变化，但需要覆盖 MinTier!=Bronze 的物品，
    // 因此对所有 tier 做一次 OR（例如：Silver 起步的物品在 Bronze tier 可能没有 Cooldown）。
    for (const auto& attrs : templ.attributes) {
        if (attrs[core::ItemKey::Cooldown] > 0) tags |= core::DerivedTag::Cooldown;
        if (attrs[core::ItemKey::AmmoCap] > 0) tags |= core::DerivedTag::Ammo;
    }

    for (int i = 0; i < templ.ability_count; i++) {
        const auto& ab = templ.abilities[i];
        switch (ab.type) {
            case core::AbilityType::Damage:
                tags |= core::DerivedTag::Damage;
                break;
            case core::AbilityType::Charge:
                tags |= core::DerivedTag::Charge;
                break;
            case core::AbilityType::Freeze:
                tags |= core::DerivedTag::Freeze;
                break;
            case core::AbilityType::Slow:
                tags |= core::DerivedTag::Slow;
                break;
            case core::AbilityType::Haste:
                tags |= core::DerivedTag::Haste;
                break;
            case core::AbilityType::Reload:
                tags |= core::DerivedTag::Reload;
                break;
            case core::AbilityType::Repair:
                tags |= core::DerivedTag::Repair;
                break;
            case core::AbilityType::Destroy:
                tags |= core::DerivedTag::Destroy;
                break;
            case core::AbilityType::Burn:
                tags |= core::DerivedTag::Burn;
                break;
            case core::AbilityType::Poison:
                tags |= core::DerivedTag::Poison;
                break;
            default:
                break;
        }
        if (AbilityHasCastTrigger(ab) && AbilityTypeContributesCritEligible(ab.type)) {
            tags |= core::DerivedTag::Crit;
        }
    }
    return tags;
}

struct GeneratedView {
    std::string_view key;
    core::ItemTemplate* templ;
};

// 在首次访问时把生成数据“就地补齐” derived tags
std::span<const ItemRecord> BuildCache() {
    static bool inited = false;
    static std::vector<ItemRecord> records_storage;

    if (inited) {
        return std::span<const ItemRecord>(records_storage.data(), records_storage.size());
    }
    inited = true;

    auto gen = bazaararena::data::generated::GetGeneratedItems();
    records_storage.clear();

    // 将 id 设为按 key 排序后的 1..N
    std::vector<GeneratedView> views;
    views.reserve(gen.size());
    for (const auto& gi : gen) {
        views.push_back(GeneratedView{gi.key, const_cast<core::ItemTemplate*>(&gi.templ)});
    }
    std::sort(views.begin(), views.end(), [](const auto& a, const auto& b) { return a.key < b.key; });

    records_storage.reserve(views.size());
    for (size_t i = 0; i < views.size(); i++) {
        auto* templ = views[i].templ;
        const int derived = ComputeDerivedTags(*templ);
        for (auto& tier : templ->attributes) {
            tier[core::ItemKey::DerivedTags] = derived;
        }

        records_storage.push_back(ItemRecord{
            .id = static_cast<int>(i + 1),
            .key = views[i].key,
            .templ = templ,
        });
    }

    return std::span<const ItemRecord>(records_storage.data(), records_storage.size());
}

}  // namespace

std::span<const ItemRecord> GetAllItems() { return BuildCache(); }

const core::ItemTemplate* GetItemById(int id) {
    auto all = BuildCache();
    if (id <= 0) return nullptr;
    const auto it = std::lower_bound(
        all.begin(), all.end(), id, [](const ItemRecord& r, int v) { return r.id < v; });
    if (it == all.end() || it->id != id) return nullptr;
    return it->templ;
}

const core::ItemTemplate* GetItemByKey(std::string_view key) {
    auto all = BuildCache();
    for (const auto& r : all) {
        if (r.key == key) return r.templ;
    }
    return nullptr;
}

}  // namespace bazaararena::data

