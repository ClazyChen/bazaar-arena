import type {
    CollectionRow,
    DeckRow,
    DeckSlotEntry,
    DeckSlotsResponse,
    ItemRow,
    SimulateCliEnvelope,
} from "@/types";

async function j<T>(r: Response): Promise<T> {
    if (!r.ok) {
        const t = await r.text();
        throw new Error(t || r.statusText);
    }
    return r.json() as Promise<T>;
}

export async function fetchItems(params: {
    hero?: string;
    size?: string;
    tier?: string;
}): Promise<ItemRow[]> {
    const q = new URLSearchParams();
    if (params.hero) q.set("hero", params.hero);
    if (params.size) q.set("size", params.size);
    if (params.tier) q.set("tier", params.tier);
    const url = "/api/items" + (q.toString() ? `?${q}` : "");
    const data = await j<{ items: ItemRow[] }>(await fetch(url));
    return data.items;
}

export async function fetchCollections(): Promise<CollectionRow[]> {
    const data = await j<{ collections: CollectionRow[] }>(
        await fetch("/api/collections"),
    );
    return data.collections;
}

export async function createCollection(name: string): Promise<CollectionRow> {
    const data = await j<CollectionRow>(
        await fetch("/api/collections", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ name }),
        }),
    );
    return data;
}

export async function fetchDecks(collectionId: number): Promise<DeckRow[]> {
    const data = await j<{ decks: DeckRow[] }>(
        await fetch(`/api/collections/${collectionId}/decks`),
    );
    return data.decks;
}

export async function createDeck(
    collectionId: number,
    name: string,
    playerLevel: number,
): Promise<DeckRow> {
    return j<DeckRow>(
        await fetch(`/api/collections/${collectionId}/decks`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ name, player_level: playerLevel }),
        }),
    );
}

export async function patchDeck(
    deckId: number,
    body: Partial<{ name: string; player_level: number; sort_order: number }>,
): Promise<DeckRow> {
    return j<DeckRow>(
        await fetch(`/api/decks/${deckId}`, {
            method: "PATCH",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
        }),
    );
}

export async function deleteDeck(deckId: number): Promise<void> {
    const r = await fetch(`/api/decks/${deckId}`, { method: "DELETE" });
    if (!r.ok) throw new Error(await r.text());
}

export async function duplicateDeck(deckId: number): Promise<DeckRow> {
    return j<DeckRow>(
        await fetch(`/api/decks/${deckId}/duplicate`, { method: "POST" }),
    );
}

export async function reorderDecks(
    collectionId: number,
    order: number[],
): Promise<void> {
    await j<{ ok: boolean }>(
        await fetch(`/api/collections/${collectionId}/decks/reorder`, {
            method: "PATCH",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ order }),
        }),
    );
}

export async function fetchDeckSlots(deckId: number): Promise<DeckSlotsResponse> {
    return j(await fetch(`/api/decks/${deckId}/slots`));
}

export async function saveDeckSlots(
    deckId: number,
    slots: DeckSlotEntry[],
): Promise<void> {
    await j(
        await fetch(`/api/decks/${deckId}/slots`, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ slots }),
        }),
    );
}

export async function postSimulate(body: {
    deck_id_0: number;
    deck_id_1: number;
    seed?: number | null;
    debug_level?: "detailed" | "summary" | "none";
    max_events?: number;
}): Promise<SimulateCliEnvelope> {
    const payload: Record<string, unknown> = {
        deck_id_0: body.deck_id_0,
        deck_id_1: body.deck_id_1,
    };
    if (body.seed != null) payload.seed = body.seed;
    if (body.debug_level != null) payload.debug_level = body.debug_level;
    if (body.max_events != null) payload.max_events = body.max_events;
    return j(
        await fetch("/api/simulate", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload),
        }),
    );
}

export async function postSimulateBatch(body: {
    deck_id_0: number;
    deck_id_1: number;
    batch_count?: number;
    seed?: number | null;
    threads?: number;
}): Promise<SimulateCliEnvelope> {
    const payload: Record<string, unknown> = {
        deck_id_0: body.deck_id_0,
        deck_id_1: body.deck_id_1,
    };
    if (body.batch_count != null) payload.batch_count = body.batch_count;
    if (body.seed != null) payload.seed = body.seed;
    if (body.threads != null) payload.threads = body.threads;
    return j(
        await fetch("/api/simulate/batch", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload),
        }),
    );
}

/** 获取与后端 POST /api/simulate 相同结构的 job，用于本地 `bazaararena_cli --input job.json` */
export async function fetchReproJobJson(params: {
    deck_id_0: number;
    deck_id_1: number;
    seed: number;
    debug_level?: "detailed" | "summary" | "none";
    max_events?: number;
}): Promise<{ job: unknown; used_seed: number; debug_level: string }> {
    const q = new URLSearchParams();
    q.set("deck_id_0", String(params.deck_id_0));
    q.set("deck_id_1", String(params.deck_id_1));
    q.set("seed", String(params.seed));
    q.set("debug_level", params.debug_level ?? "detailed");
    if (params.max_events != null) q.set("max_events", String(params.max_events));
    return j(await fetch(`/api/simulate/repro-job?${q}`));
}

/** 将复现 JSON 写入仓库 `samples/cli/`（仅本地后端开发环境） */
export async function postSaveCliRepro(body: {
    deck_id_0: number;
    deck_id_1: number;
    seed: number;
    debug_level?: "detailed" | "summary" | "none";
    max_events?: number;
}): Promise<{ ok: boolean; relativePath?: string; filename?: string; error?: string }> {
    const payload: Record<string, unknown> = {
        deck_id_0: body.deck_id_0,
        deck_id_1: body.deck_id_1,
        seed: body.seed,
    };
    if (body.debug_level != null) payload.debug_level = body.debug_level;
    if (body.max_events != null) payload.max_events = body.max_events;
    return j(
        await fetch("/api/simulate/save-cli-repro", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload),
        }),
    );
}
