from __future__ import annotations

# YAML { type, params } / 字符串简写 → C++ formula/condition 模板实例（生成源码字符串）。
# 生成文件内需：using namespace bazaararena::condition; namespace formula = bazaararena::formula;


def _item_key_cpp(name: str) -> str:
    return f"core::ItemKey::{name}"


def _side_key_cpp(name: str) -> str:
    return f"core::SideKey::{name}"


def _tag_cpp(name: str) -> str:
    return f"core::Tag::{name}"


def _derived_tag_cpp(name: str) -> str:
    return f"core::DerivedTag::{name}"


# 允许出现在 Item/Caster 上的 ItemKey 名（与 engine ItemKey 一致）
# 与 data/items 模板字段对应的 ItemKey 名（供 emit_cpp 校验）
ITEM_KEY_NAMES = frozenset(
    {
        "Id",
        "SideIndex",
        "ItemIndex",
        "Damage",
        "Burn",
        "Poison",
        "Shield",
        "Heal",
        "Regen",
        "CritRate",
        "CritDamage",
        "Multicast",
        "AmmoCap",
        "AmmoRemaining",
        "Charge",
        "ChargeTargetCount",
        "Haste",
        "HasteTargetCount",
        "Slow",
        "SlowTargetCount",
        "PercentSlowReduction",
        "Freeze",
        "FreezeTargetCount",
        "PercentFreezeReduction",
        "Reload",
        "ReloadTargetCount",
        "DestroyTargetCount",
        "RepairTargetCount",
        "InFlight",
        "Destroyed",
        "Cooldown",
        "ChargedTime",
        "FreezeRemaining",
        "SlowRemaining",
        "HasteRemaining",
        "Value",
        "Tags",
        "DerivedTags",
        "Size",
        "Quest",
        "LifeSteal",
        "ModifyAttributeTargetCount",
        "Hero",
        "MinTier",
        "Tier",
        "CooldownReduction",
        "CooldownReductionPercent",
        "Custom_0",
        "Custom_1",
        "Custom_2",
        "Custom_3",
    }
)

_ITEM_KEYS = ITEM_KEY_NAMES

_SIDE_KEYS = frozenset(
    {
        "Id",
        "MaxHp",
        "Hp",
        "Shield",
        "Burn",
        "Poison",
        "Regen",
        "Gold",
        "Income",
        "Resistance",
        "ItemCount",
    }
)


def emit_formula_ast(node: object, *, where: str = "") -> str:
    """
    将 YAML 公式 AST 转为 C++ 表达式字符串。
    - dict: { type: str, params?: list }
    - str: 无参叶子（如 AdjacentToCaster、SameSide），需在 condition 或 formula 中可找到
    - int: 当作 Constant<n>（少见，优先用 {type: Constant, params: [n]}）
    """
    if node is None:
        raise ValueError(f"{where}: 公式节点不能为 null")

    if isinstance(node, bool):
        raise TypeError(f"{where}: 公式节点不能是 bool")

    if isinstance(node, int):
        return f"formula::Constant<{node}>"

    if isinstance(node, str):
        return _emit_named_leaf(node.strip(), where=where)

    if not isinstance(node, dict):
        raise TypeError(f"{where}: 公式节点必须是 object、string 或 int，实际 {type(node)}")

    typ = node.get("type")
    if not isinstance(typ, str) or not typ:
        raise ValueError(f"{where}: 缺少 type")
    typ = typ.strip()
    params = node.get("params", [])
    if params is None:
        params = []
    if not isinstance(params, list):
        raise TypeError(f"{where}: params 必须是 list")

    return _emit_by_type(typ, params, where=f"{where}.{typ}")


def _emit_named_leaf(name: str, *, where: str) -> str:
    # 无命名空间的裸名：优先 condition 常用，与生成文件 using condition 一致
    direct = {
        "True": "formula::True",
        "False": "formula::False",
        "Always": "Always",
        "Never": "Never",
        "SameAsCaster": "SameAsCaster",
        "DifferentFromCaster": "DifferentFromCaster",
        "SameSide": "SameSide",
        "DifferentSide": "DifferentSide",
        "Destroyed": "Destroyed",
        "NotDestroyed": "NotDestroyed",
        "HasCooldown": "HasCooldown",
        "CanCrit": "CanCrit",
        "NotFullyCharged": "NotFullyCharged",
        "NotFrozen": "NotFrozen",
        "AdjacentToCaster": "AdjacentToCaster",
        "InFlight": "InFlight",
        "NotInFlight": "NotInFlight",
    }
    if name in direct:
        return direct[name]
    raise ValueError(f"{where}: 未知的公式/条件叶子：{name}")


def _emit_children(params: list[object], *, where: str) -> list[str]:
    out: list[str] = []
    for i, p in enumerate(params):
        out.append(emit_formula_ast(p, where=f"{where}[{i}]"))
    return out


