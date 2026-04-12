<script setup lang="ts">
import { computed, onUnmounted, ref } from "vue";
import { useCatalogStore } from "@/stores/catalog";
import { useBuilderSession } from "@/stores/builder";
import {
    cycleTier,
    dcardOuterWidthPx,
    itemArtAspectStyle,
    maxSlotsForLevel,
    tierBorderColor,
    usedSlots,
    webpUrl,
} from "@/lib/deckMath";
import {
    attrsOverrideFromDraft,
    draftFromSlot,
    type SlotAttrDraft,
} from "@/lib/deckSlotAttrs";
import { patchDeck, saveDeckSlots } from "@/api";
import ItemTooltipAnchor from "@/components/ItemTooltipAnchor.vue";
import type { DeckSlotEntry } from "@/types";

const props = defineProps<{
    deckId: number | null;
}>();

const emit = defineEmits<{
    /** 保存成功（含等级/槽位写入服务端）后通知父级刷新卡组列表等 */
    saved: [];
}>();

const catalog = useCatalogStore();
const session = useBuilderSession();

const filterHero = ref<string>("all");
const filterSize = ref<string>("all");
const filterTier = ref<string>("all");
const saveMsg = ref<string | null>(null);

/** 双击编辑 Custom/Quest */
const editIndex = ref<number | null>(null);
const editDraft = ref<SlotAttrDraft>({
    custom_0: 0,
    custom_1: 0,
    custom_2: 0,
    custom_3: 0,
    quest: 0,
});

/** 卡组内卡牌拖拽：用于「拖出条带则移除」与条带内 drop 的协调 */
const deckDragFrom = ref<number | null>(null);
const deckDropOnStripHandled = ref(false);
const stripWrapRef = ref<HTMLElement | null>(null);
let lastDeckDragClient = { x: 0, y: 0 };

const budget = computed(() => maxSlotsForLevel(session.editorLevel));
const used = computed(() =>
    usedSlots(session.slots, (name) => catalog.byName.get(name)?.size ?? 1),
);

/** 剩余槽位（与游戏内体型占用一致），用于右侧空槽占位块数量 */
const remainingSlotUnits = computed(() => Math.max(0, budget.value - used.value));

const emptyGhostIndices = computed(() =>
    Array.from({ length: remainingSlotUnits.value }, (_, i) => i),
);

const editSlotTitle = computed(() => {
    const i = editIndex.value;
    if (i === null) return "";
    return session.slots[i]?.item_name ?? "";
});

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
        emit("saved");
    } catch (e) {
        saveMsg.value = e instanceof Error ? e.message : String(e);
    }
}

function onPoolDragStart(itemName: string, ev: DragEvent): void {
    const payload = JSON.stringify({ kind: "pool", itemName });
    ev.dataTransfer?.setData("application/x-bazaar", payload);
    ev.dataTransfer?.setData("text/plain", itemName);
    ev.dataTransfer!.effectAllowed = "copyMove";
}

function onEditorDragOver(ev: DragEvent): void {
    if (deckDragFrom.value !== null) {
        lastDeckDragClient = { x: ev.clientX, y: ev.clientY };
    }
}

function onDeckDragStart(index: number, ev: DragEvent): void {
    deckDragFrom.value = index;
    deckDropOnStripHandled.value = false;
    lastDeckDragClient = { x: ev.clientX, y: ev.clientY };
    const payload = JSON.stringify({ kind: "deck", index });
    ev.dataTransfer?.setData("application/x-bazaar", payload);
    ev.dataTransfer?.setData("text/plain", `deck:${index}`);
    ev.dataTransfer!.effectAllowed = "move";
}

function onDeckDragEnd(): void {
    const from = deckDragFrom.value;
    deckDragFrom.value = null;
    const handled = deckDropOnStripHandled.value;
    deckDropOnStripHandled.value = false;
    if (handled || from === null) return;

    let x = lastDeckDragClient.x;
    let y = lastDeckDragClient.y;
    const el = stripWrapRef.value;
    if (!el) return;
    const r = el.getBoundingClientRect();
    const pad = 4;
    const inside =
        x >= r.left - pad &&
        x <= r.right + pad &&
        y >= r.top - pad &&
        y <= r.bottom + pad;
    if (inside) return;

    const next = session.slots.slice();
    if (from < 0 || from >= next.length) return;
    next.splice(from, 1);
    session.setSlots(next);
}

