# -*- coding: utf-8 -*-
"""理由图（reason graph）：从物品 YAML AST 机械提取「加入理由」。

原则（docs/bazaar-meta-evidence.md 阶段3后续）：一个物品加入阵容总是有原因的，
原因 = 机制引用，分三类：
- trigger：A 的触发事件由 B 生产（trigger/triggers，condition 中 HasTag 精化，
  如 UseItem+Weapon → WeaponUse；裸 UseItem 过泛，不计入强边）；
- resource：A 的缩放公式/光环值引用 B 生产的资源（Opp/Side/Caster [Poison|Burn|Regen|...]；
  attribute/key 是「修改的对象」而非消费的资源，不计入）；
- selector：A 的目标/光环选择器命中 B 的标签（HasTag/HasDerivedTag/位置选择器由
  perm_constraints 另行覆盖）。

边 B→A 读作「B 给 A 提供了加入理由」。用于：引擎闭包挖掘、构造式提议器的准入与解释。
"""

from __future__ import annotations

from collections import defaultdict
from pathlib import Path

import yaml

import re

EVENTS = {"Poison", "Burn", "Slow", "Freeze", "Haste", "Regen", "Heal", "Charge",
          "Reload", "Crit", "Shield", "Destroy", "Transform", "Flying",
          "PoisonSelf", "CooldownReduction"}
RESOURCES = {"Poison", "Burn", "Regen", "Shield", "Hp", "MaxHp", "Flying", "PoisonSelf",
             "CooldownReduction"}
GROW_KEYS = {"Damage", "Burn", "Poison", "Regen", "Heal", "Shield"}
# 触发频率权重（静态代理）：DoT 类资源每 tick 触发（高频），控制类按次（中频），其余低频
TRIGGER_FREQ = {
    "Poison": 2.5, "Burn": 2.5, "Regen": 2.5, "Heal": 2.0,
    "Slow": 1.5, "Freeze": 1.5, "PoisonSelf": 2.0,
}
# 词元归一化：数据中的别名 → 统一通道名
TOKEN_ALIAS = {
    "InFlight": "Flying",
    "StartFlying": "Flying",
    "StopFlying": "Flying",
    "CooldownReductionPercent": "CooldownReduction",
    "Cooldown": "CooldownReduction",
}
SELF_HARM_TOKENS = {"PoisonSelf", "BurnSelf"}
SELECTOR_TAGS = {"Weapon", "Potion", "Relic", "Friend", "Reagent", "Tool", "Apparel",
                 "Dragon", "Vehicle", "Property"}
SOUL_VARIANTS = {"剧毒减速魂石", "剧毒冻结魂石", "灼烧减速魂石", "灼烧冻结魂石"}
# 与 GDF ItemPool 一致的 Mak 排除清单（局外/未实现物品）
MAK_IGNORED = {"产药药水", "催化剂", "筛盘", "奥秘之书", "亚罕典籍", "蒸馏器", "空灵灰烬"}


def norm(name: str) -> str:
    return "魂石" if name in SOUL_VARIANTS else name


def _collect(node, out: list):
    """收集 AST 词元；attribute/key（修改对象）与裸字符串键名不计入。词元按 TOKEN_ALIAS 归一。"""
    if isinstance(node, dict):
        for k, v in node.items():
            if k in ("attribute", "key"):
                continue
            if k == "type" and isinstance(v, str):
                out.append(TOKEN_ALIAS.get(v, v))
            elif k == "params" and isinstance(v, list):
                for p in v:
                    if isinstance(p, str):
                        out.append(TOKEN_ALIAS.get(p, p))
                    _collect(p, out)
            else:
                _collect(v, out)
    elif isinstance(node, list):
        for x in node:
            _collect(x, out)


