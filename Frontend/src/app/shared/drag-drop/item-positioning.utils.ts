import { ITEM_POSITION_STEP } from '../../services/folders-and-files.api';

export function computePositionForInsertion<T>(
    sorted: T[],
    insertionIndex: number,
    getPosition: (item: T) => number
): number {
    const idx = Math.max(0, Math.min(insertionIndex, sorted.length));
    const before = idx > 0 ? getPosition(sorted[idx - 1]) : null;
    const after = idx < sorted.length ? getPosition(sorted[idx]) : null;

    if (before !== null && after !== null) {
        const midpoint = Math.floor((before + after) / 2);
        return midpoint === before ? before + 1 : midpoint;
    }
    if (before !== null) return before + ITEM_POSITION_STEP;
    if (after !== null) return Math.max(1, after - ITEM_POSITION_STEP);
    return ITEM_POSITION_STEP;
}
