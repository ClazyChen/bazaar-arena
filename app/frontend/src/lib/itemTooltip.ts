import type { DeckSlotAttrsOverride, ItemRow } from "@/types";
import {
    AMMO_KEYWORD_RGB,
    BURN_KEYWORD_RGB,
    CHARGE_KEYWORD_RGB,
    DAMAGE_KEYWORD_RGB,
    FREEZE_KEYWORD_RGB,
    HEAL_KEYWORD_RGB,
    POISON_KEYWORD_RGB,
    REGEN_KEYWORD_RGB,
    SHIELD_KEYWORD_RGB,
    SLOW_KEYWORD_RGB,
    tierBorderColor,
} from "@/lib/deckMath";

/** 与 engine/cli ColorizeSummaryLine 关键词配色一致；仅包裹匹配到的子串 */
const KEYWORD_COLORS: { needle: string; color: string }[] = [
    { needle: "治疗", color: HEAL_KEYWORD_RGB },
    { needle: "生命上限", color: "rgb(97, 176, 60)" },
    { needle: "生命再生", color: REGEN_KEYWORD_RGB },
    { needle: "弹药", color: AMMO_KEYWORD_RGB },
    { needle: "装填", color: AMMO_KEYWORD_RGB },
    { needle: "加速", color: CHARGE_KEYWORD_RGB },
    { needle: "充能", color: CHARGE_KEYWORD_RGB },
    { needle: "冻结", color: FREEZE_KEYWORD_RGB },
    { needle: "护盾", color: SHIELD_KEYWORD_RGB },
    { needle: "飞行", color: SHIELD_KEYWORD_RGB },
    { needle: "伤害", color: DAMAGE_KEYWORD_RGB },
    { needle: "暴击率", color: DAMAGE_KEYWORD_RGB },
    { needle: "暴击伤害", color: DAMAGE_KEYWORD_RGB },
    { needle: "摧毁", color: "rgb(255, 50, 120)" },
    { needle: "剧毒", color: POISON_KEYWORD_RGB },
    { needle: "修复", color: "rgb(143, 252, 188)" },
    { needle: "减速", color: SLOW_KEYWORD_RGB },
    { needle: "灼烧", color: BURN_KEYWORD_RGB },
];

const MS_DISPLAY_KEYS = new Set(["Cooldown", "Haste", "Slow", "Freeze", "Charge"]);

/** Desc 占位符中的别名 → tooltip_attrs / ItemKey */
const DESC_FIELD_ALIASES: Record<string, string> = {
    ChargeSeconds: "Charge",
    SlowSeconds: "Slow",
    FreezeSeconds: "Freeze",
    HasteSeconds: "Haste",
    ModifyTargetCount: "ModifyAttributeTargetCount",
};

function escapeHtml(s: string): string {
    return s
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
}