def profile(item: dict) -> dict:
    """物品的机制画像：生产/触发消费/资源消费/标签选择。"""
    produces: set[str] = set()
    consumes_trigger: set[str] = set()
    consumes_resource: set[str] = set()
    selects_tags: set[str] = set()
    feeds: set[str] = set()
    has_active = False

    for ab in item.get("Abilities", []) or []:
        t = TOKEN_ALIAS.get(ab.get("type"), ab.get("type"))
        if t in EVENTS:
            produces.add(t)
            has_active = True
        elif t in ("Damage", "Heal"):
            produces.add(t if t == "Heal" else "Damage")
            has_active = True
        # AddAttribute 赋予飞行（key=InFlight）→ 生产 Flying
        if ab.get("type") == "AddAttribute" and ab.get("key") == "InFlight":
            produces.add("Flying")
        # 供能边：给其他物品充能/加速/装填（target 非 SameAsCaster），是引擎喂养关系
        tgt_all: list[str] = []
        _collect(ab.get("target_condition"), tgt_all)
        _collect(ab.get("ex_target_condition"), tgt_all)
        if t in ("Charge", "Haste", "Reload", "CooldownReduction") and "SameAsCaster" not in tgt_all:
            feeds.add(t)
        trigs: list[str] = []
        if "trigger" in ab:
            trigs.append(ab["trigger"])
        for tr in ab.get("triggers", []) or []:
            if isinstance(tr, dict) and "trigger" in tr:
                trigs.append(tr["trigger"])
        for tr in trigs:
            consumes_trigger.add(tr)
        cond_tokens: list[str] = []
        _collect(ab.get("condition"), cond_tokens)
        _collect(ab.get("ex_condition"), cond_tokens)
        if "UseItem" in trigs:
            for tag in SELECTOR_TAGS:
                if tag in cond_tokens:
                    consumes_trigger.add(f"{tag}Use")
            if "Flying" in cond_tokens:
                consumes_trigger.add("FlyingUse")
        for scope in ("target_condition", "ex_target_condition"):
            toks: list[str] = []
            _collect(ab.get(scope), toks)
            for tok in toks:
                if tok in SELECTOR_TAGS:
                    selects_tags.add(tok)
                elif tok in EVENTS | RESOURCES:
                    consumes_resource.add(tok)
    for au in item.get("Auras", []) or []:
        # 冷却操纵光环（含条件化：图书馆的武器/非武器分流）→ 供能边
        if au.get("attribute") in ("CooldownReduction", "CooldownReductionPercent"):
            feeds.add("CooldownReduction")
        toks: list[str] = []
        _collect(au, toks)
        for r in EVENTS | RESOURCES:
            if r in toks:
                consumes_resource.add(r)
        for tag in SELECTOR_TAGS:
            if tag in toks:
                selects_tags.add(tag)
    tags = set(item.get("Tags") or [])
    # 弹药经济：带弹药上限的物品需要被装填（消费 Reload 资源）
    if item.get("AmmoCap") is not None:
        consumes_resource.add("Reload")
    if has_active:
        produces.add("UseItem")
        for tag in SELECTOR_TAGS & tags:
            produces.add(f"{tag}Use")
        if "Flying" in produces:
            produces.add("FlyingUse")
    return {"produces": produces, "consumes_trigger": consumes_trigger,
            "consumes_resource": consumes_resource, "selects_tags": selects_tags,
            "feeds": feeds, "tags": tags}


def has_active_use(p: dict) -> bool:
    return "UseItem" in p["produces"]


def pair_weight(reasons: list[str]) -> float:
    """按理由通道分别封顶（枢纽物品的多触发类型不再线性膨胀）：
    trigger ≤2，resource ≤2，selector ≤1，feed 每条 0.5（至多 1 条）。"""
    n_tr = sum(1 for r in reasons if r.startswith("trigger:"))
    n_re = sum(1 for r in reasons if r.startswith("resource:"))
    n_se = sum(1 for r in reasons if r.startswith("selector:"))
    n_fe = sum(1 for r in reasons if r.startswith("feed:"))
    return min(n_tr, 2) + min(n_re, 2) + min(n_se, 1) + 0.5 * min(n_fe, 1)


def tier_value(item: dict, key, tier_idx: int = 2):
    """取 item[key] 在指定档位的数值（min_tier 偏移与 codegen 一致，时间字符串转秒）。"""
    if not isinstance(key, str):
        return None
    v = item.get(key)
    if isinstance(v, list):
        if not v:
            return None
        base = {"bronze": 0, "silver": 1, "gold": 2, "diamond": 3}.get(
            str(item.get("Tier", "Bronze")).lower(), 0)
        i = min(max(tier_idx - base, 0), len(v) - 1)
        v = v[i]
    if isinstance(v, str):
        m = re.match(r"([\d.]+)(ms|s)?$", v)
        if m:
            x = float(m.group(1))
            return x / 1000 if m.group(2) == "ms" else x
        return None
    return v if isinstance(v, (int, float)) else None


