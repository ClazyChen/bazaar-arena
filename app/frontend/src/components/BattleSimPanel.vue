<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from "vue";
import { fetchDeckSlots, postSaveCliRepro, postSimulate } from "@/api";
import {
    AMMO_KEYWORD_RGB,
    BURN_KEYWORD_RGB,
    CHARGE_KEYWORD_RGB,
    DAMAGE_KEYWORD_RGB,
    FREEZE_KEYWORD_RGB,
    SLOW_KEYWORD_RGB,
    HEAL_KEYWORD_RGB,
    POISON_KEYWORD_RGB,
    REGEN_KEYWORD_RGB,
    SHIELD_KEYWORD_RGB,
    dcardOuterWidthPx,
    freezeOverlayRgba,
    itemArtAspectStyle,
    tierBorderColor,
    unchargedOverlayRgba,
    webpUrl,
} from "@/lib/deckMath";
import {
    BATTLE_TICK_MS,
    buildPlaybackTimeline,
    unchargedOverlayFill,
    extractHudFloatEvents,
    formatTimeSec,
    hudFloatEventsForStep,
    type PlaybackStep,
} from "@/lib/battleSim";
import { useCatalogStore } from "@/stores/catalog";
import ItemTooltipAnchor from "@/components/ItemTooltipAnchor.vue";
import { itemCooldownMsForDeckTier } from "@/lib/itemTooltip";
import type {
    DeckSlotPayload,
    FrameEndItemSnapshot,
    FrameEndSideSnapshot,
    HudFloatEvent,
    SimulateCliEnvelope,
} from "@/types";

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

/** 最近一次成功 detailed 模拟的种子，用于 Summary 与保存 CLI 复现 JSON */
const lastUsedSeed = ref<number | null>(null);
const summaryLines = ref<string[]>([]);
const summaryErr = ref<string | null>(null);
const summaryLoading = ref(false);
const saveReproLoading = ref(false);
const saveReproErr = ref<string | null>(null);
const saveReproPath = ref<string | null>(null);

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

const hudFloatEventsAll = computed(() =>
    extractHudFloatEvents(simEnvelope.value?.result?.debug?.events),
);

/** 飘字独立队列：随步进追加，动画结束移除，避免播放时每帧被清空 */
interface HpFloatItem {
    id: number;
    ev: HudFloatEvent;
}

const hpFloatActiveP1 = ref<HpFloatItem[]>([]);
const hpFloatActiveP2 = ref<HpFloatItem[]>([]);
let nextHpFloatId = 0;

function clearHpFloats(): void {
    hpFloatActiveP1.value = [];
    hpFloatActiveP2.value = [];
}

function removeHpFloatP1(id: number): void {
    hpFloatActiveP1.value = hpFloatActiveP1.value.filter((x) => x.id !== id);
}

function removeHpFloatP2(id: number): void {
    hpFloatActiveP2.value = hpFloatActiveP2.value.filter((x) => x.id !== id);
}

function appendFloatsForStep(idx: number): void {
    const steps = timeline.value;
    if (steps.length === 0) return;
    const all = hudFloatEventsAll.value;
    const { side0, side1 } = hudFloatEventsForStep(steps, all, idx);
    const add0 = side0.map((ev) => ({ id: ++nextHpFloatId, ev }));
    const add1 = side1.map((ev) => ({ id: ++nextHpFloatId, ev }));
    if (add0.length > 0) {
        hpFloatActiveP1.value = [...hpFloatActiveP1.value, ...add0];
    }
    if (add1.length > 0) {
        hpFloatActiveP2.value = [...hpFloatActiveP2.value, ...add1];
    }
}

watch(playheadIndex, (idx, prevIdx) => {
    const steps = timeline.value;
    if (steps.length === 0) {
        clearHpFloats();
        return;
    }
    if (prevIdx !== undefined && idx - prevIdx !== 1) {
        clearHpFloats();
    }
    appendFloatsForStep(idx);
});

watch(timeline, (steps) => {
    playing.value = false;
    clearHpFloats();
    const prevPh = playheadIndex.value;
    playheadIndex.value = 0;
    if (steps.length === 0) return;
    if (prevPh === playheadIndex.value) {
        appendFloatsForStep(0);
    }
});

watch(maxPlayhead, (m) => {
    if (playheadIndex.value > m) playheadIndex.value = m;
});

function hudFloatLabel(ev: HudFloatEvent): string {
    const n = Math.round(ev.amount);
    if (ev.kind === "heal" || ev.kind === "regen") {
        return ev.isCrit ? `+${n}!` : `+${n}`;
    }
    if (ev.kind === "burn") {
        return ev.isCrit ? `火${n}!` : `火${n}`;
    }
    if (ev.kind === "poison") {
        return ev.isCrit ? `毒${n}!` : `毒${n}`;
    }
    return ev.isCrit ? `−${n}!` : `−${n}`;
}

function hudFloatColor(ev: HudFloatEvent): string {
    switch (ev.kind) {
        case "damage":
            return DAMAGE_KEYWORD_RGB;
        case "burn":
            return BURN_KEYWORD_RGB;
        case "poison":
            return POISON_KEYWORD_RGB;
        case "heal":
            return HEAL_KEYWORD_RGB;
        case "regen":
            return REGEN_KEYWORD_RGB;
        default:
            return DAMAGE_KEYWORD_RGB;
    }
}

