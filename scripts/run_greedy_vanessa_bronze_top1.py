#!/usr/bin/env python3
import argparse
import re
import subprocess
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="对海盗物品批量运行锚定贪心搜索并汇总 top1。"
    )
    parser.add_argument(
        "--output",
        default=None,
        help="汇总输出文件（每行 [锚定物品]=[卡组]）。默认 docs/greedy-vanessa-bronze-top1-l<level>.txt（含等级后缀）。",
    )
    parser.add_argument(
        "--raw-log",
        default=None,
        help="Greedy 原始控制台输出日志文件（完整 tee 落盘）。默认 Logs/greedy/greedy-vanessa-bronze-top1-l<level>-raw.log。",
    )
    parser.add_argument(
        "--level",
        type=int,
        choices=[2, 3, 4, 5, 6],
        default=2,
        help="传给 Greedy 的 --level（2：6 槽半 overrides；3：8 槽铜档；4：10 槽铜银平均 overrides；5：10 槽银档；6：10 槽银金平均 overrides）。",
    )
    parser.add_argument("--top-k", type=int, default=20, help="Greedy 的 K。")
    parser.add_argument("--top-multiplier", type=int, default=2, help="Greedy 的 M。")
    parser.add_argument("--workers", type=int, default=8, help="Greedy 的 workers。")
    parser.add_argument(
        "--project",
        default="src/BazaarArena.GreedyDeckFinder/BazaarArena.GreedyDeckFinder.csproj",
        help="GreedyDeckFinder csproj 路径。",
    )
    parser.add_argument(
        "--keep-tmp",
        action="store_true",
        help="保留每个锚定物品的临时输出文件。",
    )
    return parser.parse_args()


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def supports_color() -> bool:
    return sys.stdout.isatty()


def color_text(text: str, code: str) -> str:
    if not supports_color():
        return text
    return f"\033[{code}m{text}\033[0m"


def info(text: str) -> None:
    print(color_text(text, "36"))


def ok(text: str) -> None:
    print(color_text(text, "32"))


def parse_registered_class_names(vanessa_register_file: Path) -> list[str]:
    text = vanessa_register_file.read_text(encoding="utf-8")
    def extract_block(tier: str) -> str:
        m = re.search(
            rf"DefaultMinTier\s*=\s*ItemTier\.{tier};(?P<body>[\s\S]*?)(?:DefaultMinTier\s*=\s*ItemTier\.\w+;|}}\s*$)",
            text,
        )
        if not m:
            raise RuntimeError(f"无法在文件中找到 {tier} 注册段：{vanessa_register_file}")
        return m.group("body")

    class_names: list[str] = []
    for tier in ("Bronze",):
        class_names.extend(re.findall(r"db\.Register\((\w+)\.Template\(\)\);", extract_block(tier)))
    return class_names


def parse_registered_class_names_for_tiers(vanessa_register_file: Path, tiers: list[str]) -> list[str]:
    text = vanessa_register_file.read_text(encoding="utf-8")

    def extract_block(tier: str) -> str:
        m = re.search(
            rf"DefaultMinTier\s*=\s*ItemTier\.{tier};(?P<body>[\s\S]*?)(?:DefaultMinTier\s*=\s*ItemTier\.\w+;|}}\s*$)",
            text,
        )
        if not m:
            raise RuntimeError(f"无法在文件中找到 {tier} 注册段：{vanessa_register_file}")
        return m.group("body")

    class_names: list[str] = []
    for tier in tiers:
        class_names.extend(re.findall(r"db\.Register\((\w+)\.Template\(\)\);", extract_block(tier)))
    return class_names


def parse_template_name(template_file: Path) -> str:
    text = template_file.read_text(encoding="utf-8")
    m = re.search(r'Name\s*=\s*"([^"]+)"', text)
    if not m:
        raise RuntimeError(f"无法解析 Name：{template_file}")
    return m.group(1)


def collect_anchor_items(root: Path, player_level: int) -> list[str]:
    class_name_to_file: dict[str, Path] = {}
    for p in (root / "src" / "BazaarArena" / "ItemDatabase" / "Vanessa").rglob("*.cs"):
        class_name_to_file[p.stem] = p

    register_files = [
        root / "src" / "BazaarArena" / "ItemDatabase" / "Vanessa" / "small" / "VanessaSmall.cs",
        root / "src" / "BazaarArena" / "ItemDatabase" / "Vanessa" / "medium" / "VanessaMedium.cs",
        root / "src" / "BazaarArena" / "ItemDatabase" / "Vanessa" / "large" / "VanessaLarge.cs",
    ]

    names: list[str] = []
    for register_file in register_files:
        if player_level >= 5:
            class_names = parse_registered_class_names_for_tiers(register_file, ["Bronze", "Silver"])
        else:
            class_names = parse_registered_class_names(register_file)
        for class_name in class_names:
            file_path = class_name_to_file.get(class_name)
            if file_path is None:
                raise RuntimeError(f"找不到类文件：{class_name}")
            names.append(parse_template_name(file_path))

    # 特殊：Greedy lv5 会把烙刀展开为 Q1/Q2 两个锚定物品（输出需区分）。
    if "烙刀" in names:
        names = [n for n in names if n != "烙刀"]
        names.extend(["烙刀（Q1）", "烙刀（Q2）"])

    if player_level < 5 and len(names) != 52:
        raise RuntimeError(f"海盗铜物品数量不是 52，而是 {len(names)}。")
    return names


def max_slots_for_player_level(level: int) -> int:
    """与 BazaarArena.Core.Deck.MaxSlotsForLevel 一致（脚本使用 2–5）。"""
    return {2: 6, 3: 8, 4: 10, 5: 10, 6: 10}[level]