def core_roles(item: dict, tier_idx: int = 2) -> list[dict]:
    """物品的「数值核心」角色（强度可度量的核心件）：

    - grow：trigger + AddAttribute + SameAsCaster（成长型；含任务门控）；
    - payoff：trigger + 直接产出（伤害/剧毒/灼烧/再生/治疗，每次触发固定收益）；
    - base：主动技能的高基础产出（无触发依赖）。
    self_sustaining：物品同时生产自己消费的触发事件（自续成长）。
    """
    cores: list[dict] = []
    produces_events: set[str] = set()
    for ab in item.get("Abilities", []) or []:
        t = TOKEN_ALIAS.get(ab.get("type"), ab.get("type"))
        if t in EVENTS:
            produces_events.add(t)
    for ab in item.get("Abilities", []) or []:
        t = ab.get("type")
        trig = ab.get("trigger")
        if isinstance(ab.get("triggers"), list):
            trig = trig or [x.get("trigger") for x in ab["triggers"] if isinstance(x, dict)]
        if t == "AddAttribute" and trig and ab.get("target_condition") == "SameAsCaster":
            key = ab.get("key")
            if key in GROW_KEYS:
                trigs = trig if isinstance(trig, list) else [trig]
                for tr in trigs:
                    cores.append({
                        "role": "grow", "trigger": TOKEN_ALIAS.get(tr, tr), "key": key,
                        "magnitude": tier_value(item, ab.get("value"), tier_idx),
                        "quest_gated": "QuestComplete" in str(ab.get("ex_condition", "")),
                        "self_sustaining": TOKEN_ALIAS.get(tr, tr) in produces_events,
                    })
        elif t in ("Damage", "Burn", "Poison", "Regen", "Heal") and trig:
            trigs = trig if isinstance(trig, list) else [trig]
            for tr in trigs:
                cores.append({
                    "role": "payoff", "trigger": TOKEN_ALIAS.get(tr, tr), "key": t,
                    "magnitude": tier_value(item, t, tier_idx),
                    "quest_gated": "QuestComplete" in str(ab.get("ex_condition", "")),
                    "self_sustaining": TOKEN_ALIAS.get(tr, tr) in produces_events,
                })
    if not cores:
        for key in ("Damage", "Burn", "Poison", "Regen"):
            mag = tier_value(item, key, tier_idx)
            if mag:
                cores.append({"role": "base", "trigger": None, "key": key,
                              "magnitude": mag, "quest_gated": False,
                              "self_sustaining": False})
                break
    return cores


