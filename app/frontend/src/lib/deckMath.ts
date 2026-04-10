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