def collect_burn_tag_items(root: Path) -> set[str]:
    burn_names: set[str] = set()
    for p in (root / "src" / "BazaarArena" / "ItemDatabase" / "Vanessa").rglob("*.cs"):
        if p.name in {"VanessaSmall.cs", "VanessaMedium.cs", "VanessaLarge.cs"}:
            continue
        text = p.read_text(encoding="utf-8")
        if "Ability.Burn" not in text:
            continue
        burn_names.add(parse_template_name(p))
    return burn_names


def build_excluded_items(anchor: str, burn_items: set[str]) -> list[str]:
    if anchor in {"烙刀（Q1）", "烙刀（Q2）"}:
        anchor = "烙刀"
    excluded: list[str] = []
    if anchor != "舱底蠕虫":
        excluded.append("舱底蠕虫")
    if anchor != "棉鳚" and anchor not in burn_items:
        excluded.append("棉鳚")
    return excluded


def parse_top1_deck_from_output(output_text: str, max_size: int) -> str:
    block = re.search(
        rf"size={max_size}\s*(?P<body>[\s\S]*?)(?:\nsize=|\Z)",
        output_text,
    )
    if not block:
        raise RuntimeError(f"输出中不存在 size={max_size} 段。")
    body = block.group("body")
    rep = re.search(r"Rep=\[(.*?)\]", body)
    if not rep:
        raise RuntimeError(f"输出中不存在 size={max_size} top1 的 Rep。")
    return rep.group(1).strip()


def run_one(
    root: Path,
    project: str,
    anchor: str,
    excluded_items: list[str],
    top_k: int,
    top_multiplier: int,
    workers: int,
    player_level: int,
    deck_max_size: int,
    tmp_out_path: Path,
    raw_log_file,
) -> str:
    cmd = [
        "dotnet",
        "run",
        "--project",
        project,
        "--",
        "--anchor-item",
        anchor,
        "--level",
        str(player_level),
        "--top-k",
        str(top_k),
        "--top-multiplier",
        str(top_multiplier),
        "--workers",
        str(workers),
        "--output",
        str(tmp_out_path),
    ]
    for name in excluded_items:
        cmd.extend(["--exclude-item", name])

    # 保留原有 Greedy 控制台输出，同时写入 raw_log_file（tee）。
    proc = subprocess.Popen(
        cmd,
        cwd=root,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
    )
    assert proc.stdout is not None
    for line in proc.stdout:
        print(line, end="")
        raw_log_file.write(line)
    ret = proc.wait()
    if ret != 0:
        raise RuntimeError(f"锚定 [{anchor}] 运行失败，exit={ret}")

    output_text = tmp_out_path.read_text(encoding="utf-8")
    return parse_top1_deck_from_output(output_text, deck_max_size)


def main() -> int:
    args = parse_args()
    root = repo_root()
    level = args.level
    out_rel = args.output or f"docs/greedy-vanessa-bronze-top1-l{level}.txt"
    raw_rel = args.raw_log or f"Logs/greedy/greedy-vanessa-bronze-top1-l{level}-raw.log"
    marker = f"l{level}"
    if args.output is not None and marker not in Path(out_rel).name:
        print(
            f"[ERROR] 自定义 --output 的文件名须包含「{marker}」以区分玩家等级。",
            file=sys.stderr,
        )
        return 2
    if args.raw_log is not None and marker not in Path(raw_rel).name:
        print(
            f"[ERROR] 自定义 --raw-log 的文件名须包含「{marker}」以区分玩家等级。",
            file=sys.stderr,
        )
        return 2
    output_path = (root / out_rel).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    raw_log_path = (root / raw_rel).resolve()
    raw_log_path.parent.mkdir(parents=True, exist_ok=True)
    tmp_dir = output_path.parent / f".tmp_greedy_vanessa_top1_l{level}"
    tmp_dir.mkdir(parents=True, exist_ok=True)
    max_size = max_slots_for_player_level(level)

    anchors = collect_anchor_items(root, level)
    burn_items = collect_burn_tag_items(root)

    rows: list[str] = []
    with raw_log_path.open("w", encoding="utf-8") as raw_log_file:
        raw_log_file.write(
            f"# Greedy Vanessa Bronze Top1 Raw Log\n"
            f"# level={level}, top_k={args.top_k}, top_multiplier={args.top_multiplier}, workers={args.workers}\n"
            f"# anchors={len(anchors)}, max_size={max_size}\n\n"
        )
        for i, anchor in enumerate(anchors, start=1):
            excluded = build_excluded_items(anchor, burn_items)
            tmp_out = tmp_dir / f"{i:02d}_{anchor}.txt"
            info(f"[{i}/{len(anchors)}] 锚定={anchor} 排除={excluded}")
            raw_log_file.write(f"\n===== [{i}/{len(anchors)}] 锚定={anchor} 排除={excluded} =====\n")
            top1_deck = run_one(
                root=root,
                project=args.project,
                anchor=anchor,
                excluded_items=excluded,
                top_k=args.top_k,
                top_multiplier=args.top_multiplier,
                workers=args.workers,
                player_level=level,
                deck_max_size=max_size,
                tmp_out_path=tmp_out,
                raw_log_file=raw_log_file,
            )
            rows.append(f"{anchor}={top1_deck}")

    output_path.write_text("\n".join(rows) + "\n", encoding="utf-8")
    ok(f"完成：level={level}，已写入 {output_path}")
    ok(f"原始日志：{raw_log_path}")

    if not args.keep_tmp:
        for p in tmp_dir.glob("*.txt"):
            p.unlink(missing_ok=True)
        tmp_dir.rmdir()
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as ex:
        print(f"[ERROR] {ex}", file=sys.stderr)
        raise
