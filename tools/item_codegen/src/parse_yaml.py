from __future__ import annotations

from pathlib import Path


def load_all_items(data_dir: Path, *, only_yaml: str | None = None) -> list[dict]:
    import yaml

    if only_yaml is not None:
        yamls = [data_dir / only_yaml]
    else:
        yamls = sorted(data_dir.glob("*.yaml"))
    if not yamls:
        raise ValueError(f"未找到 YAML：{data_dir}")

    items: list[dict] = []
    for p in yamls:
        if not p.exists():
            raise ValueError(f"未找到 YAML：{p}")
        doc = yaml.safe_load(p.read_text(encoding="utf-8"))
        if not isinstance(doc, dict):
            raise ValueError(f"{p}: 顶层必须是 object")
        if "items" not in doc or not isinstance(doc["items"], list):
            raise ValueError(f"{p}: 缺少 items[]")

        hero = doc.get("hero", "")
        schema_version = doc.get("schemaVersion", 0)
        if not isinstance(schema_version, int) or schema_version < 1:
            raise ValueError(f"{p}: schemaVersion 必须 >= 1")
        if not isinstance(hero, str):
            raise ValueError(f"{p}: hero 必须是 string")

        for idx, item in enumerate(doc["items"]):
            if not isinstance(item, dict):
                raise ValueError(f"{p}: items[{idx}] 必须是 object")
            item = dict(item)
            item["_source_yaml"] = str(p.as_posix())
            item["_hero"] = hero
            items.append(item)

    return items

