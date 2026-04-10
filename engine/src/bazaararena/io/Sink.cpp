#include "bazaararena/io/Sink.hpp"

#include <bazaararena/core/ItemKey.hpp>
#include <bazaararena/core/SideKey.hpp>
#include <bazaararena/core/Simulator.hpp>

#include <iomanip>
#include <sstream>

namespace bazaararena::io {
namespace core = bazaararena::core;

namespace {

static std::string FormatTime(int time_ms) {
    std::ostringstream os;
    os << '[' << std::fixed << std::setprecision(2) << (static_cast<double>(time_ms) / 1000.0) << "s] ";
    return os.str();
}

static int GetSideIndex(const core::ItemState& item) {
    return item.attrs[core::ItemKey::SideIndex];
}

static int GetItemIndex(const core::ItemState& item) {
    return item.attrs[core::ItemKey::ItemIndex];
}

static std::string_view GetItemName(const core::ItemState& item) {
    if (!item.templ) return std::string_view{};
    return std::string_view(item.templ->name);
}

static std::string FormatTargetItemNames(const core::Simulator& simulator, int target_count) {
    std::ostringstream os;
    os << '[';
    bool first = true;
    for (int i = 0; i < target_count; i++) {
        const core::ItemState* it = simulator.targets[static_cast<size_t>(i)];
        if (!it) continue;
        const std::string_view name = GetItemName(*it);
        if (!first) os << ", ";
        first = false;
        os << (name.empty() ? std::string_view("?") : name);
    }
    os << ']';
    return os.str();
}

static void AppendLineWithTruncation(Sink& sink, std::string line) {
    if (sink.truncated) return;
    if (static_cast<int>(sink.lines.size()) >= sink.max_events) {
        sink.truncated = true;
        return;
    }
    sink.lines.push_back(std::move(line));
}

static void AppendEventWithTruncation(Sink& sink, JsonObject obj) {
    if (sink.truncated) return;
    if (static_cast<int>(sink.events.size()) >= sink.max_events) {
        sink.truncated = true;
        return;
    }
    sink.events.emplace_back(std::move(obj));
}

static JsonObject MakeEventBase(const core::Simulator& simulator, std::string kind) {
    JsonObject o;
    o["t"] = static_cast<double>(simulator.time);
    o["kind"] = std::move(kind);
    return o;
}

static void AddItemRef(JsonObject& o, std::string_view prefix, const core::ItemState& item) {
    const int side = GetSideIndex(item);
    const int idx = GetItemIndex(item);
    const std::string_view name = GetItemName(item);

    o[std::string(prefix) + "Side"] = static_cast<double>(side);
    o[std::string(prefix) + "ItemIndex"] = static_cast<double>(idx);
    if (!name.empty()) {
        o[std::string(prefix) + "ItemName"] = std::string(name);
    } else {
        o[std::string(prefix) + "ItemName"] = std::string("");
    }
}

}  // namespace

void Sink::OnFrameEnd(const core::Simulator& simulator) {
    if (sink_type != TypeDetailed) return;

    JsonObject e = MakeEventBase(simulator, "frame_end");
    JsonArray sides;
    sides.reserve(2);
    for (int si = 0; si < core::Simulator::SideCount; si++) {
        JsonObject s;
        const auto& a = simulator.sides[si].attrs;
        s["side"] = static_cast<double>(si);
        s["maxHp"] = static_cast<double>(a[core::SideKey::MaxHp]);
        s["hp"] = static_cast<double>(a[core::SideKey::Hp]);
        s["shield"] = static_cast<double>(a[core::SideKey::Shield]);
        s["burn"] = static_cast<double>(a[core::SideKey::Burn]);
        s["poison"] = static_cast<double>(a[core::SideKey::Poison]);
        s["regen"] = static_cast<double>(a[core::SideKey::Regen]);
        s["resistance"] = static_cast<double>(a[core::SideKey::Resistance]);

        JsonArray items;
        const int item_count = simulator.sides[si].attrs[core::SideKey::ItemCount];
        items.reserve(static_cast<size_t>(std::max(0, item_count)));
        for (int j = 0; j < item_count; j++) {
            const auto& item = simulator.sides[si].items[j];
            JsonObject it;
            it["itemIndex"] = static_cast<double>(j);
            it["name"] = std::string(GetItemName(item));

            // Effective values (considering auras)
            it["Damage"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::Damage));
            it["Burn"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::Burn));
            it["Poison"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::Poison));
            it["Regen"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::Regen));
            it["Shield"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::Shield));
            it["Heal"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::Heal));
            it["Value"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::Value));
            it["Multicast"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::Multicast));
            it["AmmoCap"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::AmmoCap));
            it["Cooldown"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::Cooldown));
            it["LifeSteal"] = static_cast<double>(simulator.GetItemInt(&item, core::ItemKey::LifeSteal));

            // State values (raw runtime)
            it["AmmoRemaining"] = static_cast<double>(item.attrs[core::ItemKey::AmmoRemaining]);
            it["InFlight"] = static_cast<double>(item.attrs[core::ItemKey::InFlight]);
            it["Destroyed"] = static_cast<double>(item.attrs[core::ItemKey::Destroyed]);
            it["ChargedTime"] = static_cast<double>(item.attrs[core::ItemKey::ChargedTime]);
            it["FreezeRemaining"] = static_cast<double>(item.attrs[core::ItemKey::FreezeRemaining]);
            it["SlowRemaining"] = static_cast<double>(item.attrs[core::ItemKey::SlowRemaining]);
            it["HasteRemaining"] = static_cast<double>(item.attrs[core::ItemKey::HasteRemaining]);

            items.emplace_back(std::move(it));
        }
        s["items"] = std::move(items);
        sides.emplace_back(std::move(s));
    }
    e["sides"] = std::move(sides);
    AppendEventWithTruncation(*this, std::move(e));
}

