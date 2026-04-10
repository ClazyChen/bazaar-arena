import type {
    CollectionRow,
    DeckRow,
    DeckSlotEntry,
    DeckSlotsResponse,
    ItemRow,
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