/** 飘字：10 及以下为标准字号；大于 10 时按 ⌈log₂(n/10)⌉ 每档 +2px，累计上限 +30 */
function hudFloatExtraFontPx(amount: number): number {
    const n = Math.round(Math.max(0, amount));
    if (n <= 10) return 0;
    const steps = Math.ceil(Math.log2(n / 10));
    return Math.min(30, steps * 2);
}

function hudFloatStyle(ev: HudFloatEvent): Record<string, string> {
    const extra = hudFloatExtraFontPx(ev.amount);
    const style: Record<string, string> = { color: hudFloatColor(ev) };
    if (extra > 0) {
        const base = ev.isCrit ? "1.58rem" : "1.25rem";
        style.fontSize = `calc(${base} + ${extra}px)`;
    }
    return style;
}

function outcomeForSide(sideIndex: 0 | 1): { text: string; cls: string } | null {
    const r = simEnvelope.value?.result;
    if (!r || !simEnvelope.value?.ok) return null;
    if (r.isDraw) return { text: "平局", cls: "tag-draw" };
    if (r.winner === sideIndex) return { text: "胜利", cls: "tag-win" };
    return { text: "失败", cls: "tag-lose" };
}

const p1Outcome = computed(() => outcomeForSide(0));
const p2Outcome = computed(() => outcomeForSide(1));

function itemSnapshotForSlot(
    side: FrameEndSideSnapshot | null,
    slotIndex: number,
): FrameEndItemSnapshot | null {
    if (!side?.items?.length) return null;
    const by = side.items.find((x) => x.itemIndex === slotIndex);
    if (by) return by;
    return side.items[slotIndex] ?? null;
}

function itemChargeOverlayStyle(
    side: FrameEndSideSnapshot | null,
    slotIndex: number,
    slot: DeckSlotPayload | undefined,
): Record<string, string> | undefined {
    const row = slot ? catalog.byName.get(slot.item_name) : undefined;
    if (itemCooldownMsForDeckTier(row, slot?.tier ?? 0) === null) return undefined;
    const it = itemSnapshotForSlot(side, slotIndex);
    if (!it) return undefined;
    const cd = it.Cooldown;
    if (!Number.isFinite(cd) || cd <= 0) return undefined;
    const fill = unchargedOverlayFill(it.ChargedTime, cd);
    if (fill <= 0) return undefined;
    return {
        height: `${fill * 100}%`,
        background: unchargedOverlayRgba(0.38),
    };
}

/** 充能推进位置：黑色未充能遮罩下缘的横向线（与 tooltip「充能」同色） */
function itemChargeFrontierLineStyle(
    side: FrameEndSideSnapshot | null,
    slotIndex: number,
    slot: DeckSlotPayload | undefined,
): Record<string, string> | undefined {
    const row = slot ? catalog.byName.get(slot.item_name) : undefined;
    if (itemCooldownMsForDeckTier(row, slot?.tier ?? 0) === null) return undefined;
    const it = itemSnapshotForSlot(side, slotIndex);
    if (!it) return undefined;
    const cd = it.Cooldown;
    if (!Number.isFinite(cd) || cd <= 0) return undefined;
    const fill = unchargedOverlayFill(it.ChargedTime, cd);
    if (fill <= 0) return undefined;
    return {
        top: `${fill * 100}%`,
        background: CHARGE_KEYWORD_RGB,
    };
}

const p1ChargeOverlayStyles = computed((): (Record<string, string> | undefined)[] => {
    const side = side0.value;
    const n = slotsP1.value.length;
    const out: (Record<string, string> | undefined)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemChargeOverlayStyle(side, i, slotsP1.value[i]));
    }
    return out;
});

const p2ChargeOverlayStyles = computed((): (Record<string, string> | undefined)[] => {
    const side = side1.value;
    const n = slotsP2.value.length;
    const out: (Record<string, string> | undefined)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemChargeOverlayStyle(side, i, slotsP2.value[i]));
    }
    return out;
});

const p1ChargeFrontierLineStyles = computed((): (Record<string, string> | undefined)[] => {
    const side = side0.value;
    const n = slotsP1.value.length;
    const out: (Record<string, string> | undefined)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemChargeFrontierLineStyle(side, i, slotsP1.value[i]));
    }
    return out;
});

const p2ChargeFrontierLineStyles = computed((): (Record<string, string> | undefined)[] => {
    const side = side1.value;
    const n = slotsP2.value.length;
    const out: (Record<string, string> | undefined)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemChargeFrontierLineStyle(side, i, slotsP2.value[i]));
    }
    return out;
});

/** 仅单独加速 / 单独减速时在充能边界线旁显示箭头（与冻结或另一状态并存时不显示） */
type ChargeArrowDecor = { variant: "haste" | "slow"; style: Record<string, string> };

