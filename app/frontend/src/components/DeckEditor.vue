<script setup lang="ts">
import { computed, ref } from "vue";
import { useCatalogStore } from "@/stores/catalog";
import { useBuilderSession } from "@/stores/builder";
import {
    cycleTier,
    maxSlotsForLevel,
    tierBorderColor,
    usedSlots,
    webpUrl,
} from "@/lib/deckMath";
import { patchDeck, saveDeckSlots } from "@/api";
import type { DeckSlotEntry } from "@/types";

const props = defineProps<{
    deckId: number | null;
}>();

const catalog = useCatalogStore();
const session = useBuilderSession();

const filterHero = ref<string>("all");
const filterSize = ref<string>("all");
const filterTier = ref<string>("all");
const saveMsg = ref<string | null>(null);

const budget = computed(() => maxSlotsForLevel(session.editorLevel));
const used = computed(() =>
    usedSlots(session.slots, (name) => catalog.byName.get(name)?.size ?? 1),
);

const heroes = computed(() => {
    const s = new Set<string>();
    for (const it of catalog.items) s.add(it.hero);
    return Array.from(s).sort();
});

const filteredPool = computed(() => {
    return catalog.items.filter((it) => {
        if (filterHero.value !== "all" && it.hero !== filterHero.value) return false;
        if (filterSize.value === "small" && it.size !== 1) return false;
        if (filterSize.value === "medium" && it.size !== 2) return false;
        if (filterSize.value === "large" && it.size !== 3) return false;
        if (filterTier.value !== "all" && String(it.min_tier) !== filterTier.value)
            return false;
        return true;
    });
});

function sizeLabel(sz: number): string {
    if (sz === 1) return "小";
    if (sz === 2) return "中";
    return "大";
}

function tierLabel(t: number): string {
    return ["铜", "银", "金", "钻", "传"][t] ?? String(t);
}

function onLevelChange(ev: Event): void {
    const v = Number((ev.target as HTMLSelectElement).value);
    const nextBudget = maxSlotsForLevel(v);
    if (used.value > nextBudget) {
        window.alert("当前占用槽位超过该等级上限，请先调整卡组再切换等级。");
        return;
    }
    session.setEditorLevel(v);
}

async function onSave(): Promise<void> {
    if (props.deckId === null) return;
    saveMsg.value = null;
    try {
        if (session.editorLevel !== session.baselineLevel) {
            await patchDeck(props.deckId, { player_level: session.editorLevel });
        }
        await saveDeckSlots(props.deckId, session.slots);
        session.syncSavedLevel(session.editorLevel);
        saveMsg.value = "已保存";
    } catch (e) {
        saveMsg.value = e instanceof Error ? e.message : String(e);
    }
}

function onPoolDragStart(itemName: string, ev: DragEvent): void {
    ev.dataTransfer?.setData("application/x-bazaar", JSON.stringify({ kind: "pool", itemName }));
    ev.dataTransfer!.effectAllowed = "copyMove";
}

function onDeckDragStart(index: number, ev: DragEvent): void {
    ev.dataTransfer?.setData(
        "application/x-bazaar",
        JSON.stringify({ kind: "deck", index }),
    );
    ev.dataTransfer!.effectAllowed = "move";
}

function parseDrop(ev: DragEvent): { kind: "pool"; itemName: string } | { kind: "deck"; index: number } | null {
    const raw = ev.dataTransfer?.getData("application/x-bazaar");
    if (!raw) return null;
    try {
        return JSON.parse(raw) as
            | { kind: "pool"; itemName: string }
            | { kind: "deck"; index: number };
    } catch {
        return null;
    }
}

function onStripDragOver(ev: DragEvent): void {
    ev.preventDefault();
}

