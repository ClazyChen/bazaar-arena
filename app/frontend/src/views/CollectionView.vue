<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import {
    createDeck,
    deleteDeck,
    duplicateDeck,
    fetchDeckSlots,
    fetchDecks,
    patchDeck,
    reorderDecks,
} from "@/api";
import BattleSimPanel from "@/components/BattleSimPanel.vue";
import DeckEditor from "@/components/DeckEditor.vue";
import DeckListPanel from "@/components/DeckListPanel.vue";
import { useBuilderSession } from "@/stores/builder";
import { useCatalogStore } from "@/stores/catalog";
import type { DeckRow } from "@/types";

const props = defineProps<{
    id: string;
}>();

const router = useRouter();
const catalog = useCatalogStore();
const session = useBuilderSession();

const collectionId = computed(() => Number(props.id));
const decks = ref<DeckRow[]>([]);
/** 卡组编辑模式下当前选中的卡组 */
const editorDeckId = ref<number | null>(null);
const viewMode = ref<"edit" | "battle">("edit");
/** 进入对战时锁定为玩家1 */
const p1DeckId = ref<number | null>(null);
/** 对战模式下左侧列表切换玩家2 */
const p2DeckId = ref<number | null>(null);
const err = ref<string | null>(null);

const listSelectedId = computed(() =>
    viewMode.value === "battle" ? p2DeckId.value : editorDeckId.value,
);

async function loadDecks(): Promise<void> {
    const cid = collectionId.value;
    if (Number.isNaN(cid)) return;
    decks.value = await fetchDecks(cid);
}

async function loadDeckIntoSession(deckId: number): Promise<void> {
    const data = await fetchDeckSlots(deckId);
    const entries = data.slots
        .sort((a, b) => a.position - b.position)
        .map((s) => ({ item_name: s.item_name, tier: s.tier }));
    session.resetFromServer(data.player_level, entries);
}

function deckLabel(id: number | null): string | undefined {
    if (id === null) return undefined;
    return decks.value.find((d) => d.id === id)?.name;
}

async function selectDeckForEdit(deckId: number): Promise<void> {
    if (session.dirty) {
        const ok = window.confirm("当前卡组有未保存更改，确定切换？");
        if (!ok) return;
    }
    editorDeckId.value = deckId;
    await loadDeckIntoSession(deckId);
}

function onSelectDeckFromList(deckId: number): void {
    if (viewMode.value === "battle") {
        p2DeckId.value = deckId;
        return;
    }
    void selectDeckForEdit(deckId);
}

function switchToEdit(): void {
    viewMode.value = "edit";
}

function enterBattle(): void {
    if (editorDeckId.value === null) {
        window.alert("请先选择一个卡组");
        return;
    }
    p1DeckId.value = editorDeckId.value;
    p2DeckId.value = editorDeckId.value;
    viewMode.value = "battle";
}

async function refreshAndSelect(deckId: number): Promise<void> {
    await loadDecks();
    editorDeckId.value = deckId;
    await loadDeckIntoSession(deckId);
}

onMounted(async () => {
    err.value = null;
    try {
        await catalog.load();
        await loadDecks();
        if (decks.value.length > 0) {
            await selectDeckForEdit(decks.value[0].id);
        } else {
            editorDeckId.value = null;
            session.resetFromServer(5, []);
        }
    } catch (e) {
        err.value = e instanceof Error ? e.message : String(e);
    }
});

function goHome(): void {
    if (session.dirty) {
        const ok = window.confirm("有未保存更改，确定返回？");
        if (!ok) return;
    }
    router.push({ name: "home" });
}

async function onCreateDeck(): Promise<void> {
    if (session.dirty) {
        const ok = window.confirm("当前卡组有未保存更改，确定新建？");
        if (!ok) return;
    }
    const name = window.prompt("卡组名称", "新卡组");
    if (name === null) return;
    const n = name.trim() || "新卡组";
    const inherit = decks.value.find((d) => d.id === editorDeckId.value);
    const pl = inherit ? inherit.player_level : 5;
    try {
        const row = await createDeck(collectionId.value, n, pl);
        session.resetFromServer(row.player_level, []);
        await loadDecks();
        editorDeckId.value = row.id;
    } catch (e) {
        window.alert(e instanceof Error ? e.message : String(e));
    }
}

async function onDuplicateDeck(id: number): Promise<void> {
    try {
        const row = await duplicateDeck(id);
        await refreshAndSelect(row.id);
    } catch (e) {
        window.alert(e instanceof Error ? e.message : String(e));
    }
}

async function onRenameDeck(id: number): Promise<void> {
    const d = decks.value.find((x) => x.id === id);
    if (!d) return;
    const name = window.prompt("重命名卡组", d.name);
    if (name === null) return;
    const n = name.trim();
    if (!n) return;
    try {
        await patchDeck(id, { name: n });
        await loadDecks();
    } catch (e) {
        window.alert(e instanceof Error ? e.message : String(e));
    }
}

