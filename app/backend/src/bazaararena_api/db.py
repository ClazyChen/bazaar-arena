from __future__ import annotations

import os
import sqlite3
from pathlib import Path


def repo_root() -> Path:
    # app/backend/src/bazaararena_api/db.py -> parents[4] = repo root
    return Path(__file__).resolve().parents[4]


def default_db_path() -> Path:
    env = os.environ.get("BAZAARARENA_DB")
    if env:
        return Path(env)
    return repo_root() / "app" / "backend" / "data" / "bazaararena.db"


def _migrate_deck_slots_attr_columns(conn: sqlite3.Connection) -> None:
    """与 tools/item_codegen emit_sqlite 一致：为 deck_slots 补全 Custom/Quest 可空列。"""
    cur = conn.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='deck_slots'")
    if cur.fetchone() is None:
        return
    cur = conn.execute("PRAGMA table_info(deck_slots)")
    cols = {str(r[1]) for r in cur.fetchall()}
    for name in ("custom_0", "custom_1", "custom_2", "custom_3", "quest"):
        if name not in cols:
            conn.execute(f"ALTER TABLE deck_slots ADD COLUMN {name} INTEGER")


def get_connection() -> sqlite3.Connection:
    path = default_db_path()
    if not path.exists():
        raise FileNotFoundError(f"数据库不存在：{path}（请先运行 python tools/gen_items_sqlite.py）")
    conn = sqlite3.connect(str(path))
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    _migrate_deck_slots_attr_columns(conn)
    conn.commit()
    return conn