function parseDrop(ev: DragEvent): { kind: "pool"; itemName: string } | { kind: "deck"; index: number } | null {
    const raw = ev.dataTransfer?.getData("application/x-bazaar");
    if (raw) {
        try {
            return JSON.parse(raw) as
                | { kind: "pool"; itemName: string }
                | { kind: "deck"; index: number };
        } catch {
            /* fall through */
        }
    }
    const plain = ev.dataTransfer?.getData("text/plain")?.trim();
    if (plain && !plain.startsWith("deck:")) {
        return { kind: "pool", itemName: plain };
    }
    if (plain?.startsWith("deck:")) {
        const n = Number(plain.slice("deck:".length));
        if (Number.isInteger(n) && n >= 0) return { kind: "deck", index: n };
    }
    return null;
}

function onStripDragOver(ev: DragEvent): void {
    ev.preventDefault();
}

/**
 * 解析松手位置对应的**目标槽位下标**（0-based）：落在第 i 张实体卡上表示「移动到第 i 张卡的位置」
 *（其余牌相对顺序不变）；返回 `length` 表示队尾 / 虚线空槽。与点在卡左/右无关。
 */
function insertSlotIndexFromStripEvent(strip: HTMLElement, ev: DragEvent): number {
    const n = session.slots.length;
    const x = ev.clientX;
    const y = ev.clientY;
    for (const node of document.elementsFromPoint(x, y)) {
        if (!(node instanceof Element)) continue;
        if (node.closest(".slot-ghost")) return n;
    }
    for (const g of strip.querySelectorAll(".slot-ghost")) {
        const r = g.getBoundingClientRect();
        if (x >= r.left && x <= r.right && y >= r.top && y <= r.bottom) return n;
    }
    const nonTail = [...strip.querySelectorAll(".slot-anchor:not(.tail)")] as HTMLElement[];
    const tailEl = strip.querySelector(".slot-anchor.tail");
    const tailRect = tailEl?.getBoundingClientRect() ?? null;

    if (nonTail.length === 0) return 0;

    if (tailRect && x >= tailRect.left && x <= tailRect.right && y >= tailRect.top && y <= tailRect.bottom)
        return n;

    for (let i = 0; i < nonTail.length; i++) {
        const r = nonTail[i].getBoundingClientRect();
        if (x >= r.left && x <= r.right && y >= r.top && y <= r.bottom) {
            return i;
        }
    }

    if (x < nonTail[0].getBoundingClientRect().left) return 0;

    for (let i = 0; i < nonTail.length - 1; i++) {
        const ra = nonTail[i].getBoundingClientRect();
        const rb = nonTail[i + 1].getBoundingClientRect();
        if (x > ra.right && x < rb.left) return i + 1;
    }

    const lastR = nonTail[nonTail.length - 1].getBoundingClientRect();
    if (tailRect && x > lastR.right && x < tailRect.left) return n;

    if (tailRect && x > tailRect.right) return n;
    if (!tailRect && x > lastR.right) return n;

    let best = n;
    let bestD = Infinity;
    for (let i = 0; i < nonTail.length; i++) {
        const r = nonTail[i].getBoundingClientRect();
        const candidates: [number, number][] = [
            [r.left, i],
            [r.right, Math.min(i + 1, n)],
        ];
        for (const [bx, insertIdx] of candidates) {
            const d = Math.abs(x - bx);
            if (d < bestD) {
                bestD = d;
                best = insertIdx;
            }
        }
    }
    return best;
}

