import { defineStore } from "pinia";
import { computed, ref } from "vue";
import { fetchItems } from "@/api";
import type { ItemRow } from "@/types";

export const useCatalogStore = defineStore("catalog", () => {
    const items = ref<ItemRow[]>([]);
    const loaded = ref(false);
    const error = ref<string | null>(null);

    const byName = computed(() => {
        const m = new Map<string, ItemRow>();
        for (const it of items.value) m.set(it.name, it);
        return m;
    });

    async function load(): Promise<void> {
        if (loaded.value) return;
        error.value = null;
        try {
            items.value = await fetchItems({});
            loaded.value = true;
        } catch (e) {
            error.value = e instanceof Error ? e.message : String(e);
            throw e;
        }
    }

    return { items, loaded, error, byName, load };
});
