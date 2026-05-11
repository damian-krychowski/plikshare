import { computed, Injectable, signal } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import { AppFileItem } from '../shared/file-item/file-item.component';
import { AppFolderItem } from '../shared/folder-item/folder-item.component';

export type DraggedItem = DraggedFolder | DraggedFile;

export type DraggedFolderItem = { item: AppFolderItem; originalIndex: number };
export type DraggedFileItem = { item: AppFileItem; originalIndex: number };

// items[0] is the leader (the item the user grabbed). The rest are siblings
// dragged along because they were selected when the drag started. Single-drag
// has length 1.
export type DraggedFolder = {
    type: 'folder';
    items: ReadonlyArray<DraggedFolderItem>;
    parentFolderExternalId: string | null;
}

export type DraggedFile = {
    type: 'file';
    items: ReadonlyArray<DraggedFileItem>;
    parentFolderExternalId: string | null;
}

export type DragStopOutcome =
    | { reason: 'success'; destinationFolderExternalId: string | null }
    | { reason: 'canceled' };

export type DraggingStoppedEvent = { item: DraggedItem } & DragStopOutcome;

export function getAllDraggedFolders(d: DraggedFolder): AppFolderItem[] {
    return d.items.map(i => i.item);
}

export function getAllDraggedFiles(d: DraggedFile): AppFileItem[] {
    return d.items.map(i => i.item);
}

const EMPTY_ID_SET: ReadonlySet<string> = new Set<string>();

@Injectable({ providedIn: 'root' })
export class DragStateService {
    private readonly _draggedItem = signal<DraggedItem | null>(null);
    private readonly _draggingStopped = new Subject<DraggingStoppedEvent>();

    readonly draggedItem = this._draggedItem.asReadonly();
    readonly isDragging = computed(() => this._draggedItem() != null);
    readonly draggingStopped$: Observable<DraggingStoppedEvent> = this._draggingStopped.asObservable();

    // Drop targets, FLIP and the is-dragging-source class consult this set so
    // every item in the drag (single or multi) is treated as part of the group.
    readonly draggedExternalIds = computed<ReadonlySet<string>>(() => {
        const d = this._draggedItem();
        if (d == null) return EMPTY_ID_SET;

        const ids = new Set<string>();
        for (const i of d.items) ids.add(i.item.externalId);
        return ids;
    });

    startDragging(item: DraggedItem): void {
        this._draggedItem.set(item);
    }

    stopDragging(outcome: DragStopOutcome): void {
        const item = this._draggedItem();

        if (item == null)
            return;

        this._draggedItem.set(null);
        this._draggingStopped.next({ item, ...outcome });
    }

    // When a list rebuilds from a fresh input (drill-in / drill-back) the
    // dragged items may exist in the new collection under different instances
    // with the same externalId. Swap stale references for the fresh ones and
    // propagate the dragged isSelected onto them so counters that iterate the
    // parent signal see the dragged state.
    syncDraggedFolders(currentFolders: AppFolderItem[]): void {
        const current = this._draggedItem();
        if (current == null || current.type !== 'folder') return;

        const byId = new Map<string, AppFolderItem>();
        for (const f of currentFolders) byId.set(f.externalId, f);

        let changed = false;
        const newItems = current.items.map(d => {
            const fresh = byId.get(d.item.externalId);
            if (!fresh || fresh === d.item) return d;
            fresh.isSelected.set(d.item.isSelected());
            changed = true;
            return { item: fresh, originalIndex: d.originalIndex };
        });

        if (changed) {
            this._draggedItem.set({ ...current, items: newItems });
        }
    }

    syncDraggedFiles(currentFiles: AppFileItem[]): void {
        const current = this._draggedItem();
        if (current == null || current.type !== 'file') return;

        const byId = new Map<string, AppFileItem>();
        for (const f of currentFiles) byId.set(f.externalId, f);

        let changed = false;
        const newItems = current.items.map(d => {
            const fresh = byId.get(d.item.externalId);
            if (!fresh || fresh === d.item) return d;
            fresh.isSelected.set(d.item.isSelected());
            changed = true;
            return { item: fresh, originalIndex: d.originalIndex };
        });

        if (changed) {
            this._draggedItem.set({ ...current, items: newItems });
        }
    }
}
