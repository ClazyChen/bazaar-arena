#!/usr/bin/env python3
import argparse
import re
import subprocess
import sys
from pathlib import Path


UNIMPLEMENTED_ITEM_NAMES = {"催化剂", "产药药水"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="对 Mak 物品批量运行锚定贪心搜索并汇总 top1（忽略未实现物品）。"
    )
    parser.add_argument(
        "--anchor-item",
        default=None,
        help="仅对指定锚定物品运行一次（覆盖批量锚定模式）。与 --seed-items 互斥。",
    )
    parser.add_argument(
        "--seed-items",
        default=None,
        help="指定有序起始卡组（CSV，如：A,B,C），传给 Greedy 的 --seed-items；覆盖批量锚定模式。与 --anchor-item 互斥。",
    )
    parser.add_argument(
        "--output",
        default=None,
        help="汇总输出文件（每行 [锚定物品]=[卡组]）。默认 docs/greedy-mak-bronze-top1-l<level>.txt（含等级后缀）。",
    )
    parser.add_argument(
        "--raw-log",
        default=None,
        help="Greedy 原始控制台输出日志文件（完整 tee 落盘）。默认 Logs/greedy/greedy-mak-bronze-top1-l<level>-raw.log。",
    )
    parser.add_argument(
        "--level",
        type=int,
        default=2,
        help="传给 Greedy 的 --level（2–20）。槽位与 Core.Deck.MaxSlotsForLevel 一致；池子/档位/overridable 见 GreedyLevelRules。",
    )
    parser.add_argument("--top-k", type=int, default=20, help="Greedy 的 K。")
    parser.add_argument("--top-multiplier", type=int, default=2, help="Greedy 的 M。")
    parser.add_argument("--workers", type=int, default=8, help="Greedy 的 workers。")
    parser.add_argument(
        "--exclude-item",
        default=None,
        help="额外排除物品（CSV）。脚本仍会强制排除未实现物品：催化剂、产药药水。",
    )
    parser.add_argument(
        "--project",
        default="src/BazaarArena.GreedyDeckFinder/BazaarArena.GreedyDeckFinder.csproj",
        help="GreedyDeckFinder csproj 路径。",
    )
    parser.add_argument(
        "--pool-hero",
        default="Mak",
        help="传给 Greedy 的 --pool-hero（默认 Mak，确保只在 Mak 物品池内搜索）。",
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


def parse_registered_class_names_for_tiers(register_file: Path, tiers: list[str]) -> list[str]:
    text = register_file.read_text(encoding="utf-8")

    def extract_block(tier: str) -> str:
        m = re.search(
            rf"DefaultMinTier\s*=\s*ItemTier\.{tier};(?P<body>[\s\S]*?)(?:DefaultMinTier\s*=\s*ItemTier\.\w+;|}}\s*$)",
            text,
        )
        if not m:
            raise RuntimeError(f"无法在文件中找到 {tier} 注册段：{register_file}")
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


def register_tiers_for_player_level(player_level: int) -> list[str]:
    """与 GreedyLevelRules.IsMinTierAllowedInPool 一致：铜恒在；≥5 银；≥8 金；≥11 钻。"""
    tiers = ["Bronze"]
    if player_level >= 5:
        tiers.append("Silver")
    if player_level >= 8:
        tiers.append("Gold")
    if player_level >= 11:
        tiers.append("Diamond")
    return tiers


def collect_anchor_items(root: Path, player_level: int) -> list[str]:
    class_name_to_file: dict[str, Path] = {}
    for p in (root / "src" / "BazaarArena" / "ItemDatabase" / "Mak").rglob("*.cs"):
        class_name_to_file[p.stem] = p

    register_files = [
        root / "src" / "BazaarArena" / "ItemDatabase" / "Mak" / "small" / "MakSmall.cs",
        root / "src" / "BazaarArena" / "ItemDatabase" / "Mak" / "medium" / "MakMedium.cs",
    ]

    tier_list = register_tiers_for_player_level(player_level)
    names: list[str] = []
    for register_file in register_files:
        class_names = parse_registered_class_names_for_tiers(register_file, tier_list)
        for class_name in class_names:
            file_path = class_name_to_file.get(class_name)
            if file_path is None:
                raise RuntimeError(f"找不到类文件：{class_name}")
            names.append(parse_template_name(file_path))

    # 忽略未实现物品：它们不应作为锚定目标，也不应进入搜索池。
    names = [n for n in names if n not in UNIMPLEMENTED_ITEM_NAMES]
    return names


def max_slots_for_player_level(level: int) -> int:
    """与 BazaarArena.Core.Deck.MaxSlotsForLevel 一致。"""
    if level <= 1:
        return 4
    if level == 2:
        return 6
    if level == 3:
        return 8
    return 10


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


def split_csv(s: str | None) -> list[str]:
    if not s:
        return []
    parts = []
    for x in s.split(","):
        t = x.strip()
        if t:
            parts.append(t)
    return parts


def sanitize_filename_part(name: str) -> str:
    # Windows 文件名禁止：<>:"/\|?*，同时避免尾部句点/空格。
    # 这里只用于临时文件名，保持可读性即可。
    bad = '<>:"/\\|?*'
    out = "".join("_" if ch in bad else ch for ch in (name or ""))
    out = out.strip().strip(".")
    return out or "_"


def run_one(
    root: Path,
    project: str,
    anchor: str | None,
    seed_items_csv: str | None,
    pool_hero: str,
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
        "--pool-hero",
        pool_hero,
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
    if seed_items_csv is not None:
        cmd.extend(["--seed-items", seed_items_csv])
    else:
        assert anchor is not None
        cmd.extend(["--anchor-item", anchor])
    for name in excluded_items:
        cmd.extend(["--exclude-item", name])

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
        label = anchor if anchor is not None else "<seed-items>"
        raise RuntimeError(f"锚定 [{label}] 运行失败，exit={ret}")

    output_text = tmp_out_path.read_text(encoding="utf-8")
    return parse_top1_deck_from_output(output_text, deck_max_size)


def main() -> int:
    args = parse_args()
    root = repo_root()
    level = args.level
    if level < 2 or level > 20:
        print("[ERROR] --level 须在 2～20 之间。", file=sys.stderr)
        return 2

    max_size = max_slots_for_player_level(level)
    anchor_item = (args.anchor_item or "").strip() if args.anchor_item is not None else ""
    seed_items_csv = (args.seed_items or "").strip() if args.seed_items is not None else ""
    if anchor_item and seed_items_csv:
        print("[ERROR] --anchor-item 与 --seed-items 互斥。", file=sys.stderr)
        return 2

    anchors: list[str]
    run_mode: str
    if seed_items_csv:
        anchors = ["<seed-items>"]
        run_mode = "seed-items"
    elif anchor_item:
        anchors = [anchor_item]
        run_mode = "anchor-item"
    else:
        anchors = collect_anchor_items(root, level)
        run_mode = "batch-anchors"

    raw_rel = args.raw_log or f"Logs/greedy/greedy-mak-bronze-top1-l{level}-raw.log"
    marker = f"l{level}"
    if args.raw_log is not None and marker not in Path(raw_rel).name:
        print(
            f"[ERROR] 自定义 --raw-log 的文件名须包含「{marker}」以区分玩家等级。",
            file=sys.stderr,
        )
        return 2
    raw_log_path = (root / raw_rel).resolve()
    raw_log_path.parent.mkdir(parents=True, exist_ok=True)

    # 临时目录：不依赖汇总输出路径，避免 seed/anchor 模式也强制创建 docs/ 文件夹。
    tmp_dir = (root / "Logs" / "greedy" / f".tmp_greedy_mak_top1_l{level}").resolve()
    tmp_dir.mkdir(parents=True, exist_ok=True)

    # 仅批量锚定模式会写汇总输出文件；指定起始卡组/锚定时只看控制台结果，不覆盖汇总文件。
    output_path: Path | None = None
    if run_mode == "batch-anchors":
        out_rel = args.output or f"docs/greedy-mak-bronze-top1-l{level}.txt"
        if args.output is not None and marker not in Path(out_rel).name:
            print(
                f"[ERROR] 自定义 --output 的文件名须包含「{marker}」以区分玩家等级。",
                file=sys.stderr,
            )
            return 2
        output_path = (root / out_rel).resolve()
        output_path.parent.mkdir(parents=True, exist_ok=True)

    # Greedy 计算过程中忽略未实现物品：对所有锚定统一排除。
    extra_excluded = split_csv(args.exclude_item)
    always_excluded = sorted(set(UNIMPLEMENTED_ITEM_NAMES).union(extra_excluded))
    pool_hero = (args.pool_hero or "Mak").strip() or "Mak"

    rows: list[str] = []
    with raw_log_path.open("w", encoding="utf-8") as raw_log_file:
        raw_log_file.write(
            f"# Greedy Mak Bronze Top1 Raw Log\n"
            f"# pool_hero={pool_hero}\n"
            f"# level={level}, top_k={args.top_k}, top_multiplier={args.top_multiplier}, workers={args.workers}\n"
            f"# run_mode={run_mode}, anchors={len(anchors)}, max_size={max_size}\n"
            f"# always_excluded={always_excluded}\n\n"
        )
        for i, anchor in enumerate(anchors, start=1):
            tmp_name = sanitize_filename_part(anchor)
            tmp_out = tmp_dir / f"{i:02d}_{tmp_name}.txt"
            info(f"[{i}/{len(anchors)}] 锚定={anchor} 排除={always_excluded}")
            raw_log_file.write(f"\n===== [{i}/{len(anchors)}] 锚定={anchor} 排除={always_excluded} =====\n")
            top1_deck = run_one(
                root=root,
                project=args.project,
                anchor=None if seed_items_csv else anchor,
                seed_items_csv=seed_items_csv if seed_items_csv else None,
                pool_hero=pool_hero,
                excluded_items=always_excluded,
                top_k=args.top_k,
                top_multiplier=args.top_multiplier,
                workers=args.workers,
                player_level=level,
                deck_max_size=max_size,
                tmp_out_path=tmp_out,
                raw_log_file=raw_log_file,
            )
            if seed_items_csv:
                rows.append(f"{seed_items_csv}={top1_deck}")
            else:
                rows.append(f"{anchor}={top1_deck}")

    if output_path is not None:
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