function onStripWrapDrop(ev: DragEvent): void {
    const wrap = stripWrapRef.value;
    if (!wrap) return;
    const strip = wrap.querySelector(".strip");
    if (!strip) return;
    const slotIndex = insertSlotIndexFromStripEvent(strip as HTMLElement, ev);
    handleStripDrop(slotIndex, ev);
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

function handleStripDrop(slotIndex: number, ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    const p = parseDrop(ev);
    if (!p) return;

    const n = session.slots.length;

    if (p.kind === "pool") {
        const it = catalog.byName.get(p.itemName);
        if (!it) return;
        if (used.value + it.size > budget.value) {
            window.alert("槽位不足");
            return;
        }
        const next = session.slots.slice();
        const entry: DeckSlotEntry = { item_name: p.itemName, tier: it.min_tier };
        next.splice(slotIndex, 0, entry);
        session.setSlots(next);
        return;
    }

    if (p.kind === "deck") {
        deckDropOnStripHandled.value = true;
        const from = p.index;
        if (slotIndex === n) {
            moveDeckEntry(from, n);
            return;
        }
        const t = slotIndex;
        if (from === t) return;
        const insertBefore = from < t ? t + 1 : t;
        moveDeckEntry(from, insertBefore);
    }
}

onUnmounted(() => {
    deckDragFrom.value = null;
});

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

function openSlotEditor(index: number): void {
    const s = session.slots[index];
    if (!s) return;
    const it = catalog.byName.get(s.item_name);
    editIndex.value = index;
    editDraft.value = draftFromSlot(s, it, s.tier);
}

function closeSlotEditorCancel(): void {
    editIndex.value = null;
}

function onSlotEditReset(): void {
    const i = editIndex.value;
    if (i === null) return;
    const s = session.slots[i];
    const it = catalog.byName.get(s.item_name);
    editDraft.value = draftFromSlot({ tier: s.tier }, it, s.tier);
}

function draftIsValid(d: SlotAttrDraft): boolean {
    const vals = [d.custom_0, d.custom_1, d.custom_2, d.custom_3, d.quest];
    return vals.every((v) => typeof v === "number" && Number.isFinite(v));
}

function onSlotEditConfirm(): void {
    const i = editIndex.value;
    if (i === null) return;
    if (!draftIsValid(editDraft.value)) {
        window.alert("请输入有效整数。");
        return;
    }
    const s = session.slots[i];
    const it = catalog.byName.get(s.item_name);
    const ao = attrsOverrideFromDraft(it, s.tier, editDraft.value);
    const next = session.slots.slice();
    next[i] = { ...s, attrs_override: ao };
    session.setSlots(next);
    editIndex.value = null;
}
</script>

<template>
    <div class="editor" @dragover.capture="onEditorDragOver">
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

            <div
                ref="stripWrapRef"
                class="strip-wrap"
                @dragenter.prevent
                @dragover="onStripDragOver"
                @drop="onStripWrapDrop"
            >
                <div class="strip">
                    <div
                        v-for="(s, i) in session.slots"
                        :key="`${s.item_name}-${i}`"
                        class="slot-anchor"
                    >
                        <ItemTooltipAnchor
                            :item="catalog.byName.get(s.item_name)"
                            mode="deck"
                            :tier="s.tier"
                            :attrs-override="s.attrs_override"
                        >
                            <div
                                class="dcard"
                                :style="{
                                    width: `${dcardOuterWidthPx(catalog.byName.get(s.item_name)?.size ?? 1)}px`,
                                    flex: '0 0 auto',
                                    borderColor: tierBorderColor(s.tier),
                                }"
                                draggable="true"
                                @dragstart="onDeckDragStart(i, $event)"
                                @dragend="onDeckDragEnd"
                                @dblclick.stop="openSlotEditor(i)"
                                @contextmenu="onCardContextMenu(i, $event)"
                            >
                                <div
                                    class="dcard-art"
                                    :style="itemArtAspectStyle(catalog.byName.get(s.item_name)?.size ?? 1)"
                                >
                                    <img
                                        class="thumb"
                                        draggable="false"
                                        :src="webpUrl(s.item_name)"
                                        :alt="s.item_name"
                                        loading="lazy"
                                        decoding="async"
                                        @error="($event.target as HTMLImageElement).style.opacity = '0.2'"
                                    />
                                </div>
                                <span class="cap">{{ s.item_name }}</span>
                            </div>
                        </ItemTooltipAnchor>
                    </div>
                    <div
                        v-for="i in emptyGhostIndices"
                        :key="'ghost-' + i"
                        class="slot-ghost"
                        aria-hidden="true"
                    >
                        <div
                            class="dcard dcard-ghost"
                            :style="{
                                width: `${dcardOuterWidthPx(1)}px`,
                                flex: '0 0 auto',
                            }"
                        >
                            <div class="dcard-art dcard-art-ghost" :style="itemArtAspectStyle(1)" />
                            <span class="cap cap-ghost" />
                        </div>
                    </div>
                    <div class="slot-anchor tail" />
                </div>
            </div>
            <p class="hint">
                右键物品循环等级（铜～钻）；双击物品可复写 Custom0–3 与 Quest；从下方拖入添加；卡组内拖拽排序；将卡牌拖出上方虚线区域可移除。
            </p>

            <Teleport to="body">
                <div
                    v-if="editIndex !== null"
                    class="slot-attr-overlay"
                    tabindex="-1"
                    @click.self="closeSlotEditorCancel"
                    @keydown.escape="closeSlotEditorCancel"
                >
                    <div class="slot-attr-panel" role="dialog" aria-modal="true" @click.stop>
                        <div class="slot-attr-head">
                            <h3 class="slot-attr-title">物品数值复写</h3>
                            <p class="slot-attr-sub">{{ editSlotTitle }}</p>
                        </div>
                        <div class="slot-attr-fields">
                            <label class="slot-attr-row"
                                ><span>Custom 0</span
                                ><input v-model.number="editDraft.custom_0" type="number" step="1"
                            /></label>
                            <label class="slot-attr-row"
                                ><span>Custom 1</span
                                ><input v-model.number="editDraft.custom_1" type="number" step="1"
                            /></label>
                            <label class="slot-attr-row"
                                ><span>Custom 2</span
                                ><input v-model.number="editDraft.custom_2" type="number" step="1"
                            /></label>
                            <label class="slot-attr-row"
                                ><span>Custom 3</span
                                ><input v-model.number="editDraft.custom_3" type="number" step="1"
                            /></label>
                            <label class="slot-attr-row"
                                ><span>Quest</span
                                ><input v-model.number="editDraft.quest" type="number" step="1"
                            /></label>
                        </div>
                        <div class="slot-attr-actions">
                            <button type="button" class="slot-attr-btn secondary" @click="onSlotEditReset">
                                重置
                            </button>
                            <button type="button" class="slot-attr-btn secondary" @click="closeSlotEditorCancel">
                                取消
                            </button>
                            <button type="button" class="slot-attr-btn primary" @click="onSlotEditConfirm">
                                确定
                            </button>
                        </div>
                    </div>
                </div>
            </Teleport>

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
                <ItemTooltipAnchor v-for="it in filteredPool" :key="it.name" :item="it" mode="pool">
                    <div
                        class="dcard"
                        :style="{
                            width: `${dcardOuterWidthPx(it.size)}px`,
                            borderColor: tierBorderColor(it.min_tier),
                        }"
                        draggable="true"
                        @dragstart="onPoolDragStart(it.name, $event)"
                    >
                        <div class="dcard-art" :style="itemArtAspectStyle(it.size)">
                            <img
                                class="thumb"
                                draggable="false"
                                :src="webpUrl(it.name)"
                                :alt="it.name"
                                loading="lazy"
                                decoding="async"
                                @error="($event.target as HTMLImageElement).style.opacity = '0.2'"
                            />
                        </div>
                        <span class="cap">{{ it.name }}</span>
                    </div>
                </ItemTooltipAnchor>
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
    align-items: flex-start;
    gap: 6px;
    min-height: 0;
    overflow-x: auto;
}
.slot-anchor {
    display: flex;
    min-width: 0;
}
.tail {
    flex: 1;
    min-width: 24px;
}
.slot-ghost {
    display: flex;
    flex-shrink: 0;
    min-width: 0;
}
.dcard.dcard-ghost {
    border-color: #323844;
    background: #1e2229;
    cursor: default;
}
.dcard-art-ghost {
    background: #1a1d23;
}
.cap-ghost {
    min-height: 1.2em;
    padding: 2px 4px 4px;
    box-sizing: border-box;
}
.dcard {
    display: flex;
    flex-direction: column;
    align-items: stretch;
    box-sizing: border-box;
    min-width: 0;
    flex-shrink: 0;
    padding: 0;
    overflow: hidden;
    border: 3px solid;
    border-radius: 8px;
    background: #1a1d24;
    cursor: grab;
}
.dcard-art {
    position: relative;
    width: 100%;
    flex-shrink: 0;
    background: #14171c;
}
.dcard-art .thumb {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
    object-fit: contain;
}
.cap {
    font-size: 0.65rem;
    text-align: center;
    line-height: 1.1;
    max-width: 100%;
    width: 100%;
    padding: 2px 4px 4px;
    box-sizing: border-box;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
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
    display: flex;
    flex-wrap: wrap;
    align-content: flex-start;
    gap: 6px;
    overflow: auto;
    flex: 1;
    min-height: 200px;
    padding: 4px;
    border: 1px solid #2f3540;
    border-radius: 8px;
}

