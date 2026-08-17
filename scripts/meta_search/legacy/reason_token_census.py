# -*- coding: utf-8 -*-
"""理由图提取器的词元普查：证明「机制通道缺失不可能静默存在」。

扫描 mak.yaml 全部 AST 构造（能力/光环的 type、trigger、target/condition 词元、
属性键），与 reason_graph 已建模词表对照，输出三类：
- MODELED：已被理由边规则消费；
- IRRELEVANT：对战内交互无关（局外/经济/展示类，人工认定）；
- UNMODELED：未建模——任何出现在这里的词元都是潜在的失明通道，必须评审。
"""

from __future__ import annotations

import sys
from collections import Counter
from pathlib import Path

import yaml

REPO = Path(__file__).resolve().parent.parent.parent.parent
sys.path.insert(0, str(REPO / "scripts"))
from meta_search.legacy.reason_graph import EVENTS, RESOURCES, SELECTOR_TAGS, TOKEN_ALIAS  # noqa: E402

# reason_graph 已消费的词元（与提取规则一一对应）
MODELED = (
    EVENTS | RESOURCES | SELECTOR_TAGS
    | {"Damage", "AddAttribute", "HasTag", "HasDerivedTag", "SameAsCaster",
       "AdjacentToCaster", "LeftOfCaster", "RightOfCaster",
       "StrictlyLeftOfCaster", "StrictlyRightOfCaster", "DifferentFromCaster",
       "UseItem", "NotDestroyed", "SameSide", "And", "Or", "Not", "Eq", "Ne",
       "Count", "Mul", "Add", "Percent", "PercentFloor", "If", "Caster",
       "Side", "Opp", "ItemCount", "Max", "Min", "Negate",
       # 2026-08 第二批建模通道：飞行 / 自毒 / 冷却操纵
       "InFlight", "StartFlying", "StopFlying", "PoisonSelf",
       "CooldownReduction", "CooldownReductionPercent", "Item", "Flying"}
)
# 对战内交互无关（局外/经济/默认值声明），人工认定后可豁免
IRRELEVANT = {"Gold", "Income", "Value", "SellPrice", "Quest", "Custom_0",
              "Custom_1", "Custom_2", "Custom_3", "Custom_4", "Multicast",
              "AmmoCap", "LifeSteal", "CritRate", "MaxHp", "Hp"}


def census_tokens(item: dict) -> set[str]:
    toks: set[str] = set()

    def walk(node):
        if isinstance(node, dict):
            for k, v in node.items():
                if k in ("type", "attribute", "key", "trigger") and isinstance(v, str):
                    toks.add(TOKEN_ALIAS.get(v, v))
                elif k == "params" and isinstance(v, list):
                    for p in v:
                        if isinstance(p, str):
                            toks.add(TOKEN_ALIAS.get(p, p))
                        walk(p)
                elif k in ("Abilities", "Auras", "Passives", "condition",
                           "ex_condition", "target_condition", "ex_target_condition",
                           "triggers", "value"):
                    walk(v)
        elif isinstance(node, list):
            for x in node:
                walk(x)

    walk(item.get("Abilities", []))
    walk(item.get("Auras", []))
    walk(item.get("Passives", []))
    return toks


def main() -> int:
    doc = yaml.safe_load(open(REPO / "data/items/mak.yaml", encoding="utf-8"))
    unmodeled: Counter = Counter()
    irrelevant: Counter = Counter()
    modeled_count = 0
    for it in doc["items"]:
        for tok in census_tokens(it):
            if tok in MODELED:
                modeled_count += 1
            elif tok in IRRELEVANT:
                irrelevant[tok] += 1
            else:
                unmodeled[tok] += 1
    print(f"建模词元命中 {modeled_count} 次；豁免类 {sum(irrelevant.values())} 次；"
          f"未建模词元 {len(unmodeled)} 种 / {sum(unmodeled.values())} 次")
    if unmodeled:
        print("\n== UNMODELED（潜在失明通道，须评审）==")
        for tok, cnt in unmodeled.most_common():
            print(f"  {cnt:4d}× {tok}")
    return 1 if unmodeled else 0


if __name__ == "__main__":
    raise SystemExit(main())
