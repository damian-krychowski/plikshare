import { Injectable, signal } from '@angular/core';
import { AppFileItem } from '../shared/file-item/file-item.component';
import { AppFolderItem } from '../shared/folder-item/folder-item.component';

export type DraggedItem =
    | { type: 'folder'; folder: AppFolderItem; parentFolderExternalId: string | null }
    | { type: 'file'; file: AppFileItem; parentFolderExternalId: string | null };

export function getDraggedExternalId(d: DraggedItem): string {
    return d.type === 'folder' ? d.folder.externalId : d.file.externalId;
}

@Injectable({ providedIn: 'root' })
export class DragStateService {
    readonly isDragging = signal(false);
    readonly draggedItem = signal<DraggedItem | null>(null);
}