/** 将 `from` 移到「当前位于 insertBefore 的物品之前」的位置（insertBefore 为 length 表示末尾）。 */
function moveDeckEntry(from: number, insertBefore: number): void {
    const next = session.slots.slice();
    if (from < 0 || from >= next.length) return;
    if (from === insertBefore) return;
    const [moved] = next.splice(from, 1);
    let dest = insertBefore;
    if (from < insertBefore) dest -= 1;
    if (dest < 0) dest = 0;
    if (dest > next.length) dest = next.length;
    next.splice(dest, 0, moved);
    session.setSlots(next);
}

function onStripDrop(insertBefore: number, ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    const p = parseDrop(ev);
    if (!p) return;

    if (p.kind === "pool") {
        const it = catalog.byName.get(p.itemName);
        if (!it) return;
        if (used.value + it.size > budget.value) {
            window.alert("槽位不足");
            return;
        }
        const next = session.slots.slice();
        const entry: DeckSlotEntry = { item_name: p.itemName, tier: it.min_tier };
        next.splice(insertBefore, 0, entry);
        session.setSlots(next);
        return;
    }

    if (p.kind === "deck") {
        const from = p.index;
        if (from === insertBefore || from === insertBefore - 1) return;
        moveDeckEntry(from, insertBefore);
    }
}

function onCardContextMenu(index: number, ev: MouseEvent): void {
    ev.preventDefault();
    const e = session.slots[index];
    if (!e) return;
    const it = catalog.byName.get(e.item_name);
    if (!it) return;
    const nt = cycleTier(e.tier, it.min_tier);
    const next = session.slots.slice();
    next[index] = { ...e, tier: nt };
    session.setSlots(next);
}
</script>

<template>
    <div class="editor">
        <div v-if="deckId === null" class="empty">请从左侧选择一个卡组</div>
        <template v-else>
            <div class="top">
                <label class="lvl">
                    等级
                    <select
                        :value="session.editorLevel"
                        @change="onLevelChange"
                    >
                        <option v-for="lv in 20" :key="lv" :value="lv">{{ lv }}</option>
                    </select>
                </label>
                <span class="usage">槽位 {{ used }} / {{ budget }}</span>
                <button type="button" class="save" :disabled="!session.dirty" @click="onSave">
                    保存到数据库
                </button>
                <span v-if="saveMsg" class="msg">{{ saveMsg }}</span>
            </div>

            <div class="strip-wrap" @dragover="onStripDragOver">
                <div class="strip">
                    <div
                        v-for="(s, i) in session.slots"
                        :key="`${s.item_name}-${i}`"
                        class="slot-anchor"
                        @dragover.prevent
                        @drop.stop="onStripDrop(i, $event)"
                    >
                        <div
                            class="dcard"
                            :style="{
                                flex: s ? catalog.byName.get(s.item_name)?.size ?? 1 : 1,
                                borderColor: tierBorderColor(s.tier),
                            }"
                            draggable="true"
                            @dragstart="onDeckDragStart(i, $event)"
                            @contextmenu="onCardContextMenu(i, $event)"
                        >
                            <img
                                class="thumb"
                                :src="webpUrl(s.item_name)"
                                :alt="s.item_name"
                                loading="lazy"
                                decoding="async"
                                @error="($event.target as HTMLImageElement).style.opacity = '0.2'"
                            />
                            <span class="cap">{{ s.item_name }}</span>
                            <span class="tier">{{ tierLabel(s.tier) }}</span>
                        </div>
                    </div>
                    <div
                        class="slot-anchor tail"
                        @dragover.prevent
                        @drop.stop="onStripDrop(session.slots.length, $event)"
                    />
                </div>
            </div>
            <p class="hint">右键物品循环等级（铜～钻）；从下方拖入添加；卡组内拖拽排序。</p>

            <div class="pool-head">
                <span class="fil">
                    英雄
                    <select v-model="filterHero">
                        <option value="all">全部</option>
                        <option v-for="h in heroes" :key="h" :value="h">{{ h }}</option>
                    </select>
                </span>
                <span class="fil">
                    体型
                    <select v-model="filterSize">
                        <option value="all">全部</option>
                        <option value="small">小型</option>
                        <option value="medium">中型</option>
                        <option value="large">大型</option>
                    </select>
                </span>
                <span class="fil">
                    稀有度
                    <select v-model="filterTier">
                        <option value="all">全部</option>
                        <option value="0">铜</option>
                        <option value="1">银</option>
                        <option value="2">金</option>
                        <option value="3">钻</option>
                        <option value="4">传</option>
                    </select>
                </span>
            </div>
            <div class="pool">
                <div
                    v-for="it in filteredPool"
                    :key="it.name"
                    class="pcard"
                    draggable="true"
                    @dragstart="onPoolDragStart(it.name, $event)"
                >
                    <img
                        class="thumb"
                        :src="webpUrl(it.name)"
                        :alt="it.name"
                        loading="lazy"
                        decoding="async"
                        @error="($event.target as HTMLImageElement).style.opacity = '0.2'"
                    />
                    <span class="pname">{{ it.name }}</span>
                    <span class="meta">{{ sizeLabel(it.size) }} · {{ tierLabel(it.min_tier) }}</span>
                </div>
            </div>
        </template>
    </div>