async function onRemoveDeck(id: number): Promise<void> {
    if (!window.confirm("确定删除该卡组？")) return;
    const wasP1 = viewMode.value === "battle" && id === p1DeckId.value;
    const wasP2 = viewMode.value === "battle" && id === p2DeckId.value;
    const wasEditor = editorDeckId.value === id;
    try {
        await deleteDeck(id);
        await loadDecks();

        if (wasP1) {
            viewMode.value = "edit";
            p1DeckId.value = null;
            p2DeckId.value = null;
        } else if (wasP2) {
            p2DeckId.value = p1DeckId.value ?? decks.value[0]?.id ?? null;
        }

        if (wasEditor) {
            editorDeckId.value = null;
            session.resetFromServer(5, []);
            if (decks.value.length > 0) {
                await selectDeckForEdit(decks.value[0].id);
            }
        }
    } catch (e) {
        window.alert(e instanceof Error ? e.message : String(e));
    }
}

async function onReorderDeck(order: number[]): Promise<void> {
    try {
        await reorderDecks(collectionId.value, order);
        await loadDecks();
    } catch (e) {
        window.alert(e instanceof Error ? e.message : String(e));
    }
}
</script>

<template>
    <div class="page">
        <header class="bar">
            <button type="button" class="back" @click="goHome">← 主页</button>
            <div class="mode-nav" aria-label="界面切换">
                <button
                    type="button"
                    class="mode-btn"
                    :class="{ active: viewMode === 'edit' }"
                    title="卡组编辑"
                    @click="switchToEdit"
                >
                    ‹
                </button>
                <span class="mode-label">{{
                    viewMode === "edit" ? "卡组编辑" : "对战模拟"
                }}</span>
                <button
                    type="button"
                    class="mode-btn"
                    :class="{ active: viewMode === 'battle' }"
                    title="对战模拟"
                    @click="enterBattle"
                >
                    ›
                </button>
            </div>
            <span class="title">卡组集 #{{ collectionId }}</span>
        </header>
        <p v-if="err" class="err">{{ err }}</p>
        <div v-else class="split">
            <aside class="side">
                <DeckListPanel
                    :decks="decks"
                    :selected-id="listSelectedId"
                    @select="onSelectDeckFromList"
                    @create="onCreateDeck"
                    @duplicate="onDuplicateDeck"
                    @rename="onRenameDeck"
                    @remove="onRemoveDeck"
                    @reorder="onReorderDeck"
                />
            </aside>
            <main class="main">
                <DeckEditor v-if="viewMode === 'edit'" :deck-id="editorDeckId" @saved="loadDecks" />
                <BattleSimPanel
                    v-else
                    :deck-id-p1="p1DeckId"
                    :deck-id-p2="p2DeckId"
                    :p1-deck-name="deckLabel(p1DeckId)"
                    :p2-deck-name="deckLabel(p2DeckId)"
                />
            </main>
        </div>
    </div>
</template>

<style scoped>
.page {
    display: flex;
    flex-direction: column;
    height: 100%;
    min-height: 0;
}
.bar {
    display: flex;
    align-items: center;
    gap: 1rem;
    padding: 0.5rem 1rem;
    border-bottom: 1px solid #2f3540;
    flex-shrink: 0;
    flex-wrap: wrap;
}
.back {
    border: none;
    background: transparent;
    color: #7ab8ff;
    cursor: pointer;
}
.mode-nav {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
    padding: 0.15rem 0.5rem;
    border-radius: 8px;
    border: 1px solid #3d4450;
    background: #22262e;
}
.mode-btn {
    border: none;
    background: transparent;
    color: #9aa3b2;
    font-size: 1.35rem;
    line-height: 1;
    padding: 0 0.25rem;
    cursor: pointer;
    border-radius: 4px;
}
.mode-btn:hover {
    color: #e8eaef;
    background: #2f3540;
}
.mode-btn.active {
    color: #7ab8ff;
}
.mode-label {
    font-size: 0.85rem;
    color: #c5cad3;
    min-width: 4.5rem;
    text-align: center;
}
.title {
    color: #9aa3b2;
    font-size: 0.95rem;
}
.err {
    color: #f88;
    padding: 0.5rem 1rem;
    margin: 0;
}
.split {
    display: flex;
    flex: 1;
    min-height: 0;
}
.side {
    width: 280px;
    flex-shrink: 0;
    border-right: 1px solid #2f3540;
    display: flex;
    flex-direction: column;
    min-height: 0;
}
.main {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-width: 0;
    padding: 0.75rem 1rem;
    min-height: 0;
}
</style>
