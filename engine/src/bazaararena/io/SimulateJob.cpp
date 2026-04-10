#include "bazaararena/io/SimulateJob.hpp"

#include "bazaararena/io/JsonLite.hpp"

#include <bazaararena/core/HpTable.hpp>
#include <bazaararena/core/SideState.hpp>

#include <sstream>

namespace bazaararena::io {
namespace {

static bool IsAllowedDebugLevel(std::string_view s) {
    return s == "none" || s == "summary" || s == "detailed";
}

static bool IsAllowedTier(std::string_view s) {
    return s == "bronze" || s == "silver" || s == "gold" || s == "diamond";
}

static std::optional<int> ReadOptionalInt(const JsonValue& v) {
    return GetInt(v);
}

static bool ReadAttrsOverrideSide(const JsonValue& obj, SideSpec& side, std::string& err) {
    const auto* o = obj.AsObject();
    if (!o) {
        err = "payload.sides[*].attrsOverride must be an object";
        return false;
    }
    for (const auto& [k, vv] : *o) {
        if (k == "itemCount") {
            err = "payload.sides[*].attrsOverride must not contain itemCount";
            return false;
        }
        auto vi = ReadOptionalInt(vv);
        if (!vi) {
            err = "payload.sides[*].attrsOverride values must be integers";
            return false;
        }
        if (k == "id") side.id = *vi;
        else if (k == "maxHp") side.maxHp = *vi;
        else if (k == "hp") side.hp = *vi;
        else if (k == "shield") side.shield = *vi;
        else if (k == "burn") side.burn = *vi;
        else if (k == "poison") side.poison = *vi;
        else if (k == "regen") side.regen = *vi;
        else if (k == "resistance") side.resistance = *vi;
        else if (k == "gold") side.gold = *vi;
        else if (k == "income") side.income = *vi;
        else {
            err = "payload.sides[*].attrsOverride has unknown field: " + k;
            return false;
        }
    }
    return true;
}

static bool ReadAttrsOverrideItem(const JsonValue& obj, ItemSpec& item, std::string& err) {
    const auto* o = obj.AsObject();
    if (!o) {
        err = "payload.sides[*].items[*].attrsOverride must be an object";
        return false;
    }
    for (const auto& [k, vv] : *o) {
        auto vi = ReadOptionalInt(vv);
        if (!vi) {
            err = "payload.sides[*].items[*].attrsOverride values must be integers";
            return false;
        }
        if (k == "custom_0") item.custom_0 = *vi;
        else if (k == "custom_1") item.custom_1 = *vi;
        else if (k == "custom_2") item.custom_2 = *vi;
        else if (k == "custom_3") item.custom_3 = *vi;
        else {
            err = "payload.sides[*].items[*].attrsOverride only allows custom_0..custom_3";
            return false;
        }
    }
    return true;
}

static bool ValidateSideSpec(const SideSpec& side, std::string& err) {
    if (side.level < 1 || side.level > bazaararena::core::HpTable::MaxLevel) {
        err = "payload.sides[*].level out of range";
        return false;
    }
    if (side.items.size() > static_cast<size_t>(bazaararena::core::SideState::MaxItems)) {
        err = "payload.sides[*].items exceeds SideState::MaxItems";
        return false;
    }

    // Default values from level, then apply overrides for validation.
    int maxHp = bazaararena::core::HpTable::ByLevel[side.level];
    int hp = maxHp;
    int shield = 0, burn = 0, poison = 0, regen = 0, resistance = 0, gold = 0, income = 7;

    if (side.maxHp) maxHp = *side.maxHp;
    if (side.hp) hp = *side.hp;
    if (side.shield) shield = *side.shield;
    if (side.burn) burn = *side.burn;
    if (side.poison) poison = *side.poison;
    if (side.regen) regen = *side.regen;
    if (side.resistance) resistance = *side.resistance;
    if (side.gold) gold = *side.gold;
    if (side.income) income = *side.income;

    if (!(maxHp >= hp && hp > 0)) {
        err = "side attrs constraint violated: MaxHp >= Hp > 0";
        return false;
    }
    if (income < 7) {
        err = "side attrs constraint violated: Income >= 7";
        return false;
    }
    auto nonneg = [](int v) { return v >= 0; };
    if (!nonneg(shield) || !nonneg(burn) || !nonneg(poison) || !nonneg(regen) || !nonneg(resistance) ||
        !nonneg(gold)) {
        err = "side attrs constraint violated: other attrs must be >= 0";
        return false;
    }
    return true;
}

}  // namespace

ParseJobResult ParseSimulateJobJson(std::string_view jsonText) {
    JsonParseError perr;
    auto rootOpt = ParseJson(jsonText, perr);
    if (!rootOpt) {
        std::ostringstream os;
        os << "invalid json: " << perr.message << " at offset " << perr.offset;
        return {.job = std::nullopt, .error = os.str()};
    }
    const JsonValue& root = *rootOpt;
    const auto* robj = root.AsObject();
    if (!robj) return {.job = std::nullopt, .error = "root must be an object"};

    SimulateJob job;

    // schemaVersion
    if (const auto* sv = GetObjectField(root, "schemaVersion")) {
        auto iv = GetInt(*sv);
        if (!iv) return {.job = std::nullopt, .error = "schemaVersion must be integer"};
        job.schemaVersion = *iv;
    }
    // jobId (optional)
    if (const auto* jid = GetObjectField(root, "jobId")) {
        auto s = GetString(*jid);
        if (!s) return {.job = std::nullopt, .error = "jobId must be string"};
        job.jobId.assign(s->data(), s->size());
    }
    // mode
    {
        const auto* m = GetObjectField(root, "mode");
        if (!m) return {.job = std::nullopt, .error = "mode is required"};
        auto s = GetString(*m);
        if (!s) return {.job = std::nullopt, .error = "mode must be string"};
        job.mode.assign(s->data(), s->size());
        if (job.mode != "simulate") return {.job = std::nullopt, .error = "mode must be 'simulate'"};
    }

    const auto* payload = GetObjectField(root, "payload");
    if (!payload || !payload->IsObject()) return {.job = std::nullopt, .error = "payload must be object"};

    // seed
    if (const auto* seed = GetObjectField(*payload, "seed")) {
        auto iv = GetInt(*seed);
        if (!iv) return {.job = std::nullopt, .error = "payload.seed must be integer"};
        job.seed = *iv;
    }
    // allowTie
    if (const auto* at = GetObjectField(*payload, "allowTie")) {
        auto bv = GetBool(*at);
        if (!bv) return {.job = std::nullopt, .error = "payload.allowTie must be boolean"};
        job.allowTie = *bv;
    }
    // debug
    if (const auto* dbg = GetObjectField(*payload, "debug")) {
        if (!dbg->IsObject()) return {.job = std::nullopt, .error = "payload.debug must be object"};
        if (const auto* en = GetObjectField(*dbg, "enabled")) {
            auto bv = GetBool(*en);
            if (!bv) return {.job = std::nullopt, .error = "payload.debug.enabled must be boolean"};
            job.debug.enabled = *bv;
        }
        if (const auto* lv = GetObjectField(*dbg, "level")) {
            auto sv = GetString(*lv);
            if (!sv) return {.job = std::nullopt, .error = "payload.debug.level must be string"};
            if (!IsAllowedDebugLevel(*sv)) return {.job = std::nullopt, .error = "payload.debug.level invalid"};
            job.debug.level.assign(sv->data(), sv->size());
        }
        if (const auto* me = GetObjectField(*dbg, "maxEvents")) {
            auto iv = GetInt(*me);
            if (!iv) return {.job = std::nullopt, .error = "payload.debug.maxEvents must be integer"};
            job.debug.maxEvents = *iv;
        }
    }

    // sides
    const auto* sides = GetObjectField(*payload, "sides");
    if (!sides || !sides->IsArray()) return {.job = std::nullopt, .error = "payload.sides must be array"};
    const auto& sarr = *sides->AsArray();
    if (sarr.size() != 2) return {.job = std::nullopt, .error = "payload.sides length must be 2"};
    job.sides.clear();
    job.sides.reserve(2);

    for (size_t si = 0; si < sarr.size(); si++) {
        const auto& sv = sarr[si];
        const auto* sobj = sv.AsObject();
        if (!sobj) return {.job = std::nullopt, .error = "payload.sides[*] must be object"};

        SideSpec side;
        side.sideId = static_cast<int>(si);

        if (const auto* sid = GetObjectField(sv, "sideId")) {
            auto iv = GetInt(*sid);
            if (!iv) return {.job = std::nullopt, .error = "payload.sides[*].sideId must be integer"};
            side.sideId = *iv;
        }
        const auto* lvl = GetObjectField(sv, "level");
        if (!lvl) return {.job = std::nullopt, .error = "payload.sides[*].level is required"};
        {
            auto iv = GetInt(*lvl);
            if (!iv) return {.job = std::nullopt, .error = "payload.sides[*].level must be integer"};
            side.level = *iv;
        }
        if (const auto* ao = GetObjectField(sv, "attrsOverride")) {
            std::string e;
            if (!ReadAttrsOverrideSide(*ao, side, e)) return {.job = std::nullopt, .error = e};
        }

        const auto* items = GetObjectField(sv, "items");
        if (!items || !items->IsArray()) return {.job = std::nullopt, .error = "payload.sides[*].items must be array"};
        const auto& iarr = *items->AsArray();
        side.items.clear();
        side.items.reserve(iarr.size());
        for (size_t ii = 0; ii < iarr.size(); ii++) {
            const auto& iv = iarr[ii];
            const auto* iobj = iv.AsObject();
            if (!iobj) return {.job = std::nullopt, .error = "payload.sides[*].items[*] must be object"};
            ItemSpec item;

            const auto* key = GetObjectField(iv, "key");
            if (!key) return {.job = std::nullopt, .error = "payload.sides[*].items[*].key is required"};
            auto ks = GetString(*key);
            if (!ks) return {.job = std::nullopt, .error = "payload.sides[*].items[*].key must be string"};
            item.key.assign(ks->data(), ks->size());

            item.tier = "bronze";
            if (const auto* tier = GetObjectField(iv, "tier")) {
                auto ts = GetString(*tier);
                if (!ts) return {.job = std::nullopt, .error = "payload.sides[*].items[*].tier must be string"};
                if (!IsAllowedTier(*ts)) return {.job = std::nullopt, .error = "payload.sides[*].items[*].tier invalid"};
                item.tier.assign(ts->data(), ts->size());
            }

            if (const auto* ao = GetObjectField(iv, "attrsOverride")) {
                std::string e;
                if (!ReadAttrsOverrideItem(*ao, item, e)) return {.job = std::nullopt, .error = e};
            }

            side.items.push_back(std::move(item));
        }

        std::string verr;
        if (!ValidateSideSpec(side, verr)) return {.job = std::nullopt, .error = verr};
        job.sides.push_back(std::move(side));
    }

    return {.job = std::move(job), .error = ""};
}

}  // namespace bazaararena::io