void Sink::OnDamage(const core::Simulator& simulator, const core::ItemState& source, int damage, bool is_crit, bool is_life_steal) {
    if (sink_type == TypeNone) return;

    if (sink_type == TypeSummary) {
        std::ostringstream os;
        os << FormatTime(simulator.time);
        os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
        if (is_life_steal) {
            os << "吸血 " << damage << " 伤害";
        } else {
            os << "造成 " << damage << " 伤害";
            if (is_crit) os << "（暴击）";
        }
        AppendLineWithTruncation(*this, os.str());
        return;
    }

    JsonObject e = MakeEventBase(simulator, "damage");
    AddItemRef(e, "source", source);
    e["targetSide"] = static_cast<double>(1 - GetSideIndex(source));
    e["damage"] = static_cast<double>(damage);
    e["isCrit"] = is_crit;
    e["isLifeSteal"] = is_life_steal;
    AppendEventWithTruncation(*this, std::move(e));
}

void Sink::OnBurn(const core::Simulator& simulator, const core::ItemState& source, int burn, bool is_crit) {
    if (sink_type == TypeNone) return;

    if (sink_type == TypeSummary) {
        std::ostringstream os;
        os << FormatTime(simulator.time);
        os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
        os << "造成 " << burn << " 灼烧";
        if (is_crit) os << "（暴击）";
        AppendLineWithTruncation(*this, os.str());
        return;
    }

    JsonObject e = MakeEventBase(simulator, "burn");
    AddItemRef(e, "source", source);
    e["targetSide"] = static_cast<double>(1 - GetSideIndex(source));
    e["burn"] = static_cast<double>(burn);
    e["isCrit"] = is_crit;
    AppendEventWithTruncation(*this, std::move(e));
}

void Sink::OnPoison(const core::Simulator& simulator, const core::ItemState& source, int poison, bool is_crit, bool is_self_poison) {
    if (sink_type == TypeNone) return;

    if (sink_type == TypeSummary) {
        std::ostringstream os;
        os << FormatTime(simulator.time);
        os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
        os << "造成 " << poison << ' ' << (is_self_poison ? "自我剧毒" : "剧毒");
        if (is_crit) os << "（暴击）";
        AppendLineWithTruncation(*this, os.str());
        return;
    }

    JsonObject e = MakeEventBase(simulator, "poison");
    AddItemRef(e, "source", source);
    const int target_side = is_self_poison ? GetSideIndex(source) : (1 - GetSideIndex(source));
    e["targetSide"] = static_cast<double>(target_side);
    e["poison"] = static_cast<double>(poison);
    e["isCrit"] = is_crit;
    e["isSelfPoison"] = is_self_poison;
    AppendEventWithTruncation(*this, std::move(e));
}

