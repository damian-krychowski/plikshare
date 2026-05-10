import { computed, Injectable, signal } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import { AppFileItem } from '../shared/file-item/file-item.component';
import { AppFolderItem } from '../shared/folder-item/folder-item.component';

export type DraggedItem = DraggedFolder | DraggedFile;

export type DraggedFolder = {
    type: 'folder';
    folder: AppFolderItem;
    parentFolderExternalId: string | null;
    originalIndexInParentFolder: number;
}

export type DraggedFile = {
    type: 'file';
    file: AppFileItem;
    parentFolderExternalId: string | null;
    originalIndexInParentFolder: number;
}

export type DragStopOutcome =
    | { reason: 'success'; destinationFolderExternalId: string | null }
    | { reason: 'canceled' };

export type DraggingStoppedEvent = { item: DraggedItem } & DragStopOutcome;

export function getDraggedExternalId(d: DraggedItem): string {
    return d.type === 'folder' ? d.folder.externalId : d.file.externalId;
}

@Injectable({ providedIn: 'root' })
export class DragStateService {
    private readonly _draggedItem = signal<DraggedItem | null>(null);
    private readonly _draggingStopped = new Subject<DraggingStoppedEvent>();

    readonly draggedItem = this._draggedItem.asReadonly();
    readonly isDragging = computed(() => this._draggedItem() != null);
    readonly draggingStopped$: Observable<DraggingStoppedEvent> = this._draggingStopped.asObservable();

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
}
