/** 与后端 legacy Deck.MaxSlotsForLevel 一致 */
export function maxSlotsForLevel(level: number): number {
    if (level <= 1) return 4;
    if (level === 2) return 6;
    if (level === 3) return 8;
    return 10;
}

const TIER_BORDER: readonly string[] = [
    "rgb(180, 98, 65)",
    "rgb(192, 192, 192)",
    "rgb(255, 215, 0)",
    "rgb(0, 255, 255)",
    "rgb(255, 69, 0)",
];

export function tierBorderColor(tier: number): string {
    return TIER_BORDER[Math.min(Math.max(tier, 0), 4)] ?? TIER_BORDER[0];
}

/** 右键循环：minTier～钻石；minTier 为传奇则固定 4 */
export function cycleTier(current: number, minTier: number): number {
    if (minTier >= 4) return 4;
    const lo = minTier;
    const hi = 3;
    if (current < lo) return lo;
    if (current < hi) return current + 1;
    return lo;
}

export function usedSlots(
    entries: { item_name: string }[],
    sizeOf: (name: string) => number,
): number {
    let u = 0;
    for (const e of entries) u += sizeOf(e.item_name);
    return u;
}

export function webpUrl(itemName: string): string {
    return `/static/pictures/webp/${encodeURIComponent(itemName)}.webp`;
}

/** 立绘内容区宽度按体型 1:2:3（对应资源 200 / 400 / 600 宽），单位 px */
const DCARD_CONTENT_WIDTH_UNIT = 50;
/** `.dcard` 左右 tier 边框各 3px（box-sizing: border-box 下需计入外宽） */
const DCARD_BORDER_SIDE = 3;

/**
 * 外宽 = 内容区宽 1:2:3 + 左右边框，使 `.dcard-art` 实际宽度严格 50:100:150，
 * 与 200×400 / 400×400 / 600×400 的 aspect-ratio 组合后立绘区高度一致。
 */
export function dcardOuterWidthPx(size: number): number {
    const s = Math.min(Math.max(Math.floor(size), 1), 3);
    return DCARD_CONTENT_WIDTH_UNIT * s + 2 * DCARD_BORDER_SIDE;
}

/**
 * 物品立绘资源像素尺寸（宽×高）：小 200×400、中 400×400、大 600×400。
 * 在内容区宽度为 50:100:150 时，三种体型立绘区高度均为 100（比例单位），名称行高度一致则整卡对齐。
 */
export function itemArtAspectStyle(size: number): { aspectRatio: string } {
    if (size === 1) return { aspectRatio: "200 / 400" };
    if (size === 2) return { aspectRatio: "400 / 400" };
    if (size === 3) return { aspectRatio: "600 / 400" };
    return { aspectRatio: "1 / 1" };
}
