import { defineStore } from "pinia";
import { ref } from "vue";
import type { DeckSlotEntry } from "@/types";

/** 当前卡组编辑会话（每个卡组集页面一个逻辑实例，用 setup 外 ref 简化） */
export const useBuilderSession = defineStore("builderSession", () => {
    const editorLevel = ref(5);
    const slots = ref<DeckSlotEntry[]>([]);
    const dirty = ref(false);
    const baselineLevel = ref(5);

    /** 物品池筛选（DeckEditor 内使用；切换页面时保留） */
    const filterHero = ref<string>("all");
    const filterSize = ref<string>("all");
    const filterTier = ref<string>("all");

    function resetFromServer(level: number, serverSlots: DeckSlotEntry[]): void {
        editorLevel.value = level;
        baselineLevel.value = level;
        slots.value = serverSlots.map((s) => ({
            item_name: s.item_name,
            tier: s.tier,
            ...(s.attrs_override && Object.keys(s.attrs_override).length > 0
                ? { attrs_override: { ...s.attrs_override } }
                : {}),
        }));
        dirty.value = false;
    }

    function markDirty(): void {
        dirty.value = true;
    }

    function setSlots(next: DeckSlotEntry[]): void {
        slots.value = next;
        dirty.value = true;
    }

    function setEditorLevel(lv: number): void {
        editorLevel.value = lv;
        dirty.value = true;
    }

    function syncSavedLevel(level: number): void {
        baselineLevel.value = level;
        editorLevel.value = level;
        dirty.value = false;
    }

    return {
        editorLevel,
        slots,
        dirty,
        baselineLevel,
        resetFromServer,
        markDirty,
        setSlots,
        setEditorLevel,
        syncSavedLevel,
        filterHero,
        filterSize,
        filterTier,
    };
});