function itemChargeArrowDecor(
    side: FrameEndSideSnapshot | null,
    slotIndex: number,
    slot: DeckSlotPayload | undefined,
): ChargeArrowDecor | null {
    const row = slot ? catalog.byName.get(slot.item_name) : undefined;
    if (itemCooldownMsForDeckTier(row, slot?.tier ?? 0) === null) return null;
    const it = itemSnapshotForSlot(side, slotIndex);
    if (!it) return null;
    const cd = it.Cooldown;
    if (!Number.isFinite(cd) || cd <= 0) return null;
    const fill = unchargedOverlayFill(it.ChargedTime, cd);
    if (fill <= 0) return null;

    const fr = it.FreezeRemaining ?? 0;
    const haste = it.HasteRemaining ?? 0;
    const slow = it.SlowRemaining ?? 0;
    const frozen = Number.isFinite(fr) && fr > 0;
    const hasHaste = Number.isFinite(haste) && haste > 0;
    const hasSlow = Number.isFinite(slow) && slow > 0;
    if (frozen) return null;
    if (hasHaste && !hasSlow) {
        return {
            variant: "haste",
            style: {
                top: `calc(${fill * 100}% - 1px)`,
                transform: "translateY(-100%)",
                "--charge-arrow-color": CHARGE_KEYWORD_RGB,
            },
        };
    }
    if (hasSlow && !hasHaste) {
        return {
            variant: "slow",
            style: {
                top: `calc(${fill * 100}% + 2px)`,
                "--charge-arrow-color": CHARGE_KEYWORD_RGB,
            },
        };
    }
    return null;
}

const p1ChargeArrowDecors = computed((): (ChargeArrowDecor | null)[] => {
    const side = side0.value;
    const n = slotsP1.value.length;
    const out: (ChargeArrowDecor | null)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemChargeArrowDecor(side, i, slotsP1.value[i]));
    }
    return out;
});

const p2ChargeArrowDecors = computed((): (ChargeArrowDecor | null)[] => {
    const side = side1.value;
    const n = slotsP2.value.length;
    const out: (ChargeArrowDecor | null)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemChargeArrowDecor(side, i, slotsP2.value[i]));
    }
    return out;
});

/** 单枚徽章最大宽度 = 小物品（size=1）外宽的 40%；高为宽的一半（2:1） */
const DAMAGE_BADGE_WIDTH_PX = dcardOuterWidthPx(1) * 0.4;
const STAT_BADGE_GAP_PX = 2;
const STAT_BADGE_ROW_PAD_PX = 4;

interface ItemStatBadge {
    text: string;
    background: string;
    widthPx: number;
    heightPx: number;
    fontPx: number;
}

const STAT_BADGE_DEFS: { key: keyof Pick<FrameEndItemSnapshot, "Damage" | "Shield" | "Heal" | "Burn" | "Poison" | "Regen">; bg: string }[] = [
    { key: "Damage", bg: DAMAGE_KEYWORD_RGB },
    { key: "Shield", bg: SHIELD_KEYWORD_RGB },
    { key: "Heal", bg: HEAL_KEYWORD_RGB },
    { key: "Burn", bg: BURN_KEYWORD_RGB },
    { key: "Poison", bg: POISON_KEYWORD_RGB },
    { key: "Regen", bg: REGEN_KEYWORD_RGB },
];

function itemStatBadgeRow(
    side: FrameEndSideSnapshot | null,
    slotIndex: number,
    dcardOuterPx: number,
): ItemStatBadge[] | null {
    const it = itemSnapshotForSlot(side, slotIndex);
    if (!it) return null;
    const slices: { v: number; bg: string }[] = [];
    for (const { key, bg } of STAT_BADGE_DEFS) {
        const raw = it[key];
        const v = typeof raw === "number" ? raw : Number(raw ?? 0);
        if (!Number.isFinite(v) || v <= 0) continue;
        slices.push({ v, bg });
    }
    if (slices.length === 0) return null;
    const n = slices.length;
    const available = Math.max(
        0,
        dcardOuterPx - STAT_BADGE_ROW_PAD_PX - STAT_BADGE_GAP_PX * (n - 1),
    );
    const eachW = Math.max(12, Math.min(DAMAGE_BADGE_WIDTH_PX, available / n));
    const heightPx = eachW / 2;
    const fontPx = Math.max(6, Math.min(11, eachW * 0.36));
    return slices.map(({ v, bg }) => ({
        text: String(Math.round(v)),
        background: bg,
        widthPx: eachW,
        heightPx,
        fontPx,
    }));
}

const p1StatBadgeRows = computed((): (ItemStatBadge[] | null)[] => {
    const side = side0.value;
    const n = slotsP1.value.length;
    const out: (ItemStatBadge[] | null)[] = [];
    for (let i = 0; i < n; i++) {
        const sz = catalog.byName.get(slotsP1.value[i].item_name)?.size ?? 1;
        out.push(itemStatBadgeRow(side, i, dcardOuterWidthPx(sz)));
    }
    return out;
});

const p2StatBadgeRows = computed((): (ItemStatBadge[] | null)[] => {
    const side = side1.value;
    const n = slotsP2.value.length;
    const out: (ItemStatBadge[] | null)[] = [];
    for (let i = 0; i < n; i++) {
        const sz = catalog.byName.get(slotsP2.value[i].item_name)?.size ?? 1;
        out.push(itemStatBadgeRow(side, i, dcardOuterWidthPx(sz)));
    }
    return out;
});

