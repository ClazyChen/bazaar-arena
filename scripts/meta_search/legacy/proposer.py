# -*- coding: utf-8 -*-
"""候选提议器：理由图挖掘引擎 × 填充位（主力）+ GDF 枚举 topk（辅助）。

- reason_engine_candidates：挖掘引擎闭包（C++ --mine-engines）× elite 先验填充
  → 装配检查（魂石全家族互斥/图书馆无武器/天平居中）→ 代表布局；
- gdf_topk_candidates：GDF 锚点枚举 topk 抽样（魂石互斥过滤）；
- filler_combos/item_size：尺寸预算组合工具。
"""

from __future__ import annotations

import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent.parent))
from meta_search.legacy.templates import Template, check_assembly, layout  # noqa: E402

SIZES = {"Small": 1, "Medium": 2, "Large": 3}


def item_size(db: dict[str, dict], name: str) -> int:
    return SIZES.get(db.get(name, {}).get("Size", ""), 1)


def filler_combos(db: dict[str, dict], fillers: list[str], budget: int,
                  max_copies: int = 2) -> list[list[str]]:
    """生成总尺寸恰为 budget 的填充 multiset 列表（按 filler 字典序去重）。"""
    pool = []
    for f in fillers:
        sz = item_size(db, f)
        for c in range(1, max_copies + 1):
            pool.append((f, sz, c))
    out: list[list[str]] = []
    seen = set()

    def rec(start: int, remaining: int, acc: list[str]):
        if remaining == 0:
            key = tuple(sorted(acc))
            if key not in seen:
                seen.add(key)
                out.append(list(acc))
            return
        for i in range(start, len(pool)):
            f, sz, cmax = pool[i]
            cnt = acc.count(f)
            if cnt >= cmax or sz > remaining:
                continue
            acc.append(f)
            rec(i, remaining - sz, acc)
            acc.pop()

    rec(0, budget, [])
    return out


def reason_engine_candidates(
    g,
    db: dict[str, dict],
    *,
    top_engines: int = 60,
    fill_variants: int = 3,
    slots: int = 10,
    seed: int = 7,
    max_members: int = 4,
) -> list[dict]:
    """理由图挖掘引擎提议器（v3 主力）。

    流程：mine_engines_v2（核心播种+数值频率加权+两级配额）取 top_engines 个引擎闭包
    → 每个引擎按尺寸预算采样 fill_variants 个填充组合（elite 软先验）
    → 装配检查（魂石全家族互斥/图书馆无武器/天平居中）→ 代表布局。
    """
    from meta_search.legacy.templates import FILLERS, Template, check_assembly, layout

    rng = random.Random(seed)
    engines = _cached_engines(g, max_members)[:top_engines]
    candidates: list[dict] = []
    for sc, engine in engines:
        core = list(engine)
        core_size = sum(item_size(db, n) for n in core)
        budget = slots - core_size
        if budget < 0:
            continue
        fillers = [f for f in FILLERS if f not in core and f in db]
        combos = filler_combos(db, fillers, budget)
        rng.shuffle(combos)
        tmpl = Template(
            name=f"rg:{'+'.join(sorted(core))}",
            core=core,
            center_item="天平" if "天平" in core else None,
            no_weapon="图书馆" in core,
        )
        picked = 0
        for combo in combos:
            items = core + combo
            if check_assembly(items, tmpl, db) is not None:
                continue
            # 基底 魂石 在战斗中无任务能力（quest=0）；映射为精英层最常用的
            # 灼烧减速 变体（Q2+Q3），使候选与 GDF 条件一致（当前最佳变体映射，
            # 后续可按流派分配变体）
            items = ["灼烧减速魂石" if n == "魂石" else n for n in items]
            sig = layout(items, tmpl, db)
            candidates.append({
                "template": tmpl.name,
                "signature": ",".join(sig),
                "engine_score": round(sc, 2),
            })
            picked += 1
            if picked >= fill_variants:
                break
    return candidates


_ENGINES_CACHE: dict[tuple, list] = {}

_META_EXE = Path(__file__).resolve().parent.parent.parent.parent / "bin" / (
    "bazaararena_meta.exe" if sys.platform == "win32" else "bazaararena_meta")
_PROFILES_JSON = Path(__file__).resolve().parent.parent.parent.parent / "out" / "meta_search" / "reason_profiles.json"
_ENGINES_JSON = Path(__file__).resolve().parent.parent.parent.parent / "out" / "meta_search" / "engines_cpp.json"


