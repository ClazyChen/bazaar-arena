from __future__ import annotations

import re

from tools.item_codegen.src.formula_emit import (
    ITEM_KEY_NAMES,
    emit_formula_ast,
    item_key_cpp_from_name,
    merge_with_basic,
)


def _cpp_escape(s: str) -> str:
    return (
        s.replace("\\", "\\\\")
        .replace('"', '\\"')
        .replace("\n", "\\n")
        .replace("\r", "")
        .replace("\t", "\\t")
    )


def _as_int(v: object, *, default: int = 0) -> int:
    if v is None:
        return default
    if isinstance(v, bool):
        raise TypeError("bool 不是 int")
    if isinstance(v, int):
        return v
    raise TypeError(f"期望 int，实际 {type(v)}")


def _as_str(v: object, *, default: str = "") -> str:
    if v is None:
        return default
    if isinstance(v, str):
        return v
    raise TypeError(f"期望 string，实际 {type(v)}")


def _as_str_list(v: object) -> list[str]:
    if v is None:
        return []
    if not isinstance(v, list):
        raise TypeError(f"期望 list，实际 {type(v)}")
    out: list[str] = []
    for x in v:
        if not isinstance(x, str):
            raise TypeError(f"期望 string list，实际含 {type(x)}")
        out.append(x)
    return out


def _as_int_list(v: object) -> list[int]:
    if v is None:
        return []
    if not isinstance(v, list):
        raise TypeError(f"期望 int list，实际 {type(v)}")
    out: list[int] = []
    for x in v:
        out.append(_as_int(x))
    return out


# YAML 中秒单位：3s / 3_s / "3.5_s" → C++ N_s 字面量
_DURATION_SUFFIX_RE = re.compile(r"^(\d+(?:\.\d+)?)(?:s|_s)$")

# 不参与「通用 ItemKey 字段」解析（结构字段或已单独生成）
_YAML_STRUCT_SPECIAL_KEYS = frozenset(
    {
        "Name",
        "name",
        "nameZh",
        "name_zh",
        "Desc",
        "desc",
        "Size",
        "size",
        "Tier",
        "tier",
        "minTier",
        "min_tier",
        "Tags",
        "tags",
        "Abilities",
        "abilities",
        "Auras",
        "auras",
        "_source_yaml",
        "_hero",
    }
)

_YAML_FIELD_ALIASES: dict[str, str] = {
    "cooldown": "Cooldown",
    "cooldownSeconds": "Cooldown",
}


def _canonical_item_field_key(yaml_key: str) -> str:
    k = yaml_key.strip()
    return _YAML_FIELD_ALIASES.get(k, k)


def _emit_scalar_for_item_attr(raw: object, *, where: str) -> str:
    """生成赋给 int 属性的 C++ 右值：整数 / 浮点 / 时长 Ns 或 N_s。"""
    if isinstance(raw, bool):
        raise TypeError(f"{where}: 不能为 bool")
    if isinstance(raw, int):
        return str(raw)
    if isinstance(raw, float):
        return repr(raw)
    if isinstance(raw, str):
        s = raw.strip()
        m = _DURATION_SUFFIX_RE.match(s)
        if m:
            return f"{m.group(1)}_s"
        raise ValueError(f"{where}: 期望数字或时长字符串（如 3s、3.5_s），实际 {raw!r}")
    raise TypeError(f"{where}: 不支持的类型 {type(raw)}")


def _iter_tier_values_from_min(
    vals: list[object], min_tier: int, *, where: str
) -> list[tuple[int, object]]:
    """从 minTier 起每项对应一阶；若未到 Legendary，最后一项复用到 tier 4。"""
    if min_tier < 0 or min_tier > 4:
        raise ValueError(f"{where}: minTier 索引非法：{min_tier}")
    if not vals:
        raise ValueError(f"{where}: 升阶列表不能为空")
    out: list[tuple[int, object]] = []
    for i, raw in enumerate(vals):
        tidx = min_tier + i
        if tidx > 4:
            raise ValueError(
                f"{where}: 升阶项超出 Legendary（minTier={min_tier}，第{i + 1}档对应 tier={tidx}）"
            )
        out.append((tidx, raw))
    last = vals[-1]
    nxt = min_tier + len(vals)
    for tidx in range(nxt, 5):
        out.append((tidx, last))
    return out


