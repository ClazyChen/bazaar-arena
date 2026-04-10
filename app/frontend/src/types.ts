export interface ItemRow {
    name: string;
    hero: string;
    size: number;
    min_tier: number;
    desc: string;
    tags: string[];
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
