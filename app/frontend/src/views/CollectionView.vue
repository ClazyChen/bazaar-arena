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
const currentDeckId = ref<number | null>(null);
const err = ref<string | null>(null);

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

async function trySelectDeck(deckId: number): Promise<void> {
    if (session.dirty) {
        const ok = window.confirm("当前卡组有未保存更改，确定切换？");
        if (!ok) return;
    }
    currentDeckId.value = deckId;
    await loadDeckIntoSession(deckId);
}

async function refreshAndSelect(deckId: number): Promise<void> {
    await loadDecks();
    currentDeckId.value = deckId;
    await loadDeckIntoSession(deckId);
}

onMounted(async () => {
    err.value = null;
    try {
        await catalog.load();
        await loadDecks();
        if (decks.value.length > 0) {
            await trySelectDeck(decks.value[0].id);
        } else {
            currentDeckId.value = null;
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
    const inherit = decks.value.find((d) => d.id === currentDeckId.value);
    const pl = inherit ? inherit.player_level : 5;
    try {
        const row = await createDeck(collectionId.value, n, pl);
        session.resetFromServer(row.player_level, []);
        await loadDecks();
        currentDeckId.value = row.id;
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
    try {
        await deleteDeck(id);
        if (currentDeckId.value === id) {
            currentDeckId.value = null;
            session.resetFromServer(5, []);
        }
        await loadDecks();
        if (decks.value.length > 0 && currentDeckId.value === null) {
            await trySelectDeck(decks.value[0].id);
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
            <span class="title">卡组集 #{{ collectionId }}</span>
        </header>
        <p v-if="err" class="err">{{ err }}</p>
        <div v-else class="split">
            <aside class="side">
                <DeckListPanel
                    :decks="decks"
                    :selected-id="currentDeckId"
                    @select="trySelectDeck"
                    @create="onCreateDeck"
                    @duplicate="onDuplicateDeck"
                    @rename="onRenameDeck"
                    @remove="onRemoveDeck"
                    @reorder="onReorderDeck"
                />
            </aside>
            <main class="main">
                <DeckEditor :deck-id="currentDeckId" />
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
}
.back {
    border: none;
    background: transparent;
    color: #7ab8ff;
    cursor: pointer;
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