function itemFreezeBadge(
    side: FrameEndSideSnapshot | null,
    slotIndex: number,
): { widthPx: number; heightPx: number; fontPx: number; text: string } | null {
    const it = itemSnapshotForSlot(side, slotIndex);
    const fr = it?.FreezeRemaining ?? 0;
    if (!Number.isFinite(fr) || fr <= 0) return null;
    const widthPx = DAMAGE_BADGE_WIDTH_PX;
    const heightPx = widthPx / 2;
    return {
        widthPx,
        heightPx,
        fontPx: Math.max(8, Math.min(11, widthPx * 0.36)),
        text: (fr / 1000).toFixed(1),
    };
}

const p1FreezeBadges = computed(() => {
    const side = side0.value;
    const n = slotsP1.value.length;
    const out: ({ widthPx: number; heightPx: number; fontPx: number; text: string } | null)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemFreezeBadge(side, i));
    }
    return out;
});

const p2FreezeBadges = computed(() => {
    const side = side1.value;
    const n = slotsP2.value.length;
    const out: ({ widthPx: number; heightPx: number; fontPx: number; text: string } | null)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemFreezeBadge(side, i));
    }
    return out;
});

type ItemTimeBadge = { widthPx: number; heightPx: number; fontPx: number; text: string };

function itemHasteBadge(side: FrameEndSideSnapshot | null, slotIndex: number): ItemTimeBadge | null {
    const it = itemSnapshotForSlot(side, slotIndex);
    const ms = it?.HasteRemaining ?? 0;
    if (!Number.isFinite(ms) || ms <= 0) return null;
    const widthPx = DAMAGE_BADGE_WIDTH_PX;
    const heightPx = widthPx / 2;
    return {
        widthPx,
        heightPx,
        fontPx: Math.max(8, Math.min(11, widthPx * 0.36)),
        text: (ms / 1000).toFixed(1),
    };
}

function itemSlowBadge(side: FrameEndSideSnapshot | null, slotIndex: number): ItemTimeBadge | null {
    const it = itemSnapshotForSlot(side, slotIndex);
    const ms = it?.SlowRemaining ?? 0;
    if (!Number.isFinite(ms) || ms <= 0) return null;
    const widthPx = DAMAGE_BADGE_WIDTH_PX;
    const heightPx = widthPx / 2;
    return {
        widthPx,
        heightPx,
        fontPx: Math.max(8, Math.min(11, widthPx * 0.36)),
        text: (ms / 1000).toFixed(1),
    };
}

const p1HasteBadges = computed((): (ItemTimeBadge | null)[] => {
    const side = side0.value;
    const n = slotsP1.value.length;
    const out: (ItemTimeBadge | null)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemHasteBadge(side, i));
    }
    return out;
});

const p2HasteBadges = computed((): (ItemTimeBadge | null)[] => {
    const side = side1.value;
    const n = slotsP2.value.length;
    const out: (ItemTimeBadge | null)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemHasteBadge(side, i));
    }
    return out;
});

const p1SlowBadges = computed((): (ItemTimeBadge | null)[] => {
    const side = side0.value;
    const n = slotsP1.value.length;
    const out: (ItemTimeBadge | null)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemSlowBadge(side, i));
    }
    return out;
});

const p2SlowBadges = computed((): (ItemTimeBadge | null)[] => {
    const side = side1.value;
    const n = slotsP2.value.length;
    const out: (ItemTimeBadge | null)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemSlowBadge(side, i));
    }
    return out;
});

/** 弹药圆点：从左向右；消耗从右侧扣减（左侧为仍装填的弹药） */
function itemAmmoPips(
    side: FrameEndSideSnapshot | null,
    slotIndex: number,
): { cap: number; remaining: number } | null {
    const it = itemSnapshotForSlot(side, slotIndex);
    if (!it) return null;
    const cap = it.AmmoCap ?? 0;
    if (!Number.isFinite(cap) || cap <= 0) return null;
    let rem = it.AmmoRemaining ?? 0;
    if (!Number.isFinite(rem)) rem = 0;
    const c = Math.round(cap);
    rem = Math.max(0, Math.min(c, Math.round(rem)));
    return { cap: c, remaining: rem };
}

const p1AmmoPips = computed((): ({ cap: number; remaining: number } | null)[] => {
    const side = side0.value;
    const n = slotsP1.value.length;
    const out: ({ cap: number; remaining: number } | null)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemAmmoPips(side, i));
    }
    return out;
});

const p2AmmoPips = computed((): ({ cap: number; remaining: number } | null)[] => {
    const side = side1.value;
    const n = slotsP2.value.length;
    const out: ({ cap: number; remaining: number } | null)[] = [];
    for (let i = 0; i < n; i++) {
        out.push(itemAmmoPips(side, i));
    }
    return out;
});

/** 模拟器版本/路径与调试区默认折叠，由「调试信息」按钮展开 */
const showSimDebugPanel = ref(false);

const canShowSimDebug = computed(() => {
    const env = simEnvelope.value;
    if (!env?.ok) return false;
    return (
        lastUsedSeed.value !== null ||
        Boolean(env.bazaararenaCliVersion || env.bazaararenaCli)
    );
});