class ReasonGraph:
    """有向理由图：edges[a][b] = B 给 A 的理由列表。"""

    def __init__(self, db: dict[str, dict]):
        self.db = {n: it for n, it in db.items() if n not in MAK_IGNORED}
        self.prof = {n: profile(it) for n, it in self.db.items()}
        self.edges: dict[str, dict[str, list[str]]] = defaultdict(dict)
        # 数值核心（成长型/触发收益型/高基础型）与按 key 归一化的强度分
        self.cores: dict[str, list[dict]] = {n: core_roles(it) for n, it in self.db.items()}
        # 触发频率加权后的有效强度；按 key 的 p90 归一（抗离群）
        key_vals: dict[str, list[float]] = defaultdict(list)
        for cs in self.cores.values():
            for c in cs:
                if c["magnitude"]:
                    fw = TRIGGER_FREQ.get(c.get("trigger"), 1.0)
                    key_vals[c["key"]].append(float(c["magnitude"]) * fw)
        key_norm: dict[str, float] = {}
        for k, vs in key_vals.items():
            vs.sort()
            key_norm[k] = vs[min(len(vs) - 1, int(len(vs) * 0.9))] or 1.0
        self.core_score: dict[str, float] = {}
        for n, cs in self.cores.items():
            best = 0.0
            for c in cs:
                if not c["magnitude"]:
                    continue
                fw = TRIGGER_FREQ.get(c.get("trigger"), 1.0)
                frac = float(c["magnitude"]) * fw / key_norm[c["key"]]
                role_w = {"grow": 1.0, "payoff": 0.8, "base": 0.4}.get(c["role"], 0.4)
                if c["self_sustaining"]:
                    role_w *= 1.25
                best = max(best, frac * role_w)
            self.core_score[n] = best
        # 核心的主通道（家族配额用）
        self.core_channel: dict[str, str] = {}
        for n, cs in self.cores.items():
            best_c, best_v = None, 0.0
            for c in cs:
                if not c["magnitude"]:
                    continue
                fw = TRIGGER_FREQ.get(c.get("trigger"), 1.0)
                v = float(c["magnitude"]) * fw / key_norm[c["key"]]
                if v > best_v:
                    best_c, best_v = c["key"], v
            if best_c:
                self.core_channel[n] = best_c
        # 暴击生产端：自带暴击率或赋予他人暴击率
        for n, it in self.db.items():
            if it.get("CritRate") is not None:
                self.prof[n]["produces"].add("Crit")
            for au in it.get("Auras", []) or []:
                if au.get("attribute") == "CritRate":
                    self.prof[n]["produces"].add("Crit")
            for ab in it.get("Abilities", []) or []:
                if ab.get("type") == "AddAttribute" and ab.get("key") == "CritRate" \
                        and ab.get("target_condition") != "SameAsCaster":
                    self.prof[n]["produces"].add("Crit")
        for a, pa in self.prof.items():
            for b, pb in self.prof.items():
                if a == b:
                    continue
                reasons: list[str] = []
                for tr in sorted((pa["consumes_trigger"] - {"UseItem"}) & pb["produces"]):
                    reasons.append(f"trigger:{tr}")
                for r in sorted(pa["consumes_resource"] & pb["produces"]):
                    reasons.append(f"resource:{r}")
                for tag in sorted(pa["selects_tags"] & pb["tags"]):
                    reasons.append(f"selector:{tag}")
                # 供能边：B 给其他物品充能/加速，A 有主动技能则受益（弱理由，权重 0.5）
                for f in sorted(pb["feeds"]):
                    if has_active_use(pa):
                        reasons.append(f"feed:{f}")
                if reasons:
                    self.edges[a][b] = reasons
        self._neigh: dict[str, set[str]] = defaultdict(set)
        for a, bs in self.edges.items():
            for b in bs:
                self._neigh[a].add(b)
                self._neigh[b].add(a)

    def neighbors(self, name: str) -> set[str]:
        return self._neigh.get(norm(name), set())

    def reasons_between(self, a: str, b: str) -> list[str]:
        """B 给 A 的理由（无则空表）。"""
        return self.edges.get(norm(a), {}).get(norm(b), [])

    def reason_count(self, name: str, deck_rest: list[str]) -> int:
        """name 与 deck_rest 之间的理由边总数（双向）。"""
        x = norm(name)
        n = 0
        for y in deck_rest:
            yb = norm(y)
            if yb == x:
                continue
            n += len(self.edges.get(x, {}).get(yb, [])) > 0
            n += len(self.edges.get(yb, {}).get(x, [])) > 0
        return n

    def internal_edges(self, subset: list[str]) -> float:
        """子集内部理由加权总分（双向、通道封顶）。"""
        sub = {norm(s) for s in subset}
        total = 0.0
        for a in sub:
            for b in sub:
                if a != b:
                    rs = self.edges.get(a, {}).get(b, [])
                    total += pair_weight(rs)
        return total


def load(data_dir: str | Path, hero: str = "mak") -> ReasonGraph:
    doc = yaml.safe_load(open(Path(data_dir) / f"{hero.lower()}.yaml", encoding="utf-8"))
    return ReasonGraph({it["Name"]: it for it in doc["items"]})


def export_profiles_json(g: ReasonGraph, path: str | Path) -> None:
    """导出 C++ 挖掘用的静态画像（提取规则的唯一事实来源在本文件；YAML 变更后重跑）。

    结构：{name: {size, tier, produces[], consumes_trigger[], consumes_resource[],
    selects_tags[], feeds[], cores[{role, trigger, key, magnitude, self_sustaining}],
    core_score, core_channel}}
    """
    import json

    sizes = {"Small": 1, "Medium": 2, "Large": 3}
    out = {}
    for n, p in g.prof.items():
        item = g.db[n]
        cores = []
        for c in g.cores.get(n, []):
            cores.append({
                "role": c["role"], "trigger": c.get("trigger"), "key": c["key"],
                "magnitude": c["magnitude"], "self_sustaining": c["self_sustaining"],
            })
        out[n] = {
            "size": sizes.get(item.get("Size", ""), 1),
            "tier": item.get("Tier", "Bronze"),
            "tags": sorted(item.get("Tags") or []),
            "produces": sorted(p["produces"]),
            "consumes_trigger": sorted(p["consumes_trigger"]),
            "consumes_resource": sorted(p["consumes_resource"]),
            "selects_tags": sorted(p["selects_tags"]),
            "feeds": sorted(p["feeds"]),
            "cores": cores,
            "core_score": round(g.core_score.get(n, 0), 4),
            "core_channel": g.core_channel.get(n),
        }
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_text(json.dumps(out, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    import sys as _sys

    _out = _sys.argv[1] if len(_sys.argv) > 1 else "out/meta_search/reason_profiles.json"
    _g = load(Path(__file__).resolve().parent.parent.parent / "data" / "items", "mak")
    export_profiles_json(_g, _out)
    print(f"wrote {_out} ({len(_g.db)} items)")