def _ensure_profiles_json() -> None:
    """画像 JSON 过期（或缺失）时用 Python 提取器重导（提取规则唯一事实来源在 reason_graph.py）。"""
    yaml_path = Path(__file__).resolve().parent.parent.parent.parent / "data" / "items" / "mak.yaml"
    if _PROFILES_JSON.exists() and _PROFILES_JSON.stat().st_mtime >= yaml_path.stat().st_mtime:
        return
    from meta_search.legacy.reason_graph import export_profiles_json, load

    g = load(yaml_path.parent, "mak")
    export_profiles_json(g, _PROFILES_JSON)


def _cached_engines(g, max_members: int) -> list:
    """挖掘结果缓存（进程内 + 磁盘）：优先 C++ 挖掘（~30s），
    bazaararena_meta 缺失时回退 Python（~15min）。磁盘缓存按 mak.yaml mtime 失效。"""
    key = (id(g), max_members)
    if key in _ENGINES_CACHE:
        return _ENGINES_CACHE[key]
    import json
    import subprocess

    yaml_path = Path(__file__).resolve().parent.parent.parent.parent / "data" / "items" / "mak.yaml"
    cache_path = Path(__file__).resolve().parent.parent.parent.parent / "out" / "meta_search" / "mined_engines_v2.json"
    if cache_path.exists():
        try:
            doc = json.loads(cache_path.read_text(encoding="utf-8"))
            if doc.get("yaml_mtime") == yaml_path.stat().st_mtime and \
               doc.get("max_members") == max_members:
                out = [(sc, tuple(sub)) for sc, sub in doc["engines"]]
                _ENGINES_CACHE[key] = out
                return out
        except Exception:
            pass

    out = None
    if _META_EXE.exists():
        try:
            _ensure_profiles_json()
            proc = subprocess.run(
                [str(_META_EXE), "--mine-engines", "--profiles", str(_PROFILES_JSON),
                 "--max-members", str(max_members), "--output", str(_ENGINES_JSON)],
                capture_output=True, text=True, encoding="utf-8", errors="replace",
                cwd=str(Path(__file__).resolve().parent.parent.parent.parent),
            )
            if proc.returncode == 0:
                engines = json.loads(_ENGINES_JSON.read_text(encoding="utf-8"))
                out = [(e["score"], tuple(e["members"])) for e in engines]
        except Exception:
            out = None
    if out is None:
        from meta_search.legacy.mine_engines import mine_engines_v2

        out = mine_engines_v2(g, max_members=max_members, family_quota=3, channel_quota=8)
    cache_path.parent.mkdir(parents=True, exist_ok=True)
    cache_path.write_text(json.dumps({
        "yaml_mtime": yaml_path.stat().st_mtime,
        "max_members": max_members,
        "engines": [[sc, list(sub)] for sc, sub in out],
    }, ensure_ascii=False), encoding="utf-8")
    _ENGINES_CACHE[key] = out
    return out


def gdf_topk_candidates(
    full_topk_path: Path,
    *,
    sample: int = 100,
    seed: int = 7,
    exclude: set[str] | None = None,
) -> list[dict]:
    """从 GDF 枚举 full topk 文件抽取候选（每锚点满槽档 Top-K 的有序签名）。"""
    import re

    rng = random.Random(seed)
    sigs: list[str] = []
    rank_re = re.compile(r"^\s*\d+\.\s*RR=\s*[-\d.]+\s+anchor_m=\s*[-\d.]+\s+Swiss=\s*[-\d.]+\s*\|\s*(.+)$")
    for line in Path(full_topk_path).read_text(encoding="utf-8").splitlines():
        m = rank_re.match(line)
        if m:
            sigs.append(m.group(1).strip())
    sigs = sorted(set(sigs) - (exclude or set()))
    # 魂石家族（基底+四变体）全互斥：签名含两个及以上魂石类物品的卡组不合法
    soul_all = {"魂石", "剧毒减速魂石", "剧毒冻结魂石", "灼烧减速魂石", "灼烧冻结魂石"}
    sigs = [s for s in sigs
            if sum(1 for x in s.split(",") if x.strip() in soul_all) <= 1]
    rng.shuffle(sigs)
    return [{"template": "gdf_topk", "signature": s} for s in sigs[:sample]]