def _emit_generic_item_key_fields(
    lines: list[str],
    item: dict,
    *,
    min_tier_idx: int,
    src: str,
    item_name: str,
) -> None:
    """除 Name/Desc/Size/Tier/Tags/Abilities/Auras 外，其余键一律视为 ItemKey 模板字段。"""
    keys = sorted(k for k in item.keys() if k not in _YAML_STRUCT_SPECIAL_KEYS)
    for yk in keys:
        canon = _canonical_item_field_key(yk)
        if canon not in ITEM_KEY_NAMES:
            raise ValueError(
                f"{src} 物品「{item_name}」: 未知字段 {yk!r}（映射为 {canon!r}），应为 ItemKey 名"
            )
        val = item[yk]
        where_f = f"{src} {item_name}.{canon}"
        ik = f"core::ItemKey::{canon}"
        if isinstance(val, list):
            pairs = _iter_tier_values_from_min(val, min_tier_idx, where=where_f)
            for tidx, raw in pairs:
                rhs = _emit_scalar_for_item_attr(raw, where=f"{where_f}[tier={tidx}]")
                lines.append(f"    t.attributes[{tidx}][{ik}] = {rhs};")
        else:
            rhs = _emit_scalar_for_item_attr(val, where=where_f)
            lines.append(f"    for (auto& tier : t.attributes) tier[{ik}] = {rhs};")


def _tag_to_cpp(tag: str) -> str:
    return f"core::Tag::{tag}"


def _ability_type_to_cpp(typ: str) -> str:
    return f"core::AbilityType::{typ}"


def _trigger_to_cpp(trig: str) -> str:
    return f"core::Trigger::{trig}"


# 基础目标条件（AbilityType）
_BASIC_TARGET_SAME_SIDE = frozenset(
    {"Charge", "Haste", "Reload", "Repair", "AddAttribute", "Cast"}
)
_BASIC_TARGET_DIFF_SIDE = frozenset({"Slow", "Destroy", "Freeze", "ReduceAttribute"})


def _basic_target_condition_for_ability(ability_type: str) -> str:
    if ability_type in _BASIC_TARGET_SAME_SIDE:
        return "SameSide"
    if ability_type in _BASIC_TARGET_DIFF_SIDE:
        return "DifferentSide"
    return "Always"


def _basic_trigger_condition(trigger_yaml: str) -> str:
    t = (trigger_yaml or "").strip()
    if t == "" or t == "Cast":
        return "SameAsCaster"
    return "SameSide"


def _resolve_value_key_cpp(ab: dict, ability_type: str, *, where: str) -> str:
    if "value_key" in ab:
        return item_key_cpp_from_name(str(ab["value_key"]), where=f"{where}.value_key")
    defaults: dict[str, str] = {
        "Damage": "Damage",
        "Burn": "Burn",
        "Poison": "Poison",
        "Shield": "Shield",
        "Heal": "Heal",
        "Charge": "Charge",
        "Haste": "Haste",
        "Slow": "Slow",
        "Freeze": "Freeze",
        "Reload": "Reload",
        "Repair": "Repair",
        "Destroy": "Destroy",
        "AddAttribute": "Custom_0",
        "ReduceAttribute": "Custom_0",
        "GainGold": "Custom_0",
        "Regen": "Regen",
        "Resistance": "Custom_0",
        "PoisonSelf": "Poison",
        "Cast": "Custom_0",
    }
    if ability_type in defaults:
        return f"core::ItemKey::{defaults[ability_type]}"
    raise ValueError(f"{where}: 缺少 value_key，且 Ability.type={ability_type} 无默认映射")


def _resolve_attribute_key_cpp(ab: dict, ability_type: str, *, where: str) -> str | None:
    if "attribute_key" in ab:
        return item_key_cpp_from_name(str(ab["attribute_key"]), where=f"{where}.attribute_key")
    if ability_type in ("AddAttribute", "ReduceAttribute"):
        return "core::ItemKey::Damage"
    return None


