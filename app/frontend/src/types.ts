export interface ItemRow {
    name: string;
    hero: string;
    size: number;
    min_tier: number;
    desc: string;
    tags: string[];
    /** tier 0..4 → 数值；时长类（Cooldown 等）为毫秒 */
    tooltip_attrs?: Record<string, number[]> | null;
    source_yaml?: string | null;
    schema_version?: number | null;
}

export interface DeckRow {
    id: number;
    collection_id: number;
    name: string;
    player_level: number;
    sort_order: number;
}

export interface CollectionRow {
    id: number;
    name: string;
    sort_order: number;
    created_at?: string | null;
}

export interface DeckSlotEntry {
    item_name: string;
    tier: number;
}

export interface DeckSlotsResponse {
    deck_id: number;
    player_level: number;
    max_slots: number;
    slots: DeckSlotPayload[];
}

export interface DeckSlotPayload {
    position: number;
    item_name: string;
    tier: number;
}

/** 引擎 CLI 顶层输出（stdin job → stdout JSON） */
export interface SimulateCliEnvelope {
    schemaVersion: number;
    ok: boolean;
    error: string;
    jobId?: string;
    result?: SimulateResultBody;
    /** 后端写入 job 的 RNG 种子，便于 Summary 复现与 GET repro-job */
    usedSeed?: number;
    requestedDebugLevel?: string;
    /** 后端解析到的 bazaararena_cli 绝对路径（用于确认是否为新编引擎） */
    bazaararenaCli?: string | null;
    /** `bazaararena_cli --version` 首行 */
    bazaararenaCliVersion?: string | null;
}

export interface SimulateResultBody {
    winner: number;
    isDraw: boolean;
    endTimeMs: number;
    final?: unknown;
    debug?: SimulateDebugBlock;
}

export interface SimulateDebugBlock {
    level: string;
    events?: BattleDebugEvent[];
    /** summary 模式：人类可读的逐行日志 */
    lines?: string[];
    truncated?: boolean;
}

export type BattleDebugEvent = FrameEndEvent | Record<string, unknown>;

export interface FrameEndEvent {
    t: number;
    kind: "frame_end";
    sides: FrameEndSideSnapshot[];
}

export interface FrameEndSideSnapshot {
    side: number;
    maxHp: number;
    hp: number;
    shield: number;
    burn: number;
    poison: number;
    regen: number;
    resistance: number;
}
