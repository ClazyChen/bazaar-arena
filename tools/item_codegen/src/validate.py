from __future__ import annotations

from pathlib import Path


def main() -> None:
    data_dir = Path(__file__).resolve().parents[3] / "data" / "items"
    yamls = sorted(data_dir.glob("*.yaml"))
    if not yamls:
        raise SystemExit(f"未找到 YAML：{data_dir}")
    print(f"OK: 找到 {len(yamls)} 个 YAML（占位：尚未做 schema 校验）")


if __name__ == "__main__":
    main()

