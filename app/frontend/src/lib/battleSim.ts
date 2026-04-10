import type {
    BattleDebugEvent,
    FrameEndEvent,
    FrameEndSideSnapshot,
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
        });
    }
    return out;
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
