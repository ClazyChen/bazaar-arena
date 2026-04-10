<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from "vue";
import { fetchDeckSlots, fetchReproJobJson, postSimulate } from "@/api";
import {
    dcardOuterWidthPx,
    itemArtAspectStyle,
    tierBorderColor,
    webpUrl,
} from "@/lib/deckMath";
import {
    BATTLE_TICK_MS,
    buildPlaybackTimeline,
    formatTimeSec,
    type PlaybackStep,
} from "@/lib/battleSim";
import { useCatalogStore } from "@/stores/catalog";
import ItemTooltipAnchor from "@/components/ItemTooltipAnchor.vue";
import type { DeckSlotPayload, FrameEndSideSnapshot, SimulateCliEnvelope } from "@/types";

const props = defineProps<{
    deckIdP1: number | null;
    deckIdP2: number | null;
    p1DeckName?: string;
    p2DeckName?: string;
}>();

const catalog = useCatalogStore();

const slotsP1 = ref<DeckSlotPayload[]>([]);
const slotsP2 = ref<DeckSlotPayload[]>([]);
const slotsErr = ref<string | null>(null);
const slotsLoading = ref(false);

const simEnvelope = ref<SimulateCliEnvelope | null>(null);
const simErr = ref<string | null>(null);
const battleLoading = ref(false);

/** 最近一次成功 detailed 模拟的种子，用于 Summary 与 job 下载 */
const lastUsedSeed = ref<number | null>(null);
const summaryLines = ref<string[]>([]);
const summaryErr = ref<string | null>(null);
const summaryLoading = ref(false);

const timeline = computed((): PlaybackStep[] => {
    const env = simEnvelope.value;
    if (!env?.ok || !env.result) return [];
    return buildPlaybackTimeline(env.result.debug?.events, env.result);
});

const playheadIndex = ref(0);
const playing = ref(false);

let tickTimer: ReturnType<typeof setInterval> | null = null;

function stopTick(): void {
    if (tickTimer !== null) {
        clearInterval(tickTimer);
        tickTimer = null;
    }
}

function startTick(): void {
    stopTick();
    tickTimer = setInterval(() => {
        const steps = timeline.value;
        if (steps.length === 0) {
            playing.value = false;
            return;
        }
        const maxI = steps.length - 1;
        if (playheadIndex.value >= maxI) {
            playing.value = false;
            return;
        }
        playheadIndex.value += 1;
    }, BATTLE_TICK_MS);
}

watch(playing, (p) => {
    if (p) startTick();
    else stopTick();
});

onUnmounted(() => stopTick());

const maxPlayhead = computed(() => Math.max(0, timeline.value.length - 1));

const currentTMs = computed(() => timeline.value[playheadIndex.value]?.tMs ?? 0);
const endTMs = computed(() => timeline.value[timeline.value.length - 1]?.tMs ?? 0);

watch(timeline, (steps) => {
    playheadIndex.value = 0;
    playing.value = false;
    if (steps.length === 0) return;
});

watch(maxPlayhead, (m) => {
    if (playheadIndex.value > m) playheadIndex.value = m;
});

const currentSides = computed((): FrameEndSideSnapshot[] | null => {
    const steps = timeline.value;
    if (steps.length === 0) return null;
    const i = Math.min(Math.max(0, playheadIndex.value), steps.length - 1);
    return steps[i].sides;
});

const side0 = computed(() => currentSides.value?.[0] ?? null);
const side1 = computed(() => currentSides.value?.[1] ?? null);

const hasBattleResult = computed(
    () => Boolean(simEnvelope.value?.ok && timeline.value.length > 0),
);

function outcomeForSide(sideIndex: 0 | 1): { text: string; cls: string } | null {
    const r = simEnvelope.value?.result;
    if (!r || !simEnvelope.value?.ok) return null;
    if (r.isDraw) return { text: "平局", cls: "tag-draw" };
    if (r.winner === sideIndex) return { text: "胜利", cls: "tag-win" };
    return { text: "失败", cls: "tag-lose" };
}

const p1Outcome = computed(() => outcomeForSide(0));
const p2Outcome = computed(() => outcomeForSide(1));

