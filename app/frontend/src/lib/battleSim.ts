import type {
    BattleDebugEvent,
    FrameEndEvent,
    FrameEndItemSnapshot,
    FrameEndSideSnapshot,
    HudFloatEvent,
    HudFloatKind,
    SimulateResultBody,
} from "@/types";

/** 与 engine `Simulator::Frame` 一致：50ms */
export const BATTLE_TICK_MS = 50;

export interface PlaybackStep {
    /** 引擎时间轴（毫秒）；结局帧使用 `result.endTimeMs` */
    tMs: number;
    sides: FrameEndSideSnapshot[];
    /** 来自 `frame_end` 或权威结局 `final` */
    kind: "frame" | "final";
}

export function extractFrameEnds(events: BattleDebugEvent[] | undefined): FrameEndEvent[] {
    if (!events || events.length === 0) return [];
    const out: FrameEndEvent[] = [];
    for (const e of events) {
        if (!e || typeof e !== "object") continue;
        const k = (e as { kind?: unknown }).kind;
        if (k !== "frame_end") continue;
        const t = (e as { t?: unknown }).t;
        const sides = (e as { sides?: unknown }).sides;
        if (typeof t !== "number" || !Array.isArray(sides)) continue;
        out.push(e as FrameEndEvent);
    }
    out.sort((a, b) => a.t - b.t);
    return out;
}

function parseItemSnapshots(raw: unknown): FrameEndItemSnapshot[] | undefined {
    if (!Array.isArray(raw) || raw.length === 0) return undefined;
    const out: FrameEndItemSnapshot[] = [];
    for (const it of raw) {
        if (!it || typeof it !== "object") continue;
        const o = it as Record<string, unknown>;
        const idxRaw = o.itemIndex;
        const itemIndex =
            typeof idxRaw === "number" ? idxRaw : typeof idxRaw === "string" ? Number(idxRaw) : out.length;
        out.push({
            itemIndex: Number.isFinite(itemIndex) ? itemIndex : out.length,
            ChargedTime: Number(o.ChargedTime ?? 0),
            Cooldown: Number(o.Cooldown ?? 0),
            Damage: Number(o.Damage ?? 0),
            Shield: Number(o.Shield ?? 0),
            Heal: Number(o.Heal ?? 0),
            Burn: Number(o.Burn ?? 0),
            Poison: Number(o.Poison ?? 0),
            Regen: Number(o.Regen ?? 0),
            FreezeRemaining: Number(o.FreezeRemaining ?? 0),
            HasteRemaining: Number(o.HasteRemaining ?? 0),
            SlowRemaining: Number(o.SlowRemaining ?? 0),
            AmmoCap: Number(o.AmmoCap ?? 0),
            AmmoRemaining: Number(o.AmmoRemaining ?? 0),
            LifeSteal: Number(o.LifeSteal ?? 0),
            InFlight: Number(o.InFlight ?? 0),
            Multicast: Number(o.Multicast ?? 0),
            Destroyed: Number(o.Destroyed ?? 0),
            name: typeof o.name === "string" ? o.name : undefined,
        });
    }
    return out.length > 0 ? out : undefined;
}

function parseFinalSides(final: unknown): FrameEndSideSnapshot[] | null {
    if (!final || typeof final !== "object") return null;
    const raw = (final as { sides?: unknown }).sides;
    if (!Array.isArray(raw) || raw.length < 2) return null;
    const out: FrameEndSideSnapshot[] = [];
    for (let i = 0; i < raw.length; i++) {
        const s = raw[i];
        if (!s || typeof s !== "object") return null;
        const o = s as Record<string, unknown>;
        out.push({
            side: typeof o.side === "number" ? o.side : i,
            maxHp: Number(o.maxHp),
            hp: Number(o.hp),
            shield: Number(o.shield),
            burn: Number(o.burn),
            poison: Number(o.poison),
            regen: Number(o.regen),
            resistance: Number(o.resistance),
            items: parseItemSnapshots(o.items),
        });
    }
    return out;
}

/** 未充能遮罩高度比例：0 表示已充满或无需显示；接近 1 表示几乎全未充能 */
export function unchargedOverlayFill(chargedMs: number, cooldownMs: number): number {
    if (cooldownMs <= 0) return 0;
    return Math.max(0, Math.min(1, 1 - chargedMs / cooldownMs));
}

function parseSide01(v: unknown): 0 | 1 | null {
    const n = typeof v === "number" ? v : typeof v === "string" ? Number(v) : NaN;
    if (n === 0 || n === 1) return n;
    return null;
}

const FLOAT_KIND_ORDER: Record<HudFloatKind, number> = {
    damage: 0,
    burn: 1,
    poison: 2,
    heal: 3,
    shield: 4,
    regen: 5,
};

/**
 * 解析 detailed 中飘字相关事件：`damage` / `burn` / `poison` / `heal` / `shield` / `regen`（与引擎 Sink 字段一致）。
 */
