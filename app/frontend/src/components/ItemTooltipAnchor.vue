<script setup lang="ts">
import { computed, nextTick, ref } from "vue";
import type { ItemRow } from "@/types";
import { buildItemTooltipHtml } from "@/lib/itemTooltip";

const props = defineProps<{
    item: ItemRow | null | undefined;
    mode: "deck" | "pool";
    /** 卡组内物品当前稀有度档位 0..4 */
    tier?: number;
}>();

const show = ref(false);
const pos = ref({ x: 0, y: 0 });
const floatRef = ref<HTMLElement | null>(null);
/** 水平方向：true = 浮层在指针右侧，false = 在左侧；用滞回避免贴右缘时左右反复翻转 */
const placeTooltipRight = ref(true);

const html = computed(() => {
    if (!props.item) return "";
    return buildItemTooltipHtml(props.item, {
        mode: props.mode,
        tier: props.tier ?? 0,
    });
});

const GAP = 14;
const MARGIN = 10;
/** 从「左侧贴指针」切回「右侧贴指针」时需要的额外余量，防止在阈值附近振荡 */
const HORIZONTAL_PLACE_RIGHT_HYST = 48;

function adjustPosition(clientX: number, clientY: number): void {
    const el = floatRef.value;
    if (!el) return;
    const w = el.offsetWidth;
    const h = el.offsetHeight;
    if (w < 1 || h < 1) return;
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    const overflowRight = clientX + GAP + w > vw - MARGIN;
    const enoughRoomToPreferRight =
        clientX + GAP + w <= vw - MARGIN - HORIZONTAL_PLACE_RIGHT_HYST;

    if (placeTooltipRight.value) {
        if (overflowRight) {
            placeTooltipRight.value = false;
        }
    } else if (enoughRoomToPreferRight) {
        placeTooltipRight.value = true;
    }

    let left = placeTooltipRight.value ? clientX + GAP : clientX - w - GAP;
    let top = clientY + GAP;
    if (left < MARGIN) {
        left = MARGIN;
    }
    if (top + h > vh - MARGIN) {
        top = clientY - h - GAP;
    }
    if (top < MARGIN) {
        top = MARGIN;
    }
    pos.value = { x: left, y: top };
}

async function onEnter(e: MouseEvent): Promise<void> {
    placeTooltipRight.value = true;
    show.value = true;
    pos.value = { x: e.clientX + GAP, y: e.clientY + GAP };
    await nextTick();
    requestAnimationFrame(() => {
        requestAnimationFrame(() => {
            adjustPosition(e.clientX, e.clientY);
        });
    });
}

async function move(e: MouseEvent): Promise<void> {
    await nextTick();
    requestAnimationFrame(() => {
        requestAnimationFrame(() => {
            adjustPosition(e.clientX, e.clientY);
        });
    });
}

function onLeave(): void {
    show.value = false;
}
</script>

<template>
    <div
        v-if="item"
        class="tip-anchor"
        @mouseenter="onEnter"
        @mousemove="move"
        @mouseleave="onLeave"
    >
        <slot />
    </div>
    <div v-else class="tip-anchor tip-anchor--bare">
        <slot />
    </div>
    <Teleport to="body">
        <div
            v-show="show && item"
            ref="floatRef"
            class="item-tooltip-float"
            :style="{ left: pos.x + 'px', top: pos.y + 'px' }"
            v-html="html"
        />
    </Teleport>
</template>

<style>
.item-tooltip-float {
    position: fixed;
    z-index: 10050;
    box-sizing: border-box;
    width: max-content;
    max-width: min(96vw, 960px);
    padding: 0.6rem 0.75rem;
    border-radius: 8px;
    background: #1e2330;
    border: 1px solid #3d4450;
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.45);
    font-size: 0.8rem;
    line-height: 1.45;
    color: #e8eaef;
    pointer-events: none;
    overflow-x: auto;
    overflow-y: hidden;
}
.it-tip .it-name {
    font-weight: 600;
    margin-bottom: 0.35rem;
}
.it-meta {
    display: block;
    font-size: 0.75rem;
    opacity: 0.92;
    margin-bottom: 0.4rem;
}
.it-meta em {
    font-style: italic;
    font-weight: 400;
}
.it-cd {
    font-size: 0.75rem;
    color: #9aa3b2;
    margin-bottom: 0.4rem;
}
.it-desc {
    white-space: pre;
    word-break: normal;
    overflow-wrap: normal;
}
.it-desc .it-ph {
    font-weight: 600;
}
</style>