void Sink::OnRegen(const core::Simulator& simulator, const core::ItemState& source, int regen, bool is_crit) {
    if (sink_type == TypeNone) return;

    if (sink_type == TypeSummary) {
        std::ostringstream os;
        os << FormatTime(simulator.time);
        os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
        os << "造成 " << regen << " 生命再生";
        if (is_crit) os << "（暴击）";
        AppendLineWithTruncation(*this, os.str());
        return;
    }

    JsonObject e = MakeEventBase(simulator, "regen");
    AddItemRef(e, "source", source);
    e["targetSide"] = static_cast<double>(GetSideIndex(source));
    e["regen"] = static_cast<double>(regen);
    e["isCrit"] = is_crit;
    AppendEventWithTruncation(*this, std::move(e));
}

void Sink::OnShield(const core::Simulator& simulator, const core::ItemState& source, int shield, bool is_crit) {
    if (sink_type == TypeNone) return;

    if (sink_type == TypeSummary) {
        std::ostringstream os;
        os << FormatTime(simulator.time);
        os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
        os << "造成 " << shield << " 护盾";
        if (is_crit) os << "（暴击）";
        AppendLineWithTruncation(*this, os.str());
        return;
    }

    JsonObject e = MakeEventBase(simulator, "shield");
    AddItemRef(e, "source", source);
    e["shield"] = static_cast<double>(shield);
    e["isCrit"] = is_crit;
    AppendEventWithTruncation(*this, std::move(e));
}

void Sink::OnHeal(const core::Simulator& simulator, const core::ItemState& source, int heal, bool is_crit) {
    if (sink_type == TypeNone) return;

    if (sink_type == TypeSummary) {
        std::ostringstream os;
        os << FormatTime(simulator.time);
        os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
        os << "造成 " << heal << " 治疗";
        if (is_crit) os << "（暴击）";
        AppendLineWithTruncation(*this, os.str());
        return;
    }

    JsonObject e = MakeEventBase(simulator, "heal");
    AddItemRef(e, "source", source);
    e["heal"] = static_cast<double>(heal);
    e["isCrit"] = is_crit;
    AppendEventWithTruncation(*this, std::move(e));
}

void Sink::OnCharge(const core::Simulator& simulator, const core::ItemState& source, int target_count, int charge) {
    if (sink_type != TypeSummary) return;
    if (target_count <= 0) return;
    std::ostringstream os;
    os << FormatTime(simulator.time);
    os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
    os << "充能 " << (static_cast<double>(charge) / 1000.0) << " 秒 → " << FormatTargetItemNames(simulator, target_count);
    AppendLineWithTruncation(*this, os.str());
}

void Sink::OnHaste(const core::Simulator& simulator, const core::ItemState& source, int target_count, int haste) {
    if (sink_type != TypeSummary) return;
    if (target_count <= 0) return;
    std::ostringstream os;
    os << FormatTime(simulator.time);
    os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
    os << "加速 " << (static_cast<double>(haste) / 1000.0) << " 秒 → " << FormatTargetItemNames(simulator, target_count);
    AppendLineWithTruncation(*this, os.str());
}

void Sink::OnSlow(const core::Simulator& simulator, const core::ItemState& source, int target_count, int slow) {
    if (sink_type != TypeSummary) return;
    if (target_count <= 0) return;
    std::ostringstream os;
    os << FormatTime(simulator.time);
    os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
    os << "减速 " << (static_cast<double>(slow) / 1000.0) << " 秒 → " << FormatTargetItemNames(simulator, target_count);
    AppendLineWithTruncation(*this, os.str());
}

void Sink::OnFreeze(const core::Simulator& simulator, const core::ItemState& source, int target_count, int freeze) {
    if (sink_type != TypeSummary) return;
    if (target_count <= 0) return;
    std::ostringstream os;
    os << FormatTime(simulator.time);
    os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
    os << "冻结 " << (static_cast<double>(freeze) / 1000.0) << " 秒 → " << FormatTargetItemNames(simulator, target_count);
    AppendLineWithTruncation(*this, os.str());
}

