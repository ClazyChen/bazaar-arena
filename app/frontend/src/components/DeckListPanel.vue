<script setup lang="ts">
import { ref } from "vue";
import type { DeckRow } from "@/types";

const props = defineProps<{
    decks: DeckRow[];
    selectedId: number | null;
}>();

const emit = defineEmits<{
    select: [id: number];
    create: [];
    duplicate: [id: number];
    remove: [id: number];
    rename: [id: number];
    reorder: [order: number[]];
}>();

const dragId = ref<number | null>(null);

function label(d: DeckRow): string {
    return `[${d.player_level}] ${d.name}`;
}

function onDragStart(id: number): void {
    dragId.value = id;
}

function onDragEnd(): void {
    dragId.value = null;
}

function onDropOver(targetId: number): void {
    const from = dragId.value;
    dragId.value = null;
    if (from === null || from === targetId) return;
    const ids = props.decks.map((d) => d.id);
    const fi = ids.indexOf(from);
    const ti = ids.indexOf(targetId);
    if (fi < 0 || ti < 0) return;
    const next = [...ids];
    next.splice(fi, 1);
    next.splice(ti, 0, from);
    emit("reorder", next);
}
</script>

<template>
    <div class="panel">
        <div class="toolbar">
            <button type="button" class="btn" @click="emit('create')">新建</button>
        </div>
        <ul class="list">
            <li
                v-for="d in decks"
                :key="d.id"
                class="row"
                :class="{ active: selectedId === d.id }"
                draggable="true"
                @dragstart="onDragStart(d.id)"
                @dragend="onDragEnd"
                @dragover.prevent
                @drop="onDropOver(d.id)"
            >
                <button type="button" class="row-main" @click="emit('select', d.id)">
                    {{ label(d) }}
                </button>
                <span class="ops">
                    <button type="button" class="mini" title="复制" @click.stop="emit('duplicate', d.id)">
                        ⧉
                    </button>
                    <button type="button" class="mini" title="重命名" @click.stop="emit('rename', d.id)">
                        ✎
                    </button>
                    <button type="button" class="mini danger" title="删除" @click.stop="emit('remove', d.id)">
                        ✕
                    </button>
                </span>
            </li>
        </ul>
    </div>
</template>

<style scoped>
.panel {
    display: flex;
    flex-direction: column;
    height: 100%;
    min-height: 0;
}
.toolbar {
    padding: 0.5rem 0.75rem;
    border-bottom: 1px solid #2f3540;
}
.btn {
    padding: 0.35rem 0.75rem;
    border-radius: 6px;
    border: 1px solid #3d4450;
    background: #2a3038;
    color: #e8eaef;
}
.list {
    list-style: none;
    margin: 0;
    padding: 0.25rem;
    overflow: auto;
    flex: 1;
}
.row {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    border-radius: 6px;
    margin-bottom: 2px;
}
.row.active {
    background: #2a3648;
}
.row-main {
    flex: 1;
    text-align: left;
    padding: 0.4rem 0.5rem;
    border: none;
    background: transparent;
    color: inherit;
    cursor: pointer;
}
.ops {
    display: flex;
    gap: 2px;
    padding-right: 0.25rem;
}
.mini {
    border: none;
    background: transparent;
    color: #9aa3b2;
    cursor: pointer;
    padding: 0.15rem 0.25rem;
    font-size: 0.85rem;
}
.mini:hover {
    color: #e8eaef;
}
.danger:hover {
    color: #f88;
}
</style>