.slot-attr-overlay {
    position: fixed;
    inset: 0;
    z-index: 1200;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.55);
    padding: 1rem;
    box-sizing: border-box;
}
.slot-attr-panel {
    width: min(360px, 100%);
    border-radius: 10px;
    border: 1px solid #3d4450;
    background: #1e2229;
    padding: 1rem 1.1rem;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.45);
}
.slot-attr-head {
    margin-bottom: 0.75rem;
}
.slot-attr-title {
    margin: 0;
    font-size: 1rem;
    font-weight: 600;
    color: #e8eaef;
}
.slot-attr-sub {
    margin: 0.35rem 0 0;
    font-size: 0.85rem;
    color: #9aa3b2;
    word-break: break-all;
}
.slot-attr-fields {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
}
.slot-attr-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.75rem;
    font-size: 0.85rem;
    color: #c4cad6;
}
.slot-attr-row input {
    width: 8rem;
    padding: 0.3rem 0.45rem;
    border-radius: 4px;
    border: 1px solid #3d4450;
    background: #22262e;
    color: #e8eaef;
}
.slot-attr-actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: 0.5rem;
    margin-top: 1rem;
}
.slot-attr-btn {
    padding: 0.35rem 0.75rem;
    border-radius: 6px;
    border: 1px solid #3d4450;
    background: #2a303a;
    color: #e8eaef;
    cursor: pointer;
    font-size: 0.85rem;
}
.slot-attr-btn.primary {
    border-color: #3d6fb8;
    background: #2a4a78;
    color: #fff;
}
.slot-attr-btn.secondary:hover {
    background: #323844;
}
</style>
