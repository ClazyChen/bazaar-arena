from __future__ import annotations

import json
import sqlite3
from pathlib import Path

from tools.item_codegen.src.emit_cpp import (
    _as_str,
    _as_str_list,
    _get_any,
    _size_name_to_cpp,
    _tier_name_to_cpp,
)
from tools.item_codegen.src.tooltip_sqlite_data import tooltip_attrs_json_str


def _ensure_schema(conn: sqlite3.Connection) -> None:
    """若表不存在则创建（新建库或旧库缺表时均可）。"""
    conn.executescript(
        """
        CREATE TABLE IF NOT EXISTS items (
            name TEXT PRIMARY KEY,
            hero TEXT NOT NULL DEFAULT '',
            size INTEGER NOT NULL,
            min_tier INTEGER NOT NULL,
            desc TEXT NOT NULL DEFAULT '',
            tags_json TEXT NOT NULL DEFAULT '[]',
            source_yaml TEXT,
            schema_version INTEGER,
            tooltip_attrs_json TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_items_hero ON items(hero);
        CREATE INDEX IF NOT EXISTS idx_items_size ON items(size);
        CREATE INDEX IF NOT EXISTS idx_items_min_tier ON items(min_tier);

        CREATE TABLE IF NOT EXISTS deck_collections (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            sort_order INTEGER NOT NULL DEFAULT 0,
            created_at TEXT
        );

        CREATE TABLE IF NOT EXISTS decks (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            collection_id INTEGER NOT NULL,
            name TEXT NOT NULL,
            player_level INTEGER NOT NULL DEFAULT 5,
            sort_order INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (collection_id) REFERENCES deck_collections(id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS idx_decks_collection ON decks(collection_id);

        CREATE TABLE IF NOT EXISTS deck_slots (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            deck_id INTEGER NOT NULL,
            position INTEGER NOT NULL,
            item_name TEXT NOT NULL,
            tier INTEGER NOT NULL,
            custom_0 INTEGER,
            custom_1 INTEGER,
            custom_2 INTEGER,
            custom_3 INTEGER,
            quest INTEGER,
            FOREIGN KEY (deck_id) REFERENCES decks(id) ON DELETE CASCADE,
            FOREIGN KEY (item_name) REFERENCES items(name),
            UNIQUE(deck_id, position)
        );
        CREATE INDEX IF NOT EXISTS idx_deck_slots_deck ON deck_slots(deck_id);
        """
    )


def _build_item_rows(items: list[dict]) -> list[tuple]:
    rows: list[tuple] = []
    for item in items:
        src = _as_str(item.get("_source_yaml"), default="")
        item_name = _as_str(_get_any(item, "Name", "name", "nameZh", "name_zh"), default="")
        if not item_name:
            raise ValueError(f"{src}: 物品缺少 Name")
        desc = _as_str(_get_any(item, "Desc", "desc"), default="")
        hero = _as_str(item.get("_hero"), default="")
        tags = _as_str_list(_get_any(item, "Tags", "tags"))
        _, size_int = _size_name_to_cpp(_get_any(item, "Size", "size"), src=src)
        _, min_tier_idx = _tier_name_to_cpp(_get_any(item, "Tier", "minTier", "min_tier"), src=src)
        tip = tooltip_attrs_json_str(item, min_tier_idx=min_tier_idx, src=src, item_name=item_name)
        rows.append(
            (
                item_name,
                hero,
                size_int,
                min_tier_idx,
                desc,
                json.dumps(tags, ensure_ascii=False),
                src,
                None,
                tip,
            )
        )
    return rows


def _migrate_items_tooltip_column(conn: sqlite3.Connection) -> None:
    cur = conn.execute("PRAGMA table_info(items)")
    cols = {str(r[1]) for r in cur.fetchall()}
    if "tooltip_attrs_json" not in cols:
        conn.execute("ALTER TABLE items ADD COLUMN tooltip_attrs_json TEXT")


def _migrate_deck_slots_attr_columns(conn: sqlite3.Connection) -> None:
    cur = conn.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='deck_slots'")
    if cur.fetchone() is None:
        return
    cur = conn.execute("PRAGMA table_info(deck_slots)")
    cols = {str(r[1]) for r in cur.fetchall()}
    for name in ("custom_0", "custom_1", "custom_2", "custom_3", "quest"):
        if name not in cols:
            conn.execute(f"ALTER TABLE deck_slots ADD COLUMN {name} INTEGER")


def _sync_items(conn: sqlite3.Connection, rows: list[tuple]) -> None:
    """用 YAML 结果更新 items：UPSERT，并删除既不在 YAML 也不在卡组引用中的物品行。"""
    conn.execute("PRAGMA foreign_keys = ON")
    conn.executemany(
        """
        INSERT INTO items (name, hero, size, min_tier, desc, tags_json, source_yaml, schema_version, tooltip_attrs_json)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(name) DO UPDATE SET
            hero = excluded.hero,
            size = excluded.size,
            min_tier = excluded.min_tier,
            desc = excluded.desc,
            tags_json = excluded.tags_json,
            source_yaml = excluded.source_yaml,
            schema_version = excluded.schema_version,
            tooltip_attrs_json = excluded.tooltip_attrs_json
        """,
        rows,
    )
    yaml_names = [r[0] for r in rows]
    if not yaml_names:
        # 极端情况：无物品定义则仅删除未被卡组引用的行
        conn.execute(
            """
            DELETE FROM items
            WHERE name NOT IN (SELECT DISTINCT item_name FROM deck_slots)
            """
        )
        return

    placeholders = ",".join("?" * len(yaml_names))
    conn.execute(
        f"""
        DELETE FROM items
        WHERE name NOT IN ({placeholders})
          AND name NOT IN (SELECT DISTINCT item_name FROM deck_slots)
        """,
        yaml_names,
    )


def emit_sqlite(items: list[dict], sqlite_path: str | Path) -> None:
    """从 YAML 解析后的 items[] 更新展示用 SQLite。

    - 数据库**不存在**：创建文件与全部表，再写入物品。
    - 数据库**已存在**：只同步 ``items`` 表（UPSERT + 安全删除），**不**清空
      ``deck_collections`` / ``decks`` / ``deck_slots``。
    """
    path = Path(sqlite_path)
    path.parent.mkdir(parents=True, exist_ok=True)

    conn = sqlite3.connect(str(path))
    try:
        conn.execute("PRAGMA foreign_keys = ON")
        _ensure_schema(conn)
        _migrate_items_tooltip_column(conn)
        _migrate_deck_slots_attr_columns(conn)
        rows = _build_item_rows(items)
        _sync_items(conn, rows)
        conn.commit()
    finally:
        conn.close()
