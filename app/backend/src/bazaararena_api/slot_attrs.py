from __future__ import annotations

import sqlite3

# 与引擎 job JSON attrsOverride 键名、deck_slots 列名一致
SLOT_ATTR_KEYS = ("custom_0", "custom_1", "custom_2", "custom_3", "quest")


def api_slot_dict_from_row(r: sqlite3.Row) -> dict[str, object]:
    """GET /slots 单条：含可选 attrs_override。"""
    d: dict[str, object] = {
        "position": int(r["position"]),
        "item_name": str(r["item_name"]),
        "tier": int(r["tier"]),
    }
    ao: dict[str, int] = {}
    for k in SLOT_ATTR_KEYS:
        if k in r.keys() and r[k] is not None:
            ao[k] = int(r[k])
    if ao:
        d["attrs_override"] = ao
    return d


def engine_attrs_override_from_slot_dict(slot: dict[str, object]) -> dict[str, int] | None:
    """simulate job 中 items[*].attrsOverride。"""
    ao = slot.get("attrs_override")
    if not isinstance(ao, dict) or not ao:
        return None
    out: dict[str, int] = {}
    for k in SLOT_ATTR_KEYS:
        if k in ao and ao[k] is not None:
            out[k] = int(ao[k])  # type: ignore[arg-type]
    return out if out else None


def parse_attrs_override_from_put_entry(entry: dict[str, object], idx: int) -> tuple[int | None, ...]:
    """PUT body slots[i].attrs_override → 五列可空整数。缺省或 null 表示不覆盖。"""
    ao = entry.get("attrs_override")
    if ao is None:
        return (None, None, None, None, None)
    if not isinstance(ao, dict):
        raise ValueError(f"slots[{idx}].attrs_override must be an object or null")
    allowed = frozenset(SLOT_ATTR_KEYS)
    for k in ao:
        if k not in allowed:
            raise ValueError(f"slots[{idx}].attrs_override unknown key: {k}")
    vals: list[int | None] = []
    for k in SLOT_ATTR_KEYS:
        if k not in ao or ao[k] is None:
            vals.append(None)
        else:
            v = ao[k]
            try:
                vals.append(int(v))
            except (TypeError, ValueError) as e:
                raise ValueError(f"slots[{idx}].attrs_override.{k} must be integer or null") from e
    return (vals[0], vals[1], vals[2], vals[3], vals[4])
