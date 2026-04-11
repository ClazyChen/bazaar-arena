import type {
    BattleDebugEvent,
    FrameEndEvent,
    FrameEndItemSnapshot,
    FrameEndSideSnapshot,
    HpDamageEvent,
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
            FreezeRemaining: Number(o.FreezeRemaining ?? 0),
            AmmoCap: Number(o.AmmoCap ?? 0),
            AmmoRemaining: Number(o.AmmoRemaining ?? 0),
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

/** 解析 detailed 中 `kind: "damage"`（含沙尘暴等同形事件） */
export function extractHpDamageEvents(events: BattleDebugEvent[] | undefined): HpDamageEvent[] {
    if (!events?.length) return [];
    const out: HpDamageEvent[] = [];
    for (const e of events) {
        if (!e || typeof e !== "object") continue;
        const o = e as Record<string, unknown>;
        if (o.kind !== "damage") continue;
        const t = o.t;
        const damage = o.damage;
        const targetSideRaw = o.targetSide;
        if (typeof t !== "number" || typeof damage !== "number") continue;
        if (!Number.isFinite(t) || !Number.isFinite(damage) || damage <= 0) continue;
        const ts =
            typeof targetSideRaw === "number"
                ? targetSideRaw
                : typeof targetSideRaw === "string"
                  ? Number(targetSideRaw)
                  : NaN;
        if (ts !== 0 && ts !== 1) continue;
        out.push({
            t,
            damage,
            targetSide: ts as 0 | 1,
            isCrit: o.isCrit === true,
        });
    }
    out.sort((a, b) => (a.t !== b.t ? a.t - b.t : a.targetSide - b.targetSide));
    return out;
}

/**
 * 将伤害归属到时间轴步：区间 (T_{i-1}, T_i]，T_{-1} = -1。
 */
export function hpDamageEventsForStep(
    steps: PlaybackStep[],
    all: HpDamageEvent[],
    stepIndex: number,
): { side0: HpDamageEvent[]; side1: HpDamageEvent[] } {
    const empty = { side0: [] as HpDamageEvent[], side1: [] as HpDamageEvent[] };
    if (stepIndex < 0 || stepIndex >= steps.length) return empty;
    const prevT = stepIndex > 0 ? steps[stepIndex - 1].tMs : -1;
    const curT = steps[stepIndex].tMs;
    const side0: HpDamageEvent[] = [];
    const side1: HpDamageEvent[] = [];
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
