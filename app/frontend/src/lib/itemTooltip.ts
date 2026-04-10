import type { ItemRow } from "@/types";
import { tierBorderColor } from "@/lib/deckMath";

/** 与 engine/cli ColorizeSummaryLine 关键词配色一致；仅包裹匹配到的子串 */
const KEYWORD_COLORS: { needle: string; color: string }[] = [
    { needle: "治疗", color: "rgb(97, 176, 60)" },
    { needle: "生命上限", color: "rgb(97, 176, 60)" },
    { needle: "生命再生", color: "rgb(142, 234, 49)" },
    { needle: "弹药", color: "rgb(255, 142, 0)" },
    { needle: "装填", color: "rgb(255, 142, 0)" },
    { needle: "加速", color: "rgb(0, 236, 195)" },
    { needle: "充能", color: "rgb(0, 236, 195)" },
    { needle: "冻结", color: "rgb(63, 200, 247)" },
    { needle: "护盾", color: "rgb(244, 207, 32)" },
    { needle: "飞行", color: "rgb(244, 207, 32)" },
    { needle: "伤害", color: "rgb(245, 80, 61)" },
    { needle: "暴击率", color: "rgb(245, 80, 61)" },
    { needle: "暴击伤害", color: "rgb(245, 80, 61)" },
    { needle: "摧毁", color: "rgb(255, 50, 120)" },
    { needle: "剧毒", color: "rgb(14, 190, 79)" },
    { needle: "修复", color: "rgb(143, 252, 188)" },
    { needle: "减速", color: "rgb(203, 159, 110)" },
    { needle: "灼烧", color: "rgb(255, 159, 69)" },
];

const MS_DISPLAY_KEYS = new Set(["Cooldown", "Haste", "Slow", "Freeze", "Charge"]);

/** Desc 占位符中的别名 → tooltip_attrs / ItemKey */
const DESC_FIELD_ALIASES: Record<string, string> = {
    ChargeSeconds: "Charge",
    SlowSeconds: "Slow",
    FreezeSeconds: "Freeze",
    HasteSeconds: "Haste",
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

/** Desc 占位符内展示的 HTML（已含上色与前后缀） */
function formatPlaceholderValueHtml(
    key: string,
    arr: number[] | null,
    mode: "deck" | "pool",
    tier: number,
    minTier: number,
    leadingPlus: boolean,
    trailingPercent: boolean,
): string {
    if (!arr || arr.length === 0) {
        return escapeHtml("—");
    }
    const t = Math.min(Math.max(tier, 0), 4);
    if (mode === "deck") {
        const v = arr[t] ?? arr[0];
        const core = valuesAllEqual(arr)
            ? formatNumericDefaultHtml(key, v)
            : formatNumericTierHtml(key, v, t);
        return wrapAffixes(core, leadingPlus, trailingPercent);
    }
    const sliced = sliceTierValuesForPoolDisplay(arr, minTier);
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
        const innerHtml = arr
            ? formatPlaceholderValueHtml(key, arr, mode, tier, minTier, leadingPlus, trailingPercent)
            : escapeHtml("—");
        out += `<span class="it-ph">${innerHtml}</span>`;
        last = m.index + m[0].length;
    }
    out += colorizePlainText(desc.slice(last));
    return out;
}

export function buildItemTooltipHtml(
    item: ItemRow,
    opts: { mode: "deck" | "pool"; tier?: number },
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
            cdInnerHtml = valuesAllEqual(cdArr)
                ? formatNumericDefaultHtml("Cooldown", v)
                : formatNumericTierHtml("Cooldown", v, tDeck);
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
    const descHtml = formatDescHtml(descProcessed, attrs, opts.mode, tier, minTier);

    return `<div class="it-tip"><div class="it-name">${name}</div>${sizeLine}${cooldownBlock}<div class="it-desc">${descHtml}</div></div>`;
}
