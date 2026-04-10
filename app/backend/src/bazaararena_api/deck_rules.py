from __future__ import annotations


def max_slots_for_level(level: int) -> int:
    """与 legacy Deck.MaxSlotsForLevel 一致：1→4，2→6，3→8，其余→10。"""
    if level <= 1:
        return 4
    if level == 2:
        return 6
    if level == 3:
        return 8
    return 10
