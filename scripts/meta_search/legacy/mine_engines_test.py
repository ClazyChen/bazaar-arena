# -*- coding: utf-8 -*-
"""理由图挖掘的覆盖回归测试（改提取规则或物品数据后必跑）。

不变量：
1. 已知强流派引擎核心必须全部出现在无截断挖掘空间中（10/10 覆盖）；
2. 词元普查的未建模清单不得超出已批准快照（SNAPSHOT_UNMODELED）——
   新出现的未建模词元 = 新的潜在失明通道，测试失败并点名；
3. 挖掘有界性：无截断全量枚举须在 120s 内完成。
"""

import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent.parent))
from meta_search.legacy.mine_engines import mine_engines  # noqa: E402
from meta_search.legacy.reason_graph import load  # noqa: E402

REPO = Path(__file__).resolve().parent.parent.parent.parent

KNOWN_FAMILIES = {
    "药水引擎(炼金炉)": {"碎瓶", "沸腾烧瓶", "炼金炉"},
    "药水引擎(发射器)": {"碎瓶", "药瓶发射器", "符文药水"},
    "武器剧毒": {"地刺陷阱", "罂粟花田", "蛇首手杖"},
    "自毒注射": {"快速注射系统", "肾上腺素调节服", "毒伞菇"},
    "空白石碑多元素": {"空白石碑", "秘密配方", "魂石"},
    "遗物再生": {"先祖墓", "魂戒", "秘密配方"},
    "减速控制": {"时间之砂", "嗅盐"},
    "天平充能": {"天平", "永恒火炬"},
    "图书馆引擎": {"图书馆", "永恒火炬"},
    "飞行": {"风之巨龙", "重力之石"},
}

# 已批准（待评审）的未建模词元快照；新增未建模词元会使测试失败
SNAPSHOT_UNMODELED = {
    "QuestComplete", "HasCooldown", "CanCrit", "Lt", "Time", "Gt", "Sub",
    "Resistance", "SideIndex", "Target", "BaseRegen", "ReduceAttribute",
    "EveryFrame", "DifferentSide", "NotHasTag", "Tags", "BitCount", "Le",
    "Ammo", "SameAsSource", "OppMax", "IsPoisonTick", "IsBurnTick", "GainGold",
    "SlowTargetCount", "BattleStart", "Transform_quicksilver", "StartSandstorm",
    "ReduceMaxHp", "AddMaxHp", "FreezeTargetCount", "SideItemTypes", "AmmoRemaining",
    "Source", "AboutToLose", "Constant", "SetHp", "Sum", "SameAsTarget",
    "Transform_mirror", "FirstHalfHp",
}


def main() -> int:
    failures = []

    t0 = time.time()
    g = load(REPO / "data" / "items", "mak")
    kept = mine_engines(g, apply_dedupe=False)
    elapsed = time.time() - t0
    print(f"无截断挖掘: {len(kept)} 闭包, {elapsed:.0f}s")
    if elapsed > 120:
        failures.append(f"bounded_time({elapsed:.0f}s > 120s)")

    kept_sets = [set(sub) for _, sub in kept]
    for name, core in KNOWN_FAMILIES.items():
        hit = any(core <= s for s in kept_sets)
        print(f"{'PASS' if hit else 'FAIL'}  family:{name}")
        if not hit:
            failures.append(f"family:{name}")

    # 词元普查快照对比
    import yaml
    from collections import Counter

    sys.path.insert(0, str(REPO / "scripts"))
    from meta_search.legacy.reason_token_census import MODELED, IRRELEVANT, census_tokens  # noqa: E402

    doc = yaml.safe_load(open(REPO / "data/items/mak.yaml", encoding="utf-8"))
    unmodeled: Counter = Counter()
    for it in doc["items"]:
        for tok in census_tokens(it):
            if tok not in MODELED and tok not in IRRELEVANT:
                unmodeled[tok] += 1
    new_tokens = set(unmodeled) - SNAPSHOT_UNMODELED
    removed = SNAPSHOT_UNMODELED - set(unmodeled)
    print(f"{'PASS' if not new_tokens else 'FAIL'}  census:no-new-unmodeled "
          f"(新增: {sorted(new_tokens) or '无'}; 已消失: {sorted(removed) or '无'})")
    if new_tokens:
        failures.append(f"census:new-unmodeled={sorted(new_tokens)}")

    print()
    if failures:
        print(f"{len(failures)} FAILED: {failures}")
        return 1
    print("reason-graph coverage regression: all passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