watch(
    () => [props.deckIdP1, props.deckIdP2] as const,
    async ([id1, id2]) => {
        slotsErr.value = null;
        simErr.value = null;
        simEnvelope.value = null;
        lastUsedSeed.value = null;
        summaryLines.value = [];
        summaryErr.value = null;
        slotsP1.value = [];
        slotsP2.value = [];
        playheadIndex.value = 0;
        playing.value = false;
        if (id1 === null || id2 === null) return;
        slotsLoading.value = true;
        try {
            const [a, b] = await Promise.all([fetchDeckSlots(id1), fetchDeckSlots(id2)]);
            slotsP1.value = a.slots.sort((x, y) => x.position - y.position);
            slotsP2.value = b.slots.sort((x, y) => x.position - y.position);
        } catch (e) {
            slotsErr.value = e instanceof Error ? e.message : String(e);
        } finally {
            slotsLoading.value = false;
        }
    },
    { immediate: true },
);

async function runBattle(): Promise<void> {
    const id1 = props.deckIdP1;
    const id2 = props.deckIdP2;
    if (id1 === null || id2 === null) return;
    battleLoading.value = true;
    simErr.value = null;
    simEnvelope.value = null;
    lastUsedSeed.value = null;
    summaryLines.value = [];
    summaryErr.value = null;
    try {
        const env = await postSimulate({
            deck_id_0: id1,
            deck_id_1: id2,
            debug_level: "detailed",
        });
        simEnvelope.value = env;
        if (!env.ok) {
            simErr.value = env.error || "模拟失败";
            return;
        }
        if (typeof env.usedSeed === "number") {
            lastUsedSeed.value = env.usedSeed;
        }
    } catch (e) {
        simErr.value = e instanceof Error ? e.message : String(e);
    } finally {
        battleLoading.value = false;
    }
}

async function fetchSummaryLog(): Promise<void> {
    const id1 = props.deckIdP1;
    const id2 = props.deckIdP2;
    const seed = lastUsedSeed.value;
    if (id1 === null || id2 === null || seed === null) return;
    summaryLoading.value = true;
    summaryErr.value = null;
    try {
        const env = await postSimulate({
            deck_id_0: id1,
            deck_id_1: id2,
            seed,
            debug_level: "summary",
        });
        if (!env.ok) {
            summaryErr.value = env.error || "Summary 请求失败";
            summaryLines.value = [];
            return;
        }
        const lines = env.result?.debug?.lines;
        summaryLines.value = Array.isArray(lines) ? lines : [];
    } catch (e) {
        summaryErr.value = e instanceof Error ? e.message : String(e);
        summaryLines.value = [];
    } finally {
        summaryLoading.value = false;
    }
}

async function downloadReproJob(dl: "summary" | "detailed"): Promise<void> {
    const id1 = props.deckIdP1;
    const id2 = props.deckIdP2;
    const seed = lastUsedSeed.value;
    if (id1 === null || id2 === null || seed === null) return;
    try {
        const { job } = await fetchReproJobJson({
            deck_id_0: id1,
            deck_id_1: id2,
            seed,
            debug_level: dl,
        });
        const blob = new Blob([JSON.stringify(job, null, 2)], {
            type: "application/json;charset=utf-8",
        });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `bazaararena-repro-${dl}-seed-${seed}.json`;
        a.click();
        URL.revokeObjectURL(url);
    } catch (e) {
        summaryErr.value = e instanceof Error ? e.message : String(e);
    }
}

async function copySeed(): Promise<void> {
    const s = lastUsedSeed.value;
    if (s === null) return;
    try {
        await navigator.clipboard.writeText(String(s));
    } catch {
        summaryErr.value = "复制种子失败";
    }
}

function togglePlay(): void {
    if (timeline.value.length === 0) return;
    playing.value = !playing.value;
}

function goStart(): void {
    playheadIndex.value = 0;
    playing.value = false;
}

function goEnd(): void {
    const m = maxPlayhead.value;
    playheadIndex.value = m;
    playing.value = false;
}

function stepPrev(): void {
    playing.value = false;
    playheadIndex.value = Math.max(0, playheadIndex.value - 1);
}

function stepNext(): void {
    playing.value = false;
    playheadIndex.value = Math.min(maxPlayhead.value, playheadIndex.value + 1);
}

function onScrubInput(): void {
    playing.value = false;
}

function onScrubStart(): void {
    playing.value = false;
}