watch(
    () => [props.deckIdP1, props.deckIdP2] as const,
    async ([id1, id2]) => {
        slotsErr.value = null;
        simErr.value = null;
        simEnvelope.value = null;
        lastUsedSeed.value = null;
        summaryLines.value = [];
        summaryErr.value = null;
        saveReproPath.value = null;
        saveReproErr.value = null;
        slotsP1.value = [];
        slotsP2.value = [];
        playheadIndex.value = 0;
        playing.value = false;
        showSimDebugPanel.value = false;
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
    saveReproPath.value = null;
    saveReproErr.value = null;
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

/** 将复现数据写入仓库 samples/cli/（需本地后端可写盘） */
async function saveCliReproToSamples(): Promise<void> {
    const id1 = props.deckIdP1;
    const id2 = props.deckIdP2;
    const seed = lastUsedSeed.value;
    if (id1 === null || id2 === null || seed === null) return;
    saveReproLoading.value = true;
    saveReproErr.value = null;
    saveReproPath.value = null;
    try {
        const res = await postSaveCliRepro({
            deck_id_0: id1,
            deck_id_1: id2,
            seed,
            debug_level: "detailed",
        });
        if (!res.ok) {
            saveReproErr.value = res.error || "保存失败";
            return;
        }
        if (res.relativePath) {
            saveReproPath.value = res.relativePath;
        }
    } catch (e) {
        saveReproErr.value = e instanceof Error ? e.message : String(e);
    } finally {
        saveReproLoading.value = false;
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
                <button
                    v-if="canShowSimDebug"
                    type="button"
                    class="btn-sm debug-toggle"
                    :aria-expanded="showSimDebugPanel"
                    @click="showSimDebugPanel = !showSimDebugPanel"
                >
                    {{ showSimDebugPanel ? "收起调试" : "调试信息" }}
                </button>
                <span v-if="slotsLoading" class="hint">加载卡组…</span>
                <span v-else-if="battleLoading" class="hint">模拟中…</span>
            </div>
            <p v-if="slotsErr" class="err">{{ slotsErr }}</p>
            <p v-else-if="simErr" class="err">{{ simErr }}</p>

            <div v-show="showSimDebugPanel && canShowSimDebug" class="sim-debug-wrap">
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

                <div v-if="lastUsedSeed !== null" class="debug-loop">
                    <h3 class="debug-sum">调试：Summary 与 CLI 复现（同种子）</h3>
                    <p class="debug-hint">
                        闭环：点「拉取 Summary 日志」与引擎
                        <code>debug.level=summary</code>
                        文本对照；点「保存复现 JSON 到 samples/cli」由后端写入仓库
                        <code>samples/cli/</code>
                        ，供本地
                        <code>bazaararena_cli --input … --output out.json</code>
                        。顶层结构与对战模拟请求一致，并含
                        <code>cliReproMeta</code>
                        卡组快照。修改引擎后重编 CLI 再「再次对战」验证。
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
                        <button
                            type="button"
                            class="btn-sm"
                            :disabled="saveReproLoading"
                            @click="saveCliReproToSamples"
                        >
                            {{ saveReproLoading ? "写入中…" : "保存复现 JSON 到 samples/cli" }}
                        </button>
                    </div>
                    <p v-if="saveReproPath" class="save-repro-ok">
                        已写入：<code>{{ saveReproPath }}</code>
                    </p>
                    <p v-if="saveReproErr" class="err">{{ saveReproErr }}</p>
                    <p v-if="summaryErr" class="err">{{ summaryErr }}</p>
                    <pre v-if="summaryLines.length" class="summary-pre">{{ summaryLines.join("\n") }}</pre>
                </div>
            </div>

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
                    <div class="hud-bars-stack">
                        <div v-if="hpFloatActiveP1.length" class="hp-float-layer">
                            <span
                                v-for="item in hpFloatActiveP1"
                                :key="item.id"
                                class="hp-float"
                                :class="{ 'hp-float--crit': item.ev.isCrit }"
                                :style="hudFloatStyle(item.ev)"
                                @animationend="removeHpFloatP1(item.id)"
                            >{{ hudFloatLabel(item.ev) }}</span>
                        </div>
                        <div class="bars">
                            <div v-if="side0.shield > 0" class="bar shield-bar">
                                <div class="fill" :style="{ width: `${shieldFrac(side0) * 100}%` }" />
                                <span class="bar-label shield-label">{{ Math.round(side0.shield) }}</span>
                            </div>
                            <div class="bar hp-bar">
                                <div class="fill hp-fill" :style="{ width: `${hpFrac(side0) * 100}%` }" />
                                <div class="hp-on-bar hp-bar-stats">
                                    <span class="hp-bar-num hp-bar-num--hp">{{ Math.round(side0.hp) }}</span>
                                    <span v-if="side0.burn > 0" class="hp-bar-num hp-bar-num--burn"
                                        ><span class="hp-debuff-glyph" aria-hidden="true">火</span
                                        >{{ Math.round(side0.burn) }}</span
                                    >
                                    <span v-if="side0.poison > 0" class="hp-bar-num hp-bar-num--poison"
                                        ><span class="hp-debuff-glyph" aria-hidden="true">毒</span
                                        >{{ Math.round(side0.poison) }}</span
                                    >
                                    <span v-if="side0.regen > 0" class="hp-bar-num hp-bar-num--regen">{{
                                        Math.round(side0.regen)
                                    }}</span>
                                </div>
                            </div>
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
                                :attrs-override="s.attrs_override"
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
                                        <div
                                            v-if="p1ChargeOverlayStyles[i]"
                                            class="charge-overlay"
                                            :style="p1ChargeOverlayStyles[i]"
                                        />
                                        <div
                                            v-if="p1ChargeFrontierLineStyles[i]"
                                            class="charge-frontier-line"
                                            :style="p1ChargeFrontierLineStyles[i]"
                                        />
                                        <div
                                            v-if="p1ChargeArrowDecors[i]"
                                            class="charge-arrow-row"
                                            :style="p1ChargeArrowDecors[i]!.style"
                                        >
                                            <span
                                                v-for="j in 3"
                                                :key="j"
                                                class="charge-arrow"
                                                :class="
                                                    p1ChargeArrowDecors[i]!.variant === 'haste'
                                                        ? 'charge-arrow--up'
                                                        : 'charge-arrow--down'
                                                "
                                            />
                                        </div>
                                        <div
                                            v-if="p1FreezeBadges[i]"
                                            class="freeze-full-overlay"
                                            :style="{ background: freezeOverlayRgba(0.38) }"
                                        />
                                        <div v-if="p1StatBadgeRows[i]?.length" class="stat-badges-row">
                                            <div
                                                v-for="(bd, bi) in p1StatBadgeRows[i]!"
                                                :key="bi"
                                                class="stat-badge"
                                                :style="{
                                                    width: `${bd.widthPx}px`,
                                                    height: `${bd.heightPx}px`,
                                                    fontSize: `${bd.fontPx}px`,
                                                    background: bd.background,
                                                }"
                                            >
                                                {{ bd.text }}
                                            </div>
                                        </div>
                                        <div
                                            v-if="p1FreezeBadges[i]"
                                            class="freeze-time-badge"
                                            :style="{
                                                width: `${p1FreezeBadges[i]!.widthPx}px`,
                                                height: `${p1FreezeBadges[i]!.heightPx}px`,
                                                fontSize: `${p1FreezeBadges[i]!.fontPx}px`,
                                                background: FREEZE_KEYWORD_RGB,
                                            }"
                                        >
                                            {{ p1FreezeBadges[i]!.text }}
                                        </div>
                                        <div
                                            v-if="p1HasteBadges[i]"
                                            class="haste-time-badge"
                                            :style="{
                                                width: `${p1HasteBadges[i]!.widthPx}px`,
                                                height: `${p1HasteBadges[i]!.heightPx}px`,
                                                fontSize: `${p1HasteBadges[i]!.fontPx}px`,
                                                background: CHARGE_KEYWORD_RGB,
                                            }"
                                        >
                                            {{ p1HasteBadges[i]!.text }}
                                        </div>
                                        <div
                                            v-if="p1SlowBadges[i]"
                                            class="slow-time-badge"
                                            :style="{
                                                width: `${p1SlowBadges[i]!.widthPx}px`,
                                                height: `${p1SlowBadges[i]!.heightPx}px`,
                                                fontSize: `${p1SlowBadges[i]!.fontPx}px`,
                                                background: SLOW_KEYWORD_RGB,
                                            }"
                                        >
                                            {{ p1SlowBadges[i]!.text }}
                                        </div>
                                        <div
                                            v-if="p1AmmoPips[i]"
                                            class="ammo-pips"
                                            :style="{ '--ammo-dot': AMMO_KEYWORD_RGB }"
                                        >
                                            <span
                                                v-for="pipIdx in p1AmmoPips[i]!.cap"
                                                :key="pipIdx"
                                                class="ammo-pip"
                                                :class="{ 'ammo-pip--empty': pipIdx > p1AmmoPips[i]!.remaining }"
                                            >
                                                <span
                                                    v-if="pipIdx <= p1AmmoPips[i]!.remaining"
                                                    class="ammo-pip-dot"
                                                />
                                            </span>
                                        </div>
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
                                :attrs-override="s.attrs_override"
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
                                        <div
                                            v-if="p2ChargeOverlayStyles[i]"
                                            class="charge-overlay"
                                            :style="p2ChargeOverlayStyles[i]"
                                        />
                                        <div
                                            v-if="p2ChargeFrontierLineStyles[i]"
                                            class="charge-frontier-line"
                                            :style="p2ChargeFrontierLineStyles[i]"
                                        />
                                        <div
                                            v-if="p2ChargeArrowDecors[i]"
                                            class="charge-arrow-row"
                                            :style="p2ChargeArrowDecors[i]!.style"
                                        >
                                            <span
                                                v-for="j in 3"
                                                :key="j"
                                                class="charge-arrow"
                                                :class="
                                                    p2ChargeArrowDecors[i]!.variant === 'haste'
                                                        ? 'charge-arrow--up'
                                                        : 'charge-arrow--down'
                                                "
                                            />
                                        </div>
                                        <div
                                            v-if="p2FreezeBadges[i]"
                                            class="freeze-full-overlay"
                                            :style="{ background: freezeOverlayRgba(0.38) }"
                                        />
                                        <div v-if="p2StatBadgeRows[i]?.length" class="stat-badges-row">
                                            <div
                                                v-for="(bd, bi) in p2StatBadgeRows[i]!"
                                                :key="bi"
                                                class="stat-badge"
                                                :style="{
                                                    width: `${bd.widthPx}px`,
                                                    height: `${bd.heightPx}px`,
                                                    fontSize: `${bd.fontPx}px`,
                                                    background: bd.background,
                                                }"
                                            >
                                                {{ bd.text }}
                                            </div>
                                        </div>
                                        <div
                                            v-if="p2FreezeBadges[i]"
                                            class="freeze-time-badge"
                                            :style="{
                                                width: `${p2FreezeBadges[i]!.widthPx}px`,
                                                height: `${p2FreezeBadges[i]!.heightPx}px`,
                                                fontSize: `${p2FreezeBadges[i]!.fontPx}px`,
                                                background: FREEZE_KEYWORD_RGB,
                                            }"
                                        >
                                            {{ p2FreezeBadges[i]!.text }}
                                        </div>
                                        <div
                                            v-if="p2HasteBadges[i]"
                                            class="haste-time-badge"
                                            :style="{
                                                width: `${p2HasteBadges[i]!.widthPx}px`,
                                                height: `${p2HasteBadges[i]!.heightPx}px`,
                                                fontSize: `${p2HasteBadges[i]!.fontPx}px`,
                                                background: CHARGE_KEYWORD_RGB,
                                            }"
                                        >
                                            {{ p2HasteBadges[i]!.text }}
                                        </div>
                                        <div
                                            v-if="p2SlowBadges[i]"
                                            class="slow-time-badge"
                                            :style="{
                                                width: `${p2SlowBadges[i]!.widthPx}px`,
                                                height: `${p2SlowBadges[i]!.heightPx}px`,
                                                fontSize: `${p2SlowBadges[i]!.fontPx}px`,
                                                background: SLOW_KEYWORD_RGB,
                                            }"
                                        >
                                            {{ p2SlowBadges[i]!.text }}
                                        </div>
                                        <div
                                            v-if="p2AmmoPips[i]"
                                            class="ammo-pips"
                                            :style="{ '--ammo-dot': AMMO_KEYWORD_RGB }"
                                        >
                                            <span
                                                v-for="pipIdx in p2AmmoPips[i]!.cap"
                                                :key="pipIdx"
                                                class="ammo-pip"
                                                :class="{ 'ammo-pip--empty': pipIdx > p2AmmoPips[i]!.remaining }"
                                            >
                                                <span
                                                    v-if="pipIdx <= p2AmmoPips[i]!.remaining"
                                                    class="ammo-pip-dot"
                                                />
                                            </span>
                                        </div>
                                    </div>
                                    <span class="cap">{{ s.item_name }}</span>
                                </div>
                            </ItemTooltipAnchor>
                        </div>
                    </div>
                </div>
                <div v-if="side1" class="hud hud-below">
                    <div class="hud-bars-stack">
                        <div v-if="hpFloatActiveP2.length" class="hp-float-layer">
                            <span
                                v-for="item in hpFloatActiveP2"
                                :key="item.id"
                                class="hp-float"
                                :class="{ 'hp-float--crit': item.ev.isCrit }"
                                :style="hudFloatStyle(item.ev)"
                                @animationend="removeHpFloatP2(item.id)"
                            >{{ hudFloatLabel(item.ev) }}</span>
                        </div>
                        <div class="bars">
                            <div v-if="side1.shield > 0" class="bar shield-bar">
                                <div class="fill" :style="{ width: `${shieldFrac(side1) * 100}%` }" />
                                <span class="bar-label shield-label">{{ Math.round(side1.shield) }}</span>
                            </div>
                            <div class="bar hp-bar">
                                <div class="fill hp-fill" :style="{ width: `${hpFrac(side1) * 100}%` }" />
                                <div class="hp-on-bar hp-bar-stats">
                                    <span class="hp-bar-num hp-bar-num--hp">{{ Math.round(side1.hp) }}</span>
                                    <span v-if="side1.burn > 0" class="hp-bar-num hp-bar-num--burn"
                                        ><span class="hp-debuff-glyph" aria-hidden="true">火</span
                                        >{{ Math.round(side1.burn) }}</span
                                    >
                                    <span v-if="side1.poison > 0" class="hp-bar-num hp-bar-num--poison"
                                        ><span class="hp-debuff-glyph" aria-hidden="true">毒</span
                                        >{{ Math.round(side1.poison) }}</span
                                    >
                                    <span v-if="side1.regen > 0" class="hp-bar-num hp-bar-num--regen">{{
                                        Math.round(side1.regen)
                                    }}</span>
                                </div>
                            </div>
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
.sim-debug-wrap {
    display: flex;
    flex-direction: column;
    gap: 0.65rem;
}
.debug-toggle {
    flex: 0 0 auto;
}
.debug-loop {
    border: 1px solid #3a4555;
    border-radius: 8px;
    padding: 0.5rem 0.75rem;
    background: #1a1e26;
}
.debug-sum {
    margin: 0;
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
    overflow: visible;
}
.hud-below {
    margin-top: 0.35rem;
}
.hud-bars-stack {
    position: relative;
    display: flex;
    flex-direction: column;
    align-items: stretch;
    width: 100%;
    max-width: min(100%, 640px);
}
.hp-float-layer {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 100%;
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    align-items: flex-end;
    gap: 0.35rem 0.55rem;
    padding-bottom: 6px;
    pointer-events: none;
    z-index: 3;
}
.hp-float {
    display: inline-block;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
    font-size: 1.25rem;
    line-height: 1.15;
    pointer-events: none;
    text-shadow:
        0 0 3px #1a1d24,
        0 1px 2px rgba(0, 0, 0, 0.75);
    animation: hpFloatUp 0.88s ease-out forwards;
}
.hp-float--crit {
    font-size: 1.58rem;
}
@keyframes hpFloatUp {
    from {
        transform: translateY(4px);
        opacity: 0.98;
    }
    to {
        transform: translateY(-28px);
        opacity: 0;
    }
}
.hp-on-bar {
    position: absolute;
    inset: 0;
    z-index: 2;
    display: flex;
    align-items: center;
    justify-content: center;
    pointer-events: none;
}
.hp-bar-stats {
    flex-wrap: wrap;
    gap: 0 0.45rem;
    row-gap: 0;
    max-width: 100%;
    padding: 0 4px;
    box-sizing: border-box;
}
.hp-bar-num {
    font-weight: 600;
    font-size: 0.88rem;
    line-height: 1;
    font-variant-numeric: tabular-nums;
    text-shadow:
        0 0 3px #1a1d24,
        0 1px 2px rgba(0, 0, 0, 0.85);
}
.hp-bar-num--hp {
    color: #ffffff;
}
.hp-debuff-glyph {
    display: inline-block;
    margin-right: 1px;
    font-weight: 800;
    font-size: 0.82em;
    line-height: 1;
    vertical-align: 0.05em;
}
.hp-bar-num--burn {
    color: rgb(255, 159, 69);
}
.hp-bar-num--poison {
    color: rgb(14, 190, 79);
}
.hp-bar-num--regen {
    color: rgb(142, 234, 49);
}
.bars {
    display: flex;
    flex-direction: column;
    gap: 2px;
    width: 100%;
}
.bar {
    position: relative;
    height: 18px;
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
.stat-badges-row {
    position: absolute;
    left: 50%;
    top: 3px;
    transform: translateX(-50%);
    z-index: 4;
    display: flex;
    flex-direction: row;
    flex-wrap: nowrap;
    align-items: flex-start;
    justify-content: center;
    gap: 2px;
    max-width: calc(100% - 4px);
    pointer-events: none;
    padding: 0 2px;
    box-sizing: border-box;
}
.stat-badge {
    box-sizing: border-box;
    flex: 0 1 auto;
    min-width: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #ffffff;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
    line-height: 1;
    border-radius: 2px;
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
    z-index: 0;
}
.charge-overlay {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    z-index: 2;
    pointer-events: none;
}
.charge-frontier-line {
    position: absolute;
    left: 0;
    right: 0;
    height: 2px;
    z-index: 2;
    pointer-events: none;
    box-sizing: border-box;
}
.charge-arrow-row {
    position: absolute;
    left: 0;
    right: 0;
    display: flex;
    justify-content: center;
    align-items: center;
    gap: 4px;
    z-index: 2;
    pointer-events: none;
    line-height: 0;
}
.charge-arrow {
    flex-shrink: 0;
}
.charge-arrow--up {
    width: 0;
    height: 0;
    border-left: 5px solid transparent;
    border-right: 5px solid transparent;
    border-bottom: 6px solid var(--charge-arrow-color, rgb(0, 236, 195));
}
.charge-arrow--down {
    width: 0;
    height: 0;
    border-left: 5px solid transparent;
    border-right: 5px solid transparent;
    border-top: 6px solid var(--charge-arrow-color, rgb(0, 236, 195));
}
.freeze-full-overlay {
    position: absolute;
    inset: 0;
    z-index: 3;
    pointer-events: none;
}
.ammo-pips {
    position: absolute;
    left: 50%;
    bottom: 4px;
    transform: translateX(-50%);
    z-index: 6;
    display: flex;
    flex-direction: row;
    align-items: center;
    gap: 3px;
    pointer-events: none;
}
.ammo-pip {
    box-sizing: border-box;
    width: 7px;
    height: 7px;
    border-radius: 50%;
    border: 1.5px solid var(--ammo-dot, rgb(255, 142, 0));
    /* 与背景图同色时仍可辨认 */
    box-shadow: 0 0 0 1px #000000;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    background: transparent;
}
.ammo-pip--empty {
    border: none;
    background: #ffffff;
}
.ammo-pip-dot {
    width: 4px;
    height: 4px;
    border-radius: 50%;
    background: var(--ammo-dot, rgb(255, 142, 0));
    box-shadow: 0 0 0 0.5px #000000;
}
.freeze-time-badge,
.haste-time-badge,
.slow-time-badge {
    position: absolute;
    left: 50%;
    transform: translate(-50%, -50%);
    z-index: 5;
    box-sizing: border-box;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #ffffff;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
    line-height: 1;
    pointer-events: none;
    border-radius: 2px;
}
.freeze-time-badge {
    top: 50%;
}
.haste-time-badge {
    top: 40%;
}
.slow-time-badge {
    top: 60%;
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
.save-repro-ok {
    margin: 0.35rem 0 0;
    font-size: 0.85rem;
    color: #8fbc8f;
}
.save-repro-ok code {
    color: #b8dcb8;
}
</style>
