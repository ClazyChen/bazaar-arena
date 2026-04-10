from __future__ import annotations

import sys
from pathlib import Path


def main() -> None:
    repo_root = Path(__file__).resolve().parents[1]
    sys.path.insert(0, str(repo_root))

    from tools.item_codegen.src.emit_cpp import emit_cpp_static_data
    from tools.item_codegen.src.parse_yaml import load_all_items

    data_dir = repo_root / "data" / "items"
    out_cpp = repo_root / "engine" / "src" / "bazaararena" / "data" / "items_generated.cpp"

    items = load_all_items(data_dir)
    cpp = emit_cpp_static_data(items)

    out_cpp.parent.mkdir(parents=True, exist_ok=True)
    out_cpp.write_text(cpp, encoding="utf-8", newline="\n")
    print(f"OK: wrote {out_cpp}")


if __name__ == "__main__":
    main()