function hpFrac(s: FrameEndSideSnapshot | null): number {
    if (!s || s.maxHp <= 0) return 0;
    return Math.max(0, Math.min(1, s.hp / s.maxHp));
}

function shieldFrac(s: FrameEndSideSnapshot | null): number {
    if (!s || s.maxHp <= 0) return 0;
    return Math.max(0, Math.min(1, s.shield / s.maxHp));
}
</script>

<template>
    <div class="battle">
        <p v-if="deckIdP1 === null || deckIdP2 === null" class="empty">卡组未就绪</p>
        <template v-else>
            <div class="battle-bar">
                <button
                    type="button"
                    class="battle-btn"
                    :disabled="slotsLoading || battleLoading"
                    @click="runBattle"
                >
                    {{ hasBattleResult ? "再次对战" : "开始对战" }}
                </button>
                <span v-if="slotsLoading" class="hint">加载卡组…</span>
                <span v-else-if="battleLoading" class="hint">模拟中…</span>
            </div>
            <p v-if="slotsErr" class="err">{{ slotsErr }}</p>
            <p v-else-if="simErr" class="err">{{ simErr }}</p>
            <p
                v-if="
                    simEnvelope?.ok &&
                    (simEnvelope?.bazaararenaCliVersion || simEnvelope?.bazaararenaCli)
                "
                class="cli-meta"
            >
                <span class="cli-meta-label">模拟器</span>
                <code class="cli-meta-ver">{{ simEnvelope?.bazaararenaCliVersion ?? "—" }}</code>
                <span v-if="simEnvelope?.bazaararenaCli" class="cli-meta-path" :title="simEnvelope.bazaararenaCli">{{
                    simEnvelope.bazaararenaCli
                }}</span>
            </p>

            <details v-if="lastUsedSeed !== null" class="debug-loop" open>
                <summary class="debug-sum">调试：Summary 与 CLI 复现（同种子）</summary>
                <p class="debug-hint">
                    闭环：在动画里发现问题 → 点「拉取 Summary 日志」阅读与引擎
                    <code>debug.level=summary</code>
                    一致的可读文本；本地将下载的
                    <code>job.json</code>
                    交给
                    <code>bazaararena_cli --input job.json --output out.json</code>
                    ，在生成 JSON 的
                    <code>result.debug.lines</code>
                    中核对。修改 C++ 后重编 CLI，再回到本页「再次对战」验证。
                </p>
                <div class="debug-row">
                    <span class="seed-label">种子 {{ lastUsedSeed }}</span>
                    <button type="button" class="btn-sm" @click="copySeed">复制种子</button>
                    <button
                        type="button"
                        class="btn-sm primary"
                        :disabled="summaryLoading"
                        @click="fetchSummaryLog"
                    >
                        {{ summaryLoading ? "拉取中…" : "拉取 Summary 日志" }}
                    </button>
                    <button type="button" class="btn-sm" @click="downloadReproJob('summary')">
                        下载 job（summary）
                    </button>
                    <button type="button" class="btn-sm" @click="downloadReproJob('detailed')">
                        下载 job（detailed）
                    </button>
                </div>
                <p v-if="summaryErr" class="err">{{ summaryErr }}</p>
                <pre v-if="summaryLines.length" class="summary-pre">{{ summaryLines.join("\n") }}</pre>
            </details>

            <div v-if="hasBattleResult" class="player-shell">
                <div class="player-time">
                    <span class="time-val">{{ formatTimeSec(currentTMs) }}</span>
                    <span class="time-sep">/</span>
                    <span class="time-end">{{ formatTimeSec(endTMs) }}</span>
                    <span class="time-unit">秒</span>
                </div>
                <div class="player-controls">
                    <button
                        type="button"
                        class="icon-btn"
                        title="回到起点"
                        :disabled="timeline.length === 0"
                        @click="goStart"
                    >
                        <span class="glyph" aria-hidden="true">⏮</span>
                    </button>
                    <button
                        type="button"
                        class="icon-btn"
                        title="上一时刻"
                        :disabled="timeline.length === 0"
                        @click="stepPrev"
                    >
                        <span class="glyph" aria-hidden="true">⏴</span>
                    </button>
                    <button
                        type="button"
                        class="icon-btn primary"
                        :title="playing ? '暂停' : '播放'"
                        :disabled="timeline.length === 0"
                        @click="togglePlay"
                    >
                        <span class="glyph" aria-hidden="true">{{ playing ? "⏸" : "▶" }}</span>
                    </button>
                    <button
                        type="button"
                        class="icon-btn"
                        title="下一时刻"
                        :disabled="timeline.length === 0"
                        @click="stepNext"
                    >
                        <span class="glyph" aria-hidden="true">⏵</span>
                    </button>
                    <button
                        type="button"
                        class="icon-btn"
                        title="跳到结局"
                        :disabled="timeline.length === 0"
                        @click="goEnd"
                    >
                        <span class="glyph" aria-hidden="true">⏭</span>
                    </button>
                </div>
                <div class="scrub-wrap">
                    <input
                        v-model.number="playheadIndex"
                        class="scrub"
                        type="range"
                        :min="0"
                        :max="maxPlayhead"
                        step="1"
                        :disabled="timeline.length === 0"
                        @pointerdown="onScrubStart"
                        @input="onScrubInput"
                    />
                </div>
            </div>

            <section class="side-block p1">
                <header class="side-h">
                    <span>{{ p1DeckName ?? "玩家1" }}（锁定）</span>
                    <span v-if="p1Outcome" class="outcome" :class="p1Outcome.cls">{{ p1Outcome.text }}</span>
                </header>
                <div v-if="side0" class="hud">
                    <div class="nums">
                        <span class="hp-white">{{ Math.round(side0.hp) }}</span>
                        <span v-if="side0.burn > 0" class="burn">灼{{ Math.round(side0.burn) }}</span>
                        <span v-if="side0.poison > 0" class="poison">毒{{ Math.round(side0.poison) }}</span>
                        <span v-if="side0.regen > 0" class="regen">再{{ Math.round(side0.regen) }}</span>
                    </div>
                    <div class="bars">
                        <div v-if="side0.shield > 0" class="bar shield-bar">
                            <div class="fill" :style="{ width: `${shieldFrac(side0) * 100}%` }" />
                            <span class="bar-label shield-label">{{ Math.round(side0.shield) }}</span>
                        </div>
                        <div class="bar hp-bar">
                            <div class="fill hp-fill" :style="{ width: `${hpFrac(side0) * 100}%` }" />
                        </div>
                    </div>
                </div>
                <div class="strip-wrap">
                    <div class="strip">
                        <div
                            v-for="(s, i) in slotsP1"
                            :key="`p1-${s.item_name}-${i}`"
                            class="slot-anchor"
                        >
                            <ItemTooltipAnchor
                                :item="catalog.byName.get(s.item_name)"
                                mode="deck"
                                :tier="s.tier"
                            >
                                <div
                                    class="dcard"
                                    :style="{
                                        width: `${dcardOuterWidthPx(catalog.byName.get(s.item_name)?.size ?? 1)}px`,
                                        flex: '0 0 auto',
                                        borderColor: tierBorderColor(s.tier),
                                    }"
                                >
                                    <div
                                        class="dcard-art"
                                        :style="itemArtAspectStyle(catalog.byName.get(s.item_name)?.size ?? 1)"
                                    >
                                        <img
                                            class="thumb"
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
                    </div>
                </div>
            </section>

            <div class="divider" />

            <section class="side-block p2">
                <header class="side-h">
                    <span>{{ p2DeckName ?? "玩家2" }}</span>
                    <span v-if="p2Outcome" class="outcome" :class="p2Outcome.cls">{{ p2Outcome.text }}</span>
                </header>
                <div class="strip-wrap">
                    <div class="strip">
                        <div
                            v-for="(s, i) in slotsP2"
                            :key="`p2-${s.item_name}-${i}`"
                            class="slot-anchor"
                        >
                            <ItemTooltipAnchor
                                :item="catalog.byName.get(s.item_name)"
                                mode="deck"
                                :tier="s.tier"
                            >
                                <div
                                    class="dcard"
                                    :style="{
                                        width: `${dcardOuterWidthPx(catalog.byName.get(s.item_name)?.size ?? 1)}px`,
                                        flex: '0 0 auto',
                                        borderColor: tierBorderColor(s.tier),
                                    }"
                                >
                                    <div
                                        class="dcard-art"
                                        :style="itemArtAspectStyle(catalog.byName.get(s.item_name)?.size ?? 1)"
                                    >
                                        <img
                                            class="thumb"
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
                    </div>
                </div>
                <div v-if="side1" class="hud hud-below">
                    <div class="nums">
                        <span class="hp-white">{{ Math.round(side1.hp) }}</span>
                        <span v-if="side1.burn > 0" class="burn">灼{{ Math.round(side1.burn) }}</span>
                        <span v-if="side1.poison > 0" class="poison">毒{{ Math.round(side1.poison) }}</span>
                        <span v-if="side1.regen > 0" class="regen">再{{ Math.round(side1.regen) }}</span>
                    </div>
                    <div class="bars">
                        <div v-if="side1.shield > 0" class="bar shield-bar">
                            <div class="fill" :style="{ width: `${shieldFrac(side1) * 100}%` }" />
                            <span class="bar-label shield-label">{{ Math.round(side1.shield) }}</span>
                        </div>
                        <div class="bar hp-bar">
                            <div class="fill hp-fill" :style="{ width: `${hpFrac(side1) * 100}%` }" />
                        </div>
                    </div>
                </div>
            </section>
        </template>
    </div>