def _default_target_count_key_cpp(ability_type: str) -> str | None:
    m = {
        "Charge": "ChargeTargetCount",
        "Haste": "HasteTargetCount",
        "Slow": "SlowTargetCount",
        "Freeze": "FreezeTargetCount",
        "Reload": "ReloadTargetCount",
        "Destroy": "DestroyTargetCount",
        "Repair": "RepairTargetCount",
        "AddAttribute": "ModifyAttributeTargetCount",
        "ReduceAttribute": "ModifyAttributeTargetCount",
    }
    k = m.get(ability_type)
    return f"core::ItemKey::{k}" if k else None


def _priority_to_cpp(name: str) -> str:
    allowed = frozenset(
        {
            "Immediate",
            "Highest",
            "High",
            "Medium",
            "Low",
            "Lowest",
        }
    )
    if name not in allowed:
        raise ValueError(f"未知 priority：{name}")
    return f"core::AbilityPriority::{name}"


def _get_any(item: dict, *keys: str) -> object:
    for k in keys:
        if k in item:
            return item[k]
    return None


def _tier_name_to_cpp(v: object, *, src: str) -> tuple[str, int]:
    if v is None:
        return ("core::ItemTier::Bronze", 0)
    if isinstance(v, int):
        if v < 0 or v >= 5:
            raise ValueError(f"{src}: Tier/minTier 超出范围：{v}")
        return (str(v), v)
    if not isinstance(v, str):
        raise TypeError(f"{src}: Tier/minTier 必须是 string 或 int")
    mapping: dict[str, tuple[str, int]] = {
        "Bronze": ("core::ItemTier::Bronze", 0),
        "Silver": ("core::ItemTier::Silver", 1),
        "Gold": ("core::ItemTier::Gold", 2),
        "Diamond": ("core::ItemTier::Diamond", 3),
        "Legendary": ("core::ItemTier::Legendary", 4),
    }
    if v not in mapping:
        raise ValueError(f"{src}: 未知 Tier/minTier：{v}")
    return mapping[v]


def _size_name_to_cpp(v: object, *, src: str) -> tuple[str, int]:
    if v is None:
        return ("core::ItemSize::Medium", 2)
    if isinstance(v, int):
        if v not in (1, 2, 3):
            raise ValueError(f"{src}: Size/size 超出范围：{v}")
        return (str(v), v)
    if not isinstance(v, str):
        raise TypeError(f"{src}: Size/size 必须是 string 或 int")
    mapping: dict[str, tuple[str, int]] = {
        "Small": ("core::ItemSize::Small", 1),
        "Medium": ("core::ItemSize::Medium", 2),
        "Large": ("core::ItemSize::Large", 3),
    }
    if v not in mapping:
        raise ValueError(f"{src}: 未知 Size/size：{v}")
    return mapping[v]


def _default_value_by_size_tier(size_int: int) -> list[int]:
    # tier 0..3 by table; tier 4 (Legendary) fixed to 32
    # NOTE: 需求里 large 写成 [3.6,12,24]，这里按常见阶梯理解为 [3,6,12,24]
    if size_int == 1:  # Small
        base = [1, 2, 4, 8]
    elif size_int == 2:  # Medium
        base = [2, 4, 8, 16]
    elif size_int == 3:  # Large
        base = [3, 6, 12, 24]
    else:
        base = [2, 4, 8, 16]
    return [base[0], base[1], base[2], base[3], 32]