function escapeRegExp(s: string): string {
    return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function formatNumber(n: number): string {
    if (!Number.isFinite(n)) return "";
    if (Number.isInteger(n)) return String(n);
    const t = n.toFixed(4).replace(/\.?0+$/, "");
    return t || "0";
}

/** 引擎内毫秒 → 秒展示（非整数允许） */
export function formatSecondsFromMs(ms: number): string {
    return formatNumber(ms / 1000);
}

function colorizePlainText(text: string): string {
    const rules = [...KEYWORD_COLORS].sort((a, b) => b.needle.length - a.needle.length);
    if (rules.length === 0) return escapeHtml(text);
    const pattern = new RegExp(`(${rules.map((r) => escapeRegExp(r.needle)).join("|")})`, "g");
    const parts = text.split(pattern);
    return parts
        .map((part) => {
            const rule = rules.find((r) => r.needle === part);
            if (rule) {
                return `<span style="color:${rule.color}">${escapeHtml(part)}</span>`;
            }
            return escapeHtml(part);
        })
        .join("");
}

function sizeLabel(size: number): string {
    if (size === 1) return "小型";
    if (size === 2) return "中型";
    if (size === 3) return "大型";
    return String(size);
}

/** 与 engine/include/bazaararena/core/Tag.hpp 枚举名一致（YAML Tags 英文） */
const TAG_NAME_ZH: Record<string, string> = {
    Weapon: "武器",
    Tool: "工具",
    Apparel: "服饰",
    Friend: "伙伴",
    Food: "食物",
    Tech: "科技",
    Property: "地产",
    Vehicle: "载具",
    Relic: "遗物",
    Dragon: "巨龙",
    Drone: "无人机",
    Toy: "玩具",
    Aquatic: "水系",
    Ray: "射线",
    Trap: "陷阱",
    Loot: "战利品",
    Reagent: "原料",
    Potion: "药水",
    Core: "核心",
    Dinosaur: "恐龙",
};

function tagLabelZh(tag: string): string {
    return TAG_NAME_ZH[tag] ?? tag;
}

function normalizePlaceholderInner(inner: string): { key: string; leadingPlus: boolean; trailingPercent: boolean } {
    let s = inner.trim();
    const leadingPlus = s.startsWith("+");
    if (leadingPlus) s = s.slice(1);
    const trailingPercent = s.endsWith("%");
    if (trailingPercent) s = s.slice(0, -1);
    const key = DESC_FIELD_ALIASES[s] ?? s;
    return { key, leadingPlus, trailingPercent };
}

function getTierArray(attrs: Record<string, number[]> | null | undefined, key: string): number[] | null {
    if (!attrs) return null;
    const a = attrs[key];
    if (!a || a.length === 0) return null;
    return a;
}

/** Desc 占位符引擎键名 → 卡组 attrs_override 字段（仅这些键支持「已复写」展示） */
const DESC_KEY_TO_DECK_OVERRIDE: Partial<Record<string, keyof DeckSlotAttrsOverride>> = {
    Custom_0: "custom_0",
    Custom_1: "custom_1",
    Custom_2: "custom_2",
    Custom_3: "custom_3",
    Quest: "quest",
};

function getDeckAttrOverride(engineKey: string, o?: DeckSlotAttrsOverride | null): number | undefined {
    if (!o) return undefined;
    const field = DESC_KEY_TO_DECK_OVERRIDE[engineKey];
    if (!field) return undefined;
    const v = o[field];
    return v !== undefined && v !== null ? v : undefined;
}

function valuesAllEqual(arr: number[], eps = 1e-6): boolean {
    if (arr.length <= 1) return true;
    const f = arr[0];
    return arr.every((x) => Math.abs(x - f) < eps);
}

function formatOneValue(key: string, v: number): string {
    if (MS_DISPLAY_KEYS.has(key)) return formatSecondsFromMs(v);
    return formatNumber(v);
}

/** 与 tooltip 正文一致，不随 tier 染色 */
const TOOLTIP_VALUE_DEFAULT = "#e8eaef";

function formatNumericDefaultHtml(key: string, v: number): string {
    const text = formatOneValue(key, v);
    return `<span style="color:${TOOLTIP_VALUE_DEFAULT};font-weight:600">${escapeHtml(text)}</span>`;
}

/** 数值按对应 tier 上色（与卡组边框色一致）；仅当属性随等级变化时使用 */
function formatNumericTierHtml(key: string, v: number, tierIndex: number): string {
    const text = formatOneValue(key, v);
    const color = tierBorderColor(tierIndex);
    return `<span style="color:${color};font-weight:600">${escapeHtml(text)}</span>`;
}

function wrapAffixes(innerHtml: string, leadingPlus: boolean, trailingPercent: boolean): string {
    return `${leadingPlus ? "+" : ""}${innerHtml}${trailingPercent ? "%" : ""}`;
}

function formatLadderColoredHtml(
    key: string,
    sliced: number[],
    startTierIndex: number,
    leadingPlus: boolean,
    trailingPercent: boolean,
): string {
    return sliced
        .map((v, i) => {
            const ti = startTierIndex + i;
            const core = formatNumericTierHtml(key, v, ti);
            return wrapAffixes(core, leadingPlus, trailingPercent);
        })
        .join('<span style="opacity:0.55"> » </span>');
}

const OVERRIDDEN_SUFFIX_HTML = `<span style="opacity:0.82;font-weight:400;color:#9aa3b2">${escapeHtml("（已复写）")}</span>`;

/** Desc 占位符内展示的 HTML（已含上色与前后缀） */
function formatPlaceholderValueHtml(
    key: string,
    arr: number[] | null,
    mode: "deck" | "pool",
    tier: number,
    minTier: number,
    leadingPlus: boolean,
    trailingPercent: boolean,
    attrsOverride?: DeckSlotAttrsOverride | null,
): string {
    const ov = mode === "deck" ? getDeckAttrOverride(key, attrsOverride) : undefined;
    if ((!arr || arr.length === 0) && ov === undefined) {
        return escapeHtml("—");
    }
    const t = Math.min(Math.max(tier, 0), 4);
    if (mode === "deck") {
        const v = ov !== undefined ? ov : arr![t] ?? arr![0];
        const core = formatNumericDefaultHtml(key, v);
        const wrapped = wrapAffixes(core, leadingPlus, trailingPercent);
        return ov !== undefined ? wrapped + OVERRIDDEN_SUFFIX_HTML : wrapped;
    }
    // pool：首段守卫已保证有阶梯数据（无 attrs 时不会进入此处）
    const poolArr = arr as number[];
    const sliced = sliceTierValuesForPoolDisplay(poolArr, minTier);
    if (sliced.length === 0) return escapeHtml("?");
    const startTier = Math.min(Math.max(minTier, 0), 4);
    if (sliced.length === 1 || valuesAllEqual(sliced)) {
        const core = formatNumericDefaultHtml(key, sliced[0]);
        return wrapAffixes(core, leadingPlus, trailingPercent);
    }
    return formatLadderColoredHtml(key, sliced, startTier, leadingPlus, trailingPercent);
}

/** 备选区阶梯：只含物品可能出现的 tier，即 min_tier..钻石(3)；传奇(min_tier=4)仅一档 */
function sliceTierValuesForPoolDisplay(arr: number[], minTier: number): number[] {
    const mt = Math.min(Math.max(minTier, 0), 4);
    if (mt >= 4) {
        if (arr.length >= 5) return [arr[4]];
        return [arr[arr.length - 1]];
    }
    return arr.slice(mt, 4);
}

/** Desc 模板里分号表示换行，不显示分号 */
export function preprocessDescForTooltip(desc: string): string {
    return desc.replace(/;/g, "\n").replace(/；/g, "\n");
}

function formatDescHtml(
    desc: string,
    attrs: Record<string, number[]> | null | undefined,
    mode: "deck" | "pool",
    tier: number,
    minTier: number,
    attrsOverride?: DeckSlotAttrsOverride | null,
): string {
    const re = /\{([^}]+)\}/g;
    let out = "";
    let last = 0;
    let m: RegExpExecArray | null;
    while ((m = re.exec(desc)) !== null) {
        const lit = desc.slice(last, m.index);
        out += colorizePlainText(lit);
        const inner = m[1] ?? "";
        const { key, leadingPlus, trailingPercent } = normalizePlaceholderInner(inner);
        const arr = getTierArray(attrs, key);
        const hasDeckOverride =
            mode === "deck" && getDeckAttrOverride(key, attrsOverride) !== undefined;
        const innerHtml =
            arr || hasDeckOverride
                ? formatPlaceholderValueHtml(
                      key,
                      arr,
                      mode,
                      tier,
                      minTier,
                      leadingPlus,
                      trailingPercent,
                      attrsOverride,
                  )
                : escapeHtml("—");
        out += `<span class="it-ph">${innerHtml}</span>`;
        last = m.index + m[0].length;
    }
    out += colorizePlainText(desc.slice(last));
    return out;
}

