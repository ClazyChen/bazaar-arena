<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { createCollection, fetchCollections } from "@/api";
import type { CollectionRow } from "@/types";

const router = useRouter();
const collections = ref<CollectionRow[]>([]);
const err = ref<string | null>(null);
const loading = ref(true);

onMounted(async () => {
    try {
        collections.value = await fetchCollections();
    } catch (e) {
        err.value = e instanceof Error ? e.message : String(e);
    } finally {
        loading.value = false;
    }
});

function openCollection(id: number): void {
    router.push({ name: "collection", params: { id: String(id) } });
}

async function onCreate(): Promise<void> {
    const name = window.prompt("新卡组集名称", "我的卡组集");
    if (name === null) return;
    const n = name.trim() || "未命名卡组集";
    try {
        const row = await createCollection(n);
        collections.value = await fetchCollections();
        openCollection(row.id);
    } catch (e) {
        err.value = e instanceof Error ? e.message : String(e);
    }
}
</script>

<template>
    <div class="home">
        <header class="head">
            <h1>Bazaar Arena</h1>
            <p class="sub">选择一个卡组集进入编辑，或新建卡组集。</p>
        </header>
        <p v-if="loading" class="muted">加载中…</p>
        <p v-else-if="err" class="err">{{ err }}</p>
        <div v-else class="panel">
            <button type="button" class="primary" @click="onCreate">新建卡组集</button>
            <ul v-if="collections.length" class="list">
                <li v-for="c in collections" :key="c.id">
                    <button type="button" class="linkish" @click="openCollection(c.id)">
                        {{ c.name }}
                    </button>
                </li>
            </ul>
            <p v-else class="muted">暂无卡组集，请先新建。</p>
        </div>
    </div>
</template>

<style scoped>
.home {
    max-width: 560px;
    margin: 0 auto;
    padding: 2rem 1rem;
}
.head h1 {
    margin: 0 0 0.5rem;
    font-weight: 600;
}
.sub {
    margin: 0 0 1.5rem;
    color: #9aa3b2;
    font-size: 0.95rem;
}
.muted {
    color: #7a8494;
}
.err {
    color: #f88;
}
.panel {
    display: flex;
    flex-direction: column;
    gap: 1rem;
}
.primary {
    align-self: flex-start;
    padding: 0.45rem 1rem;
    border-radius: 6px;
    border: 1px solid #3d6fb8;
    background: #2a4a78;
    color: #fff;
}
.linkish {
    background: none;
    border: none;
    color: #7ab8ff;
    padding: 0.25rem 0;
    text-align: left;
}
.list {
    list-style: none;
    padding: 0;
    margin: 0;
}
</style>
