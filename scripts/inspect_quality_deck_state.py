# 读取优质卡组探测器保存的状态 JSON，输出探测效果摘要（赛季数、总对局、池大小、Top 卡组等）。
# 用法：python scripts/inspect_quality_deck_state.py [状态文件路径]
# 默认状态文件：quality_deck_state.json（仓库根或当前目录）

import json
import sys
from pathlib import Path


def find_state_path(path_arg: str | None) -> Path:
    if path_arg:
        p = Path(path_arg)
        if p.is_absolute():
            return p
        # 相对路径：优先仓库根
        root = Path(__file__).resolve().parent.parent
        for base in (root, Path.cwd()):
            candidate = base / path_arg
            if candidate.exists():
                return candidate
        return root / path_arg
    root = Path(__file__).resolve().parent.parent
    for name in ("quality_deck_state.json", "Data/quality_deck_state.json"):
        candidate = root / name
        if candidate.exists():
            return candidate
    return root / "quality_deck_state.json"


def main() -> None:
    path_arg = sys.argv[1] if len(sys.argv) > 1 else None
    path = find_state_path(path_arg)
    if not path.exists():
        print(f"状态文件不存在: {path}", file=sys.stderr)
        sys.exit(1)

    with open(path, "r", encoding="utf-8") as f:
        dto = json.load(f)

    season = dto.get("CurrentSeason", 0)
    total_games = dto.get("TotalGames", 0)
    summary = dto.get("Summary") or {}
    pool_size = summary.get("PoolSize", 0)
    max_elo = summary.get("MaxElo", 0)
    min_elo = summary.get("MinElo", 0)
    confirmed = summary.get("ConfirmedCount", 0)
    local_optima = summary.get("LocalOptimaCount", 0)
    combos = dto.get("Combos") or []
    strength_sigs = dto.get("StrengthPlayerComboSigs") or []
    anchored = dto.get("AnchoredPlayers") or []

    print("======== 优质卡组探测器 · 状态摘要 ========")
    print(f"状态文件: {path}")
    print(f"当前赛季: {season}")
    print(f"总对局数: {total_games}")
    print(f"池内卡组数: {pool_size}  (已确认: {confirmed}, 局部最优: {local_optima})")
    print(f"ELO 范围: [{min_elo:.1f}, {max_elo:.1f}]")
    print(f"强度玩家数: {len(strength_sigs)}, 锚定玩家数: {len(anchored)}")
    print()

    # Top 10 按 ELO 排序（与强度玩家取并：展示当前最强卡组）
    by_elo = sorted(combos, key=lambda c: (c.get("Elo") or 0), reverse=True)
    top_n = 10
    print(f"======== Top {top_n} 卡组（按 ELO） ========")
    for i, c in enumerate(by_elo[:top_n], 1):
        elo = c.get("Elo", 0)
        games = c.get("GameCount", 0)
        items = c.get("RepresentativeItems") or []
        confirmed_mark = " [已确认]" if c.get("IsConfirmed") else ""
        opt_mark = " [局部最优]" if c.get("IsLocalOptimum") else ""
        print(f"  {i}. ELO={elo:.1f}  对局={games}{confirmed_mark}{opt_mark}")
        print(f"      {items}")
    print()

    # 若强度玩家与 Top 有差异，可再列当前强度玩家持有的卡组 ELO
    if strength_sigs:
        sig_to_combo = {c.get("ComboSig"): c for c in combos if c.get("ComboSig")}
        strength_elos = []
        for sig in strength_sigs:
            c = sig_to_combo.get(sig)
            if c is not None:
                strength_elos.append((c.get("Elo", 0), c.get("GameCount", 0), c.get("RepresentativeItems") or []))
        strength_elos.sort(key=lambda x: x[0], reverse=True)
        print("======== 强度玩家当前卡组（按 ELO）======== ")
        for i, (elo, games, items) in enumerate(strength_elos[:10], 1):
            print(f"  {i}. ELO={elo:.1f}  对局={games}")
            print(f"      {items}")
    print("======== 结束 ========")


if __name__ == "__main__":
    main()
