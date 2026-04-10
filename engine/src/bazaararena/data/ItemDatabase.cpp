#include "bazaararena/data/ItemDatabase.hpp"

#include <algorithm>
#include <vector>

#include "bazaararena/data/GeneratedItems.hpp"
#include "bazaararena/core/DerivedTag.hpp"
#include "bazaararena/core/ItemKey.hpp"
#include "bazaararena/core/ItemTemplate.hpp"

namespace bazaararena::data {
namespace core = bazaararena::core;

namespace {

int ComputeDerivedTags(const core::ItemTemplate& templ) {
    int tags = 0;

    // 用 Bronze tier 做静态推导（此阶段派生标签不考虑随 tier 变化）
    const auto& attrs = templ.attributes[core::ItemTier::Bronze];
    if (attrs[core::ItemKey::Cooldown] > 0) tags |= core::DerivedTag::Cooldown;

    for (int i = 0; i < templ.ability_count; i++) {
        switch (templ.abilities[i].type) {
            case core::AbilityType::Damage:
                tags |= core::DerivedTag::Damage;
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