def _emit_by_type(typ: str, params: list[object], *, where: str) -> str:
    # 组合子（formula 命名空间）
    nary = ("And", "Or", "Xor", "Add", "Sub", "Mul", "Eq", "Ne", "Lt", "Le", "Gt", "Ge")
    if typ in nary:
        ch = _emit_children(params, where=where)
        if len(ch) < 1:
            raise ValueError(f"{where}: {typ} 至少需要 1 个参数")
        if len(ch) == 1:
            return ch[0]
        return f"formula::{typ}<{', '.join(ch)}>"

    if typ == "Not":
        if len(params) != 1:
            raise ValueError(f"{where}: Not 需要 1 个参数")
        inner = emit_formula_ast(params[0], where=f"{where}.0")
        return f"formula::Not<{inner}>"

    if typ == "Abs":
        if len(params) != 1:
            raise ValueError(f"{where}: Abs 需要 1 个参数")
        inner = emit_formula_ast(params[0], where=f"{where}.0")
        return f"formula::Abs<{inner}>"

    if typ == "Constant":
        if len(params) != 1:
            raise ValueError(f"{where}: Constant 需要 1 个 int 参数")
        v = params[0]
        if not isinstance(v, int) or isinstance(v, bool):
            raise TypeError(f"{where}: Constant 参数必须是 int")
        return f"formula::Constant<{v}>"

    if typ == "Item":
        if len(params) != 1:
            raise ValueError(f"{where}: Item 需要 1 个 ItemKey 名")
        k = _key_name(params[0], "ItemKey", where)
        return f"formula::Item<{_item_key_cpp(k)}>"

    if typ == "Caster":
        if len(params) != 1:
            raise ValueError(f"{where}: Caster 需要 1 个 ItemKey 名")
        k = _key_name(params[0], "ItemKey", where)
        return f"formula::Caster<{_item_key_cpp(k)}>"

    if typ == "Side":
        if len(params) != 1:
            raise ValueError(f"{where}: Side 需要 1 个 SideKey 名")
        k = _key_name(params[0], "SideKey", where)
        return f"formula::Side<{_side_key_cpp(k)}>"

    if typ == "Opp":
        if len(params) != 1:
            raise ValueError(f"{where}: Opp 需要 1 个 SideKey 名")
        k = _key_name(params[0], "SideKey", where)
        return f"formula::Opp<{_side_key_cpp(k)}>"

    if typ == "HasTag":
        if len(params) != 1:
            raise ValueError(f"{where}: HasTag 需要 1 个 Tag 名")
        tname = _key_name(params[0], "Tag", where)
        return f"HasTag<{_tag_cpp(tname)}>"

    if typ == "HasDerivedTag":
        if len(params) != 1:
            raise ValueError(f"{where}: HasDerivedTag 需要 1 个 DerivedTag 名")
        dname = _key_name(params[0], "DerivedTag", where)
        return f"HasDerivedTag<{_derived_tag_cpp(dname)}>"

    if typ == "NotHasTag":
        if len(params) != 1:
            raise ValueError(f"{where}: NotHasTag 需要 1 个 Tag 名")
        tname = _key_name(params[0], "Tag", where)
        return f"NotHasTag<{_tag_cpp(tname)}>"

    if typ == "NotHasDerivedTag":
        if len(params) != 1:
            raise ValueError(f"{where}: NotHasDerivedTag 需要 1 个 DerivedTag 名")
        dname = _key_name(params[0], "DerivedTag", where)
        return f"NotHasDerivedTag<{_derived_tag_cpp(dname)}>"

    raise ValueError(f"{where}: 未知的公式类型：{typ}")


def _key_name(v: object, kind: str, where: str) -> str:
    if not isinstance(v, str) or not v:
        raise TypeError(f"{where}: 期望 {kind} 名字符串")
    name = v.strip()
    if kind == "ItemKey":
        if name not in _ITEM_KEYS:
            raise ValueError(f"{where}: 未知 ItemKey：{name}")
        return name
    if kind == "SideKey":
        if name not in _SIDE_KEYS:
            raise ValueError(f"{where}: 未知 SideKey：{name}")
        return name
    if kind == "Tag":
        return name
    if kind == "DerivedTag":
        return name
    raise RuntimeError(kind)


def merge_with_basic(*, basic: str, extra: object | None, where: str) -> str:
    """final = And<basic, emit(extra)>；extra 缺省则 basic。basic 为 Always 且 extra 存在时可简化为 extra。"""
    if extra is None:
        return basic
    ex = emit_formula_ast(extra, where=where)
    if basic == "Always":
        return ex
    return f"formula::And<{basic}, {ex}>"


def item_key_cpp_from_name(name: str, *, where: str) -> str:
    if not isinstance(name, str) or not name.strip():
        raise ValueError(f"{where}: 需要 ItemKey 名字符串")
    n = name.strip()
    if n not in _ITEM_KEYS:
        raise ValueError(f"{where}: 未知 ItemKey：{n}")
    return _item_key_cpp(n)