def _emit_item_lambda(item: dict) -> str:
    src = _as_str(item.get("_source_yaml"), default="")

    # 主键与显示名：Name（中文）；兼容旧字段 nameZh
    item_name = _as_str(_get_any(item, "Name", "name", "nameZh", "name_zh"), default="")
    desc = _as_str(_get_any(item, "Desc", "desc"), default="")

    tags = _as_str_list(_get_any(item, "Tags", "tags"))
    abilities = item.get("Abilities", [])
    if not isinstance(abilities, list):
        raise TypeError("Abilities 必须是 list")

    size_cpp, size_int = _size_name_to_cpp(_get_any(item, "Size", "size"), src=src)
    tier_cpp, min_tier_idx = _tier_name_to_cpp(_get_any(item, "Tier", "minTier", "min_tier"), src=src)

    lines: list[str] = []
    lines.append("[]() {")
    lines.append("    core::ItemTemplate t;")
    lines.append(f'    t.name = "{_cpp_escape(item_name)}";')
    lines.append(f'    t.desc = "{_cpp_escape(desc)}";')

    # ---- defaults (all tiers) ----
    lines.append(f"    for (auto& tier : t.attributes) tier[core::ItemKey::Size] = {size_cpp};")
    lines.append(f"    for (auto& tier : t.attributes) tier[core::ItemKey::MinTier] = {tier_cpp};")
    lines.append("    for (auto& tier : t.attributes) tier[core::ItemKey::Multicast] = 1;")
    lines.append("    for (auto& tier : t.attributes) tier[core::ItemKey::CritDamage] = 200;")
    lines.append("    for (auto& tier : t.attributes) tier[core::ItemKey::Reload] = 99;")
    for key in (
        "ChargeTargetCount",
        "HasteTargetCount",
        "SlowTargetCount",
        "FreezeTargetCount",
        "ReloadTargetCount",
        "DestroyTargetCount",
        "RepairTargetCount",
        "ModifyAttributeTargetCount",
    ):
        lines.append(f"    for (auto& tier : t.attributes) tier[core::ItemKey::{key}] = 20;")

    default_values = _default_value_by_size_tier(size_int)
    for i, v in enumerate(default_values):
        lines.append(f"    t.attributes[{i}][core::ItemKey::Value] = {v};")

    if tags:
        tag_expr = " | ".join(_tag_to_cpp(x) for x in tags)
        lines.append(f"    for (auto& tier : t.attributes) tier[core::ItemKey::Tags] = ({tag_expr});")

    _emit_generic_item_key_fields(
        lines,
        item,
        min_tier_idx=min_tier_idx,
        src=src,
        item_name=item_name,
    )

    # abilities
    lines.append("    t.ability_count = 0;")
    for abi, ab in enumerate(abilities):
        if not isinstance(ab, dict):
            raise TypeError("Abilities[] 每个元素必须是 object")
        typ = _as_str(ab.get("type"))
        trig = _as_str(ab.get("trigger"), default="")
        trig_cpp = _trigger_to_cpp(trig) if trig else "core::Trigger::Cast"
        where_ab = f"{src} {item_name} Abilities[{abi}]"

        lines.append("    {")
        lines.append("        auto& a = t.abilities[t.ability_count++];")
        lines.append(f"        a.type = {_ability_type_to_cpp(typ)};")
        if "priority" in ab:
            lines.append(f"        a.priority = {_priority_to_cpp(_as_str(ab['priority']))};")
        lines.append("        a.trigger_entry_count = 1;")
        lines.append(f"        a.trigger_entries[0].trigger = {trig_cpp};")

        if "condition" in ab:
            ce = emit_formula_ast(ab["condition"], where=f"{where_ab}.condition")
            lines.append(f"        a.trigger_entries[0].condition = {ce};")
        else:
            basic_tr = _basic_trigger_condition(trig)
            tr_merged = merge_with_basic(
                basic=basic_tr,
                extra=ab.get("ex_condition"),
                where=f"{where_ab}.ex_condition",
            )
            lines.append(f"        a.trigger_entries[0].condition = {tr_merged};")

        if "target_condition" in ab:
            te = emit_formula_ast(ab["target_condition"], where=f"{where_ab}.target_condition")
            lines.append(f"        a.target_condition = {te};")
        else:
            basic_tg = _basic_target_condition_for_ability(typ)
            tg_merged = merge_with_basic(
                basic=basic_tg,
                extra=ab.get("ex_target_condition"),
                where=f"{where_ab}.ex_target_condition",
            )
            lines.append(f"        a.target_condition = {tg_merged};")

        lines.append(f"        a.value_key = {_resolve_value_key_cpp(ab, typ, where=where_ab)};")
        attr_k = _resolve_attribute_key_cpp(ab, typ, where=where_ab)
        if attr_k is not None:
            lines.append(f"        a.attribute_key = {attr_k};")

        if "target_count_key" in ab:
            lines.append(
                f"        a.target_count_key = {item_key_cpp_from_name(str(ab['target_count_key']), where=f'{where_ab}.target_count_key')};"
            )
        else:
            dtk = _default_target_count_key_cpp(typ)
            if dtk is not None:
                lines.append(f"        a.target_count_key = {dtk};")

        lines.append("    }")

    # Auras
    auras = item.get("Auras") or item.get("auras") or []
    if not isinstance(auras, list):
        raise TypeError("Auras 必须是 list")
    lines.append("    t.aura_count = 0;")
    for aui, au in enumerate(auras):
        if not isinstance(au, dict):
            raise TypeError("Auras[] 每个元素必须是 object")
        where_au = f"{src} {item_name} Auras[{aui}]"
        attr = _as_str(au.get("attribute"))
        if not attr:
            raise ValueError(f"{where_au}: 缺少 attribute")
        if "value" not in au:
            raise ValueError(f"{where_au}: 缺少 value")
        if "condition" in au:
            cond_e = emit_formula_ast(au["condition"], where=f"{where_au}.condition")
        else:
            cond_e = "SameAsCaster"
        val_e = emit_formula_ast(au["value"], where=f"{where_au}.value")
        pct = bool(au.get("percent", False))
        lines.append("    {")
        lines.append("        auto& g = t.auras[t.aura_count++];")
        lines.append(f"        g.attribute = {item_key_cpp_from_name(attr, where=f'{where_au}.attribute')};")
        lines.append(f"        g.condition = {cond_e};")
        lines.append(f"        g.value = {val_e};")
        lines.append(f"        g.percent = {'true' if pct else 'false'};")
        lines.append("    }")

    # 生成 key 便于调试（不进引擎结构，只由 ItemDatabase 用）
    if not item_name:
        raise ValueError(f"{src}: item.Name 不能为空")

    lines.append("    return t;")
    lines.append("}()")
    return "\n".join(lines)