export function extractHudFloatEvents(events: BattleDebugEvent[] | undefined): HudFloatEvent[] {
    if (!events?.length) return [];
    const out: HudFloatEvent[] = [];
    for (const e of events) {
        if (!e || typeof e !== "object") continue;
        const o = e as Record<string, unknown>;
        const t = o.t;
        if (typeof t !== "number" || !Number.isFinite(t)) continue;
        const kind = o.kind;
        if (kind === "damage") {
            const damage = o.damage;
            const ts = parseSide01(o.targetSide);
            if (typeof damage !== "number" || !Number.isFinite(damage) || damage <= 0 || ts === null) continue;
            out.push({
                t,
                targetSide: ts,
                kind: "damage",
                amount: damage,
                isCrit: o.isCrit === true,
            });
        } else if (kind === "burn") {
            const burn = o.burn;
            const ts = parseSide01(o.targetSide);
            if (typeof burn !== "number" || !Number.isFinite(burn) || burn <= 0 || ts === null) continue;
            out.push({ t, targetSide: ts, kind: "burn", amount: burn, isCrit: o.isCrit === true });
        } else if (kind === "poison") {
            const poison = o.poison;
            const ts = parseSide01(o.targetSide);
            if (typeof poison !== "number" || !Number.isFinite(poison) || poison <= 0 || ts === null) continue;
            out.push({ t, targetSide: ts, kind: "poison", amount: poison, isCrit: o.isCrit === true });
        } else if (kind === "heal") {
            const heal = o.heal;
            const ts = parseSide01(o.sourceSide);
            if (typeof heal !== "number" || !Number.isFinite(heal) || heal <= 0 || ts === null) continue;
            out.push({ t, targetSide: ts, kind: "heal", amount: heal, isCrit: o.isCrit === true });
        } else if (kind === "shield") {
            const shield = o.shield;
            const ts = parseSide01(o.sourceSide);
            if (typeof shield !== "number" || !Number.isFinite(shield) || shield <= 0 || ts === null) continue;
            out.push({ t, targetSide: ts, kind: "shield", amount: shield, isCrit: o.isCrit === true });
        } else if (kind === "regen") {
            const regen = o.regen;
            const ts = parseSide01(o.targetSide);
            if (typeof regen !== "number" || !Number.isFinite(regen) || regen <= 0 || ts === null) continue;
            out.push({ t, targetSide: ts, kind: "regen", amount: regen, isCrit: o.isCrit === true });
        }
    }
    out.sort((a, b) => {
        if (a.t !== b.t) return a.t - b.t;
        if (a.targetSide !== b.targetSide) return a.targetSide - b.targetSide;
        return FLOAT_KIND_ORDER[a.kind] - FLOAT_KIND_ORDER[b.kind];
    });
    return out;
}

/**
 * 将飘字事件归属到时间轴步：区间 (T_{i-1}, T_i]，T_{-1} = -1。
 */
export function hudFloatEventsForStep(
    steps: PlaybackStep[],
    all: HudFloatEvent[],
    stepIndex: number,
): { side0: HudFloatEvent[]; side1: HudFloatEvent[] } {
    const empty = { side0: [] as HudFloatEvent[], side1: [] as HudFloatEvent[] };
    if (stepIndex < 0 || stepIndex >= steps.length) return empty;
    const prevT = stepIndex > 0 ? steps[stepIndex - 1].tMs : -1;
    const curT = steps[stepIndex].tMs;
    const side0: HudFloatEvent[] = [];
    const side1: HudFloatEvent[] = [];
    for (const ev of all) {
        if (ev.t > prevT && ev.t <= curT) {
            if (ev.targetSide === 0) side0.push(ev);
            else side1.push(ev);
        }
    }
    return { side0, side1 };
}

/**
 * 战斗在判定胜负当帧可能不再产生 `frame_end`，结局快照以 `result.final` 为准。
 * 时间线 = 全部 `frame_end` +（若有）与 `endTimeMs` 对齐的结局帧。
 */
export function buildPlaybackTimeline(
    events: BattleDebugEvent[] | undefined,
    result: SimulateResultBody | undefined,
): PlaybackStep[] {
    const frames = extractFrameEnds(events);
    const steps: PlaybackStep[] = frames.map((f) => ({
        tMs: f.t,
        sides: f.sides as FrameEndSideSnapshot[],
        kind: "frame",
    }));

    const finalSides = result ? parseFinalSides(result.final) : null;
    if (!finalSides || !result || typeof result.endTimeMs !== "number") {
        return steps;
    }

    const endMs = result.endTimeMs;
    if (steps.length === 0) {
        return [{ tMs: endMs, sides: finalSides, kind: "final" }];
    }

    const last = steps[steps.length - 1];
    if (last && last.tMs === endMs) {
        last.sides = finalSides;
        last.kind = "final";
        return steps;
    }
    steps.push({ tMs: endMs, sides: finalSides, kind: "final" });
    return steps;
}

export function formatTimeSec(tMs: number): string {
    if (!Number.isFinite(tMs)) return "0.00";
    return (tMs / 1000).toFixed(2);
}
