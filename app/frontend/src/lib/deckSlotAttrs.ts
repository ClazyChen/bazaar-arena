import type { DeckSlotAttrsOverride, ItemRow } from "@/types";

/** 卡组编辑弹窗中的五列数值 */
export interface SlotAttrDraft {
    custom_0: number;
    custom_1: number;
    custom_2: number;
    custom_3: number;
    quest: number;
}

export function tooltipTierValue(
    attrs: ItemRow["tooltip_attrs"],
    engineKey: string,
    tier: number,
): number {
    const arr = attrs?.[engineKey];
    if (!arr || tier < 0 || tier >= arr.length) return 0;
    const v = arr[tier];
    return typeof v === "number" && !Number.isNaN(v) ? v : 0;
}

/** 打开弹窗时：有复写则用复写，否则用该 tier 下物品模板数值 */
export function draftFromSlot(
    slot: { attrs_override?: DeckSlotAttrsOverride; tier: number },
    item: ItemRow | undefined,
    tier: number,
): SlotAttrDraft {
    const t = (key: string) => tooltipTierValue(item?.tooltip_attrs, key, tier);
    return {
        custom_0: slot.attrs_override?.custom_0 ?? t("Custom_0"),
        custom_1: slot.attrs_override?.custom_1 ?? t("Custom_1"),
        custom_2: slot.attrs_override?.custom_2 ?? t("Custom_2"),
        custom_3: slot.attrs_override?.custom_3 ?? t("Custom_3"),
        quest: slot.attrs_override?.quest ?? t("Quest"),
    };
}

/** 确定：仅当某字段与模板不同时写入 attrs_override（全同则 undefined，表示无复写） */
export function attrsOverrideFromDraft(
    item: ItemRow | undefined,
    tier: number,
    draft: SlotAttrDraft,
): DeckSlotAttrsOverride | undefined {
    const pairs: [keyof SlotAttrDraft, string][] = [
        ["custom_0", "Custom_0"],
        ["custom_1", "Custom_1"],
        ["custom_2", "Custom_2"],
        ["custom_3", "Custom_3"],
        ["quest", "Quest"],
    ];
    const out: DeckSlotAttrsOverride = {};
    for (const [dk, tk] of pairs) {
        const tmpl = tooltipTierValue(item?.tooltip_attrs, tk, tier);
        if (draft[dk] !== tmpl) {
            out[dk] = draft[dk];
        }
    }
    return Object.keys(out).length > 0 ? out : undefined;
}