/**
 * 与 `buildItemTooltipHtml` 中「冷却时间」是否展示及 deck 档位取值一致。
 * 目录无有效冷却（如仅战斗开始触发的冰锥）时返回 null。
 */
export function itemCooldownMsForDeckTier(item: ItemRow | undefined, tier: number): number | null {
    if (!item) return null;
    const cdArr = item.tooltip_attrs?.Cooldown;
    if (!cdArr?.length) return null;
    if (!cdArr.some((v) => v > 0)) return null;
    const tDeck = Math.min(Math.max(tier, 0), 4);
    const ms = cdArr[tDeck] ?? cdArr[0] ?? 0;
    return ms > 0 ? ms : null;
}

export function buildItemTooltipHtml(
    item: ItemRow,
    opts: { mode: "deck" | "pool"; tier?: number; attrs_override?: DeckSlotAttrsOverride | null },
): string {
    const attrs = item.tooltip_attrs ?? null;
    const tier = opts.tier ?? 0;
    const minTier = item.min_tier ?? 0;
    const name = escapeHtml(item.name);
    const sizeLine = `<span class="it-meta"><em>${escapeHtml(sizeLabel(item.size))}</em>${item.tags?.length ? " " : ""}${(item.tags ?? []).map((t) => `<em>${escapeHtml(tagLabelZh(t))}</em>`).join(" ")}</span>`;

    let cooldownBlock = "";
    const cdArr = attrs?.Cooldown;
    if (cdArr && cdArr.some((v) => v > 0)) {
        const tDeck = Math.min(Math.max(tier, 0), 4);
        let cdInnerHtml: string;
        if (opts.mode === "deck") {
            const v = cdArr[tDeck] ?? cdArr[0];
            cdInnerHtml = formatNumericDefaultHtml("Cooldown", v);
        } else {
            const sliced = sliceTierValuesForPoolDisplay(cdArr, minTier);
            const startTier = Math.min(Math.max(minTier, 0), 4);
            if (sliced.length === 0) cdInnerHtml = escapeHtml("?");
            else if (sliced.length === 1 || valuesAllEqual(sliced)) {
                cdInnerHtml = formatNumericDefaultHtml("Cooldown", sliced[0]);
            } else {
                cdInnerHtml = formatLadderColoredHtml("Cooldown", sliced, startTier, false, false);
            }
        }
        cooldownBlock = `<div class="it-cd">冷却时间：${cdInnerHtml} 秒</div>`;
    }

    const descProcessed = preprocessDescForTooltip(item.desc);
    const descHtml = formatDescHtml(
        descProcessed,
        attrs,
        opts.mode,
        tier,
        minTier,
        opts.mode === "deck" ? opts.attrs_override : undefined,
    );

    return `<div class="it-tip"><div class="it-name">${name}</div>${sizeLine}${cooldownBlock}<div class="it-desc">${descHtml}</div></div>`;
}