</template>

<style scoped>
.battle {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    min-height: 0;
    flex: 1;
    overflow: auto;
}
.empty {
    color: #7a8494;
    padding: 1rem;
}
.battle-bar {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    flex-wrap: wrap;
}
.battle-btn {
    padding: 0.45rem 1rem;
    border-radius: 8px;
    border: 1px solid #4a7cbc;
    background: linear-gradient(180deg, #3d6fb8, #2a4a78);
    color: #fff;
    font-weight: 600;
    cursor: pointer;
}
.battle-btn:disabled {
    opacity: 0.45;
    cursor: not-allowed;
}
.hint {
    font-size: 0.85rem;
    color: #7a8494;
}
.err {
    color: #f88;
    margin: 0;
    font-size: 0.9rem;
}
.cli-meta {
    margin: 0;
    font-size: 0.72rem;
    line-height: 1.4;
    color: #8b95a5;
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: 0.35rem 0.6rem;
}
.cli-meta-label {
    color: #6d7685;
    flex: 0 0 auto;
}
.cli-meta-ver {
    font-size: 0.7rem;
    padding: 0.1rem 0.35rem;
    border-radius: 4px;
    background: #252a33;
    color: #a8e6a0;
    flex: 0 1 auto;
    max-width: 100%;
    word-break: break-all;
}
.cli-meta-path {
    flex: 1 1 100%;
    font-size: 0.68rem;
    color: #6a7382;
    word-break: break-all;
    opacity: 0.92;
}
.debug-loop {
    border: 1px solid #3a4555;
    border-radius: 8px;
    padding: 0.5rem 0.75rem;
    background: #1a1e26;
}
.debug-sum {
    cursor: pointer;
    color: #b8c0cc;
    font-size: 0.88rem;
    font-weight: 600;
}
.debug-hint {
    margin: 0.5rem 0 0.65rem;
    font-size: 0.78rem;
    line-height: 1.45;
    color: #8b95a5;
}
.debug-hint code {
    font-size: 0.76rem;
    padding: 0.05rem 0.25rem;
    border-radius: 3px;
    background: #252a33;
    color: #c5d0e0;
}
.debug-row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.45rem;
    margin-bottom: 0.35rem;
}
.seed-label {
    font-size: 0.82rem;
    color: #9aa3b2;
    font-variant-numeric: tabular-nums;
}
.btn-sm {
    padding: 0.28rem 0.55rem;
    border-radius: 6px;
    border: 1px solid #3d4450;
    background: #2a3038;
    color: #e8eaef;
    font-size: 0.78rem;
    cursor: pointer;
}
.btn-sm.primary {
    border-color: #4a7cbc;
    background: #2a4a78;
}
.btn-sm:disabled {
    opacity: 0.45;
    cursor: not-allowed;
}
.summary-pre {
    margin: 0.35rem 0 0;
    padding: 0.5rem 0.65rem;
    max-height: 280px;
    overflow: auto;
    font-size: 0.72rem;
    line-height: 1.35;
    white-space: pre-wrap;
    word-break: break-word;
    background: #14171c;
    border: 1px solid #2f3540;
    border-radius: 6px;
    color: #c8d0dc;
}
.player-shell {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    padding: 0.65rem 0.75rem;
    border-radius: 8px;
    border: 1px solid #3d4450;
    background: #1e2229;
}
.player-time {
    display: flex;
    align-items: baseline;
    gap: 0.25rem;
    font-variant-numeric: tabular-nums;
}
.time-val {
    color: #e8eaef;
    font-weight: 600;
    font-size: 1rem;
}
.time-sep {
    color: #5c6570;
}
.time-end {
    color: #9aa3b2;
    font-size: 0.95rem;
}
.time-unit {
    color: #7a8494;
    font-size: 0.8rem;
    margin-left: 0.15rem;
}
.player-controls {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.35rem;
    flex-wrap: wrap;
}
.icon-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 2.35rem;
    height: 2.35rem;
    padding: 0;
    border-radius: 8px;
    border: 1px solid #3d4450;
    background: #2a3038;
    color: #e8eaef;
    cursor: pointer;
    font-size: 1rem;
    line-height: 1;
}
.icon-btn:hover:not(:disabled) {
    background: #343b46;
    border-color: #5a6575;
}
.icon-btn:disabled {
    opacity: 0.4;
    cursor: not-allowed;
}
.icon-btn.primary {
    border-color: #4a7cbc;
    background: #2a4a78;
    min-width: 2.75rem;
}
.glyph {
    display: block;
    transform: translateY(-0.05em);
}
.scrub-wrap {
    padding: 0.15rem 0;
}
.scrub {
    width: 100%;
    height: 0.45rem;
    accent-color: #5a9fd4;
    cursor: pointer;
}
.scrub:disabled {
    opacity: 0.4;
    cursor: not-allowed;
}
.side-h {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 0.5rem;
    font-size: 0.85rem;
    color: #9aa3b2;
    margin-bottom: 0.35rem;
}
.outcome {
    display: inline-block;
    padding: 0.12rem 0.45rem;
    border-radius: 4px;
    font-size: 0.75rem;
    font-weight: 700;
    letter-spacing: 0.02em;
}
.tag-win {
    color: #b8f5a0;
    background: rgba(60, 120, 60, 0.35);
    border: 1px solid rgba(120, 200, 100, 0.45);
}
.tag-lose {
    color: #ffb8b0;
    background: rgba(140, 50, 50, 0.35);
    border: 1px solid rgba(220, 100, 90, 0.45);
}
.tag-draw {
    color: #e0d080;
    background: rgba(120, 110, 50, 0.35);
    border: 1px solid rgba(200, 180, 80, 0.45);
}
.side-block {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
}
.hud {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
}
.hud-below {
    margin-top: 0.35rem;
}
.nums {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: 0.5rem;
    font-size: 0.95rem;
    line-height: 1.2;
}
.hp-white {
    color: #ffffff;
    font-weight: 600;
}
.burn {
    color: #ff9f45;
}
.poison {
    color: #0ebe4f;
}
.regen {
    color: #8eea31;
}
.bars {
    display: flex;
    flex-direction: column;
    gap: 2px;
    max-width: 420px;
}
.bar {
    position: relative;
    height: 14px;
    border-radius: 4px;
    background: #2a2f38;
    overflow: hidden;
}
.shield-bar .fill {
    height: 100%;
    background: linear-gradient(90deg, #c9a820, #f4cf20);
    border-radius: 4px;
}
.hp-bar .hp-fill {
    height: 100%;
    background: linear-gradient(90deg, #8b2b2b, #f55a4a);
    border-radius: 4px;
}
.bar-label {
    position: absolute;
    left: 6px;
    top: 0;
    bottom: 0;
    display: flex;
    align-items: center;
    font-size: 0.7rem;
    pointer-events: none;
}
.shield-label {
    color: #1a1d24;
    font-weight: 600;
}
.divider {
    height: 1px;
    background: #3d4450;
    margin: 0.25rem 0;
}
.strip-wrap {
    border: 1px dashed #3d4450;
    border-radius: 8px;
    padding: 0.5rem;
    min-height: 100px;
    background: #22262e;
}
.strip {
    display: flex;
    flex-wrap: nowrap;
    align-items: flex-start;
    gap: 6px;
    overflow-x: auto;
}
.slot-anchor {
    display: flex;
    min-width: 0;
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
    cursor: default;
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
</style>