def _emit_generated_items(items: list[dict]) -> tuple[list[str], list[str]]:
    # returns (keys, lambdas) aligned
    keys: list[str] = []
    lambdas: list[str] = []
    for item in items:
        k = _as_str(_get_any(item, "Name", "name", "nameZh", "name_zh"), default="")
        keys.append(k)
        lambdas.append(_emit_item_lambda(item))
    return keys, lambdas


def emit_cpp_static_data(items: list[dict]) -> str:
    keys, lambdas = _emit_generated_items(items)

    # 稳定输出：按 key 排序，同时保持 templ 对齐
    pairs = sorted(zip(keys, lambdas), key=lambda x: x[0])

    out: list[str] = []
    out.append("// This file is @generated by tools/gen_items_cpp.py. DO NOT EDIT.\n")
    out.append("#include <array>")
    out.append("#include <span>")
    out.append("#include <string_view>")
    out.append("")
    out.append('#include "bazaararena/data/GeneratedItems.hpp"')
    out.append('#include "bazaararena/core/AbilityDefinition.hpp"')
    out.append('#include "bazaararena/core/AbilityPriority.hpp"')
    out.append('#include "bazaararena/core/AbilityType.hpp"')
    out.append('#include "bazaararena/core/ItemKey.hpp"')
    out.append('#include "bazaararena/core/ItemSize.hpp"')
    out.append('#include "bazaararena/core/ItemTier.hpp"')
    out.append('#include "bazaararena/core/Tag.hpp"')
    out.append('#include "bazaararena/core/Trigger.hpp"')
    out.append('#include "bazaararena/formula/Formula.hpp"')
    out.append('#include "bazaararena/formula/Condition.hpp"')
    out.append('#include "bazaararena/literals/duration.hpp"')
    out.append("")
    out.append("namespace bazaararena::data::generated {")
    out.append("")
    out.append("using namespace bazaararena::literals::duration_literals;")
    out.append("using namespace bazaararena::condition;")
    out.append("")
    out.append("namespace core = bazaararena::core;")
    out.append("namespace formula = bazaararena::formula;")
    out.append("")
    out.append(f"static const std::array<GeneratedItem, {len(pairs)}> kItems = {{")
    for key, lam in pairs:
        out.append("    GeneratedItem{")
        out.append(f'        .key = "{_cpp_escape(key)}",')
        out.append(f"        .templ = {lam},")
        out.append("    },")
    out.append("};")
    out.append("")
    out.append("std::span<const GeneratedItem> GetGeneratedItems() {")
    out.append("    return std::span<const GeneratedItem>(kItems.data(), kItems.size());")
    out.append("}")
    out.append("")
    out.append("}  // namespace bazaararena::data::generated")
    out.append("")
    return "\n".join(out)

