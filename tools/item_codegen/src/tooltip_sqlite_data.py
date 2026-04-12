from __future__ import annotations

import json
import re
from typing import Any

from tools.item_codegen.src.formula_emit import ITEM_KEY_NAMES

_DURATION_SUFFIX_RE = re.compile(r"^(\d+(?:\.\d+)?)(?:s|_s)$")

_YAML_FIELD_ALIASES: dict[str, str] = {
    "cooldown": "Cooldown",
    "cooldownSeconds": "Cooldown",
    "ModifyTargetCount": "ModifyAttributeTargetCount",
}

# 与引擎 ItemKey 一致：YAML 中秒字面量写入为毫秒
_DURATION_STORAGE_KEYS = frozenset({"Cooldown", "Haste", "Slow", "Freeze", "Charge"})

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


def _canonical_item_field_key(yaml_key: str) -> str:
    k = yaml_key.strip()
    return _YAML_FIELD_ALIASES.get(k, k)


def _iter_tier_values_from_min(
    vals: list[object], min_tier: int, *, where: str
) -> list[tuple[int, object]]:
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


def _raw_to_storage_number(canon: str, raw: object, *, where: str) -> float:
    if isinstance(raw, bool):
        raise TypeError(f"{where}: 不能为 bool")
    if canon in _DURATION_STORAGE_KEYS:
        if isinstance(raw, str):
            s = raw.strip()
            m = _DURATION_SUFFIX_RE.match(s)
            if m:
                return float(m.group(1)) * 1000.0
            raise ValueError(f"{where}: 期望时长字符串（如 3s），实际 {raw!r}")
        if isinstance(raw, int):
            return float(raw)
        if isinstance(raw, float):
            return float(raw)
        raise TypeError(f"{where}: 不支持的类型 {type(raw)}")
    if isinstance(raw, int):
        return float(raw)
    if isinstance(raw, float):
        return float(raw)
    if isinstance(raw, str):
        m = _DURATION_SUFFIX_RE.match(raw.strip())
        if m:
            return float(m.group(1)) * 1000.0
        raise ValueError(f"{where}: 期望数字或时长字符串，实际 {raw!r}")
    raise TypeError(f"{where}: 不支持的类型 {type(raw)}")


def build_tooltip_attrs(item: dict, *, min_tier_idx: int, src: str, item_name: str) -> dict[str, list[float]]:
    """每个 ItemKey → 长度 5 的列表（tier 0..4），时长类为毫秒。"""
    out: dict[str, list[float]] = {}
    keys = sorted(k for k in item.keys() if k not in _YAML_STRUCT_SPECIAL_KEYS)
    for yk in keys:
        canon = _canonical_item_field_key(yk)
        if canon not in ITEM_KEY_NAMES:
            continue
        val = item[yk]
        where_f = f"{src} {item_name}.{canon}"
        row = [0.0] * 5
        if isinstance(val, list):
            pairs = _iter_tier_values_from_min(val, min_tier_idx, where=where_f)
            for tidx, raw in pairs:
                row[tidx] = _raw_to_storage_number(canon, raw, where=f"{where_f}[tier={tidx}]")
        else:
            v = _raw_to_storage_number(canon, val, where=where_f)
            for i in range(5):
                row[i] = v
        out[canon] = row
    return out


def tooltip_attrs_json_str(item: dict, *, min_tier_idx: int, src: str, item_name: str) -> str:
    data = build_tooltip_attrs(item, min_tier_idx=min_tier_idx, src=src, item_name=item_name)
    return json.dumps(data, ensure_ascii=False, separators=(",", ":"))
