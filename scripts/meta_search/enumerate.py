# -*- coding: utf-8 -*-
"""并行锚点枚举（探测主管线步骤 1：候选生成）：每个锚点一个 GDF 进程，进程级并行。

GDF 在本管线中的角色是**锚点枚举器/候选生成器**：其内部贪心/beam 评估仅用于
枚举过程本身，不构成强度结论（真值测量由 bin/bazaararena_meta + 精英闭环承担）。

与 scripts/gdf_enumerate_anchor_top1.py 的差别：
- 进程池并行（本机 24 核下 136 锚点 Mak L8 从 ~2.6h 降到 ~13min）；
- 保留每锚点原始 stdout 供轨迹分析；
- 输出 TSV（每锚点满槽档 top1）与 full topk 文件，格式与原脚本一致。
"""

from __future__ import annotations

import argparse
import concurrent.futures as cf
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent.parent / "scripts"))

REPO = Path(__file__).resolve().parent.parent.parent

# 与 GDF ItemPool 的 Mak 忽略列表一致（IsIgnoredMakItemForGdf, ItemPool.cpp）
MAK_IGNORED = {"产药药水", "催化剂", "筛盘", "奥秘之书", "亚罕典籍", "蒸馏器", "空灵灰烬"}
SOUL_VARIANTS = ["剧毒减速魂石", "剧毒冻结魂石", "灼烧减速魂石", "灼烧冻结魂石"]
# 与 IsIgnoredVanessaItemForGdf 一致；烙刀拆为互斥双变体（ResolveItemAlias, DeckRep.cpp）
VANESSA_IGNORED = {"伪装"}
BRAND_VARIANTS = ["减速烙刀", "加速烙刀"]


def list_anchors(data_dir: Path, hero: str) -> list[str]:
    import yaml

    doc = yaml.safe_load(open(data_dir / f"{hero.lower()}.yaml", encoding="utf-8"))
    names = [it["Name"] for it in doc["items"]]
    if hero.lower() == "mak":
        names = [n for n in names if n not in MAK_IGNORED and n != "魂石"] + SOUL_VARIANTS
    if hero.lower() == "vanessa":
        names = [n for n in names if n not in VANESSA_IGNORED and n != "烙刀"] + BRAND_VARIANTS
    return names


def run_anchor(gdf: Path, data_dir: Path, hero: str, anchor: str, level: int,
               top_k: int, top_mult: int, lam: float, mu: float) -> tuple[str, int, str]:
    cmd = [
        str(gdf), "--data-dir", str(data_dir),
        "--pool-hero", hero,
        "--anchor-item", anchor,
        "--level", str(level),
        "--top-k", str(top_k),
        "--top-multiplier", str(top_mult),
        "--lambda-anchor", str(lam),
        "--mu-diversity", str(mu),
        "--diversity-exclude-seeds",
    ]
    proc = subprocess.run(cmd, cwd=str(REPO), capture_output=True, text=True,
                          encoding="utf-8", errors="replace")
    return anchor, proc.returncode, proc.stdout + proc.stderr


def main() -> int:
    from gdf_enumerate_anchor_top1 import _parse_gdf_output, _write_full_topk_file

    p = argparse.ArgumentParser()
    p.add_argument("--hero", default="Mak")
    p.add_argument("--level", type=int, default=8)
    p.add_argument("--top-k", type=int, default=10)
    p.add_argument("--top-multiplier", type=int, default=3)
    p.add_argument("--lambda-anchor", type=float, default=0.5)
    p.add_argument("--mu-diversity", type=float, default=0.1)
    p.add_argument("--parallel", type=int, default=12)
    p.add_argument("--data-dir", type=Path, default=REPO / "data" / "items")
    p.add_argument("--gdf", type=Path, default=REPO / "bin" / (
        "bazaararena_gdf.exe" if sys.platform == "win32" else "bazaararena_gdf"))
    p.add_argument("-o", "--output", type=Path, required=True, help="TSV 输出")
    p.add_argument("--raw-dir", type=Path, default=None, help="每锚点原始日志目录")
    p.add_argument("--full-topk-output", type=Path, default=None)
    args = p.parse_args()

    anchors = list_anchors(args.data_dir, args.hero)
    print(f"anchors={len(anchors)} parallel={args.parallel}")
    raw_dir = args.raw_dir
    if raw_dir:
        raw_dir.mkdir(parents=True, exist_ok=True)

    texts: list[str] = []
    failed: list[str] = []
    with cf.ThreadPoolExecutor(max_workers=args.parallel) as ex:
        futs = {
            ex.submit(run_anchor, args.gdf, args.data_dir, args.hero, a,
                      args.level, args.top_k, args.top_multiplier,
                      args.lambda_anchor, args.mu_diversity): a
            for a in anchors
        }
        for i, fut in enumerate(cf.as_completed(futs), 1):
            anchor, code, text = fut.result()
            if raw_dir:
                (raw_dir / f"{anchor}.log").write_text(text, encoding="utf-8")
            if code != 0:
                failed.append(anchor)
                print(f"[{i}/{len(anchors)}] FAIL {anchor} (code={code})")
            else:
                texts.append(text)
                print(f"[{i}/{len(anchors)}] ok {anchor}")

    rows, full_blocks = _parse_gdf_output("".join(texts), set())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="\n") as out:
        out.write("anchor\trr\tanchor_m\tswiss\tdeck\n")
        for anchor, rr, am, sw, sig in rows:
            out.write(f"{anchor}\t{rr}\t{am}\t{sw}\t{sig}\n")
    full_path = args.full_topk_output or args.output.with_name(f"{args.output.stem}_full_topk.txt")
    _write_full_topk_file(full_path, full_blocks)
    print(f"wrote {len(rows)} rows to {args.output}; full topk -> {full_path}; failed={failed}")
    return 0 if not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