void Sink::OnReload(const core::Simulator& simulator, const core::ItemState& source, int target_count, int reload) {
    if (sink_type != TypeSummary) return;
    if (target_count <= 0) return;
    std::ostringstream os;
    os << FormatTime(simulator.time);
    os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
    if (reload == 99) os << "装填";
    else os << "装填 " << reload << " 弹药";
    os << " → " << FormatTargetItemNames(simulator, target_count);
    AppendLineWithTruncation(*this, os.str());
}

void Sink::OnDestroy(const core::Simulator& simulator, const core::ItemState& source, int target_count) {
    if (sink_type != TypeSummary) return;
    if (target_count <= 0) return;
    std::ostringstream os;
    os << FormatTime(simulator.time);
    os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
    os << "摧毁 → " << FormatTargetItemNames(simulator, target_count);
    AppendLineWithTruncation(*this, os.str());
}

void Sink::OnRepair(const core::Simulator& simulator, const core::ItemState& source, int target_count) {
    if (sink_type != TypeSummary) return;
    if (target_count <= 0) return;
    std::ostringstream os;
    os << FormatTime(simulator.time);
    os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
    os << "修复 → " << FormatTargetItemNames(simulator, target_count);
    AppendLineWithTruncation(*this, os.str());
}

void Sink::OnAttributeIncrease(const core::Simulator& simulator, const core::ItemState& source, int target_count, int attribute, int value) {
    if (sink_type != TypeSummary) return;
    if (target_count <= 0) return;
    std::ostringstream os;
    os << FormatTime(simulator.time);
    os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
    if (attribute == core::ItemKey::InFlight) {
        os << "开始飞行";
        os << " → " << FormatTargetItemNames(simulator, target_count);
        AppendLineWithTruncation(*this, os.str());
        return;
    }
    os << "属性提高 " << value << "（attr=" << attribute << "） → " << FormatTargetItemNames(simulator, target_count);
    AppendLineWithTruncation(*this, os.str());
}

void Sink::OnAttributeDecrease(const core::Simulator& simulator, const core::ItemState& source, int target_count, int attribute, int value) {
    if (sink_type != TypeSummary) return;
    if (target_count <= 0) return;
    std::ostringstream os;
    os << FormatTime(simulator.time);
    os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] ";
    if (attribute == core::ItemKey::InFlight) {
        os << "停止飞行";
        os << " → " << FormatTargetItemNames(simulator, target_count);
        AppendLineWithTruncation(*this, os.str());
        return;
    }
    os << "属性降低 " << value << "（attr=" << attribute << "） → " << FormatTargetItemNames(simulator, target_count);
    AppendLineWithTruncation(*this, os.str());
}

void Sink::OnResistance(const core::Simulator& simulator, const core::ItemState& source, int old_resistance, int delta_resistance) {
    // 2 - 不进行输出（抗性改变效果会在 OnFrameEnd 中显示）
    if (sink_type != TypeSummary) return;

    const int new_resistance = old_resistance + delta_resistance;
    const bool was_invincible = (old_resistance >= 100);
    const bool now_invincible = (new_resistance >= 100);
    if (was_invincible == now_invincible) return;

    std::ostringstream os;
    os << FormatTime(simulator.time);
    if (now_invincible) {
        os << "玩家" << (GetSideIndex(source) + 1) << " [" << GetItemName(source) << "] 无敌";
    } else {
        os << "玩家" << (GetSideIndex(source) + 1) << " 解除无敌";
    }
    AppendLineWithTruncation(*this, os.str());
}

void Sink::OnGameEnd(const core::Simulator& simulator, int winner) {
    if (sink_type == TypeNone) return;

    if (sink_type == TypeSummary) {
        std::ostringstream os;
        os << FormatTime(simulator.time);
        if (winner < 0) {
            os << "平局";
        } else {
            os << "玩家" << (winner + 1) << " 获胜";
        }
        AppendLineWithTruncation(*this, os.str());
        return;
    }

    JsonObject e = MakeEventBase(simulator, "game_end");
    e["winner"] = static_cast<double>(winner);
    e["isDraw"] = (winner < 0);
    AppendEventWithTruncation(*this, std::move(e));
}

}  // namespace bazaararena::io

