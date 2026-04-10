from __future__ import annotations

import sys
from pathlib import Path


def main() -> None:
    repo_root = Path(__file__).resolve().parents[1]
    sys.path.insert(0, str(repo_root))

    from tools.item_codegen.src.emit_sqlite import emit_sqlite
    from tools.item_codegen.src.parse_yaml import load_all_items

    data_dir = repo_root / "data" / "items"
    out_db = repo_root / "app" / "backend" / "data" / "bazaararena.db"

    items = load_all_items(data_dir)
    emit_sqlite(items, out_db)
    print(f"OK: synced items into {out_db}")


if __name__ == "__main__":
    main()