</template>

<style scoped>
.editor {
    display: flex;
    flex-direction: column;
    min-height: 0;
    flex: 1;
    gap: 0.75rem;
}
.empty {
    color: #7a8494;
    padding: 2rem;
}
.top {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.75rem;
}
.lvl select {
    margin-left: 0.35rem;
    padding: 0.25rem 0.5rem;
    border-radius: 4px;
    border: 1px solid #3d4450;
    background: #22262e;
    color: #e8eaef;
}
.usage {
    color: #9aa3b2;
    font-size: 0.9rem;
}
.save {
    padding: 0.35rem 0.75rem;
    border-radius: 6px;
    border: 1px solid #3d6fb8;
    background: #2a4a78;
    color: #fff;
}
.save:disabled {
    opacity: 0.45;
    cursor: not-allowed;
}
.msg {
    font-size: 0.85rem;
    color: #8fd98f;
}
.strip-wrap {
    border: 1px dashed #3d4450;
    border-radius: 8px;
    padding: 0.5rem;
    min-height: 120px;
    background: #22262e;
}
.strip {
    display: flex;
    flex-wrap: nowrap;
    align-items: stretch;
    gap: 6px;
    min-height: 104px;
}
.slot-anchor {
    display: flex;
    min-width: 0;
}
.tail {
    flex: 1;
    min-width: 24px;
}
.dcard {
    display: flex;
    flex-direction: column;
    align-items: center;
    min-width: 56px;
    padding: 4px;
    border: 3px solid;
    border-radius: 8px;
    background: #1a1d24;
    cursor: grab;
}
.thumb {
    width: 48px;
    height: 48px;
    object-fit: contain;
}
.cap {
    font-size: 0.65rem;
    text-align: center;
    line-height: 1.1;
    max-width: 96px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
.tier {
    font-size: 0.6rem;
    color: #9aa3b2;
}
.hint {
    margin: 0;
    font-size: 0.8rem;
    color: #7a8494;
}
.pool-head {
    display: flex;
    flex-wrap: wrap;
    gap: 0.75rem;
    align-items: center;
}
.fil select {
    margin-left: 0.35rem;
    padding: 0.2rem 0.4rem;
    border-radius: 4px;
    border: 1px solid #3d4450;
    background: #22262e;
    color: #e8eaef;
}
.pool {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
    gap: 8px;
    overflow: auto;
    flex: 1;
    min-height: 200px;
    padding: 4px;
    border: 1px solid #2f3540;
    border-radius: 8px;
}
.pcard {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 6px;
    border-radius: 8px;
    background: #262b34;
    cursor: grab;
    border: 1px solid #3d4450;
}
.pcard .thumb {
    width: 56px;
    height: 56px;
}
.pname {
    font-size: 0.65rem;
    text-align: center;
    line-height: 1.1;
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
.meta {
    font-size: 0.6rem;
    color: #8b93a0;
}
</style>
