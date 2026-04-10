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


def get_connection() -> sqlite3.Connection:
    path = default_db_path()
    if not path.exists():
        raise FileNotFoundError(f"数据库不存在：{path}（请先运行 python tools/gen_items_sqlite.py）")
    conn = sqlite3.connect(str(path))
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    return conn
