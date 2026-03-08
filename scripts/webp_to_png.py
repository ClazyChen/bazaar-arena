#!/usr/bin/env python3
"""
将 pictures/webp/ 下尚未转换的 webp 图片转换为 png，保存到 pictures/png/。
基于 PIL (Pillow)，仅处理目标 png 尚不存在的 webp 文件。
"""

from pathlib import Path

try:
    from PIL import Image
except ImportError:
    raise SystemExit("请先安装 Pillow：pip install Pillow")

# 脚本在 scripts/ 下，仓库根目录为上一级
REPO_ROOT = Path(__file__).resolve().parent.parent
WEBP_DIR = REPO_ROOT / "pictures" / "webp"
PNG_DIR = REPO_ROOT / "pictures" / "png"


def main() -> None:
    if not WEBP_DIR.is_dir():
        print(f"源目录不存在: {WEBP_DIR}")
        return

    PNG_DIR.mkdir(parents=True, exist_ok=True)

    webp_files = list(WEBP_DIR.glob("*.webp"))
    if not webp_files:
        print(f"在 {WEBP_DIR} 下未找到 .webp 文件")
        return

    converted = 0
    skipped = 0

    for webp_path in webp_files:
        png_path = PNG_DIR / (webp_path.stem + ".png")
        if png_path.exists():
            skipped += 1
            continue
        try:
            with Image.open(webp_path) as img:
                # 若存在透明通道则保留，否则转为 RGB
                if img.mode in ("RGBA", "LA", "P"):
                    img = img.convert("RGBA")
                else:
                    img = img.convert("RGB")
                img.save(png_path, "PNG")
            print(f"已转换: {webp_path.name} -> {png_path.name}")
            converted += 1
        except Exception as e:
            print(f"转换失败 {webp_path.name}: {e}")

    print(f"完成：转换 {converted} 个，跳过已存在 {skipped} 个。")


if __name__ == "__main__":
    main()
