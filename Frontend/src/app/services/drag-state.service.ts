import { computed, Injectable, signal } from '@angular/core';
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
    //todo add original index
}

export function getDraggedExternalId(d: DraggedItem): string {
    return d.type === 'folder' ? d.folder.externalId : d.file.externalId;
}

@Injectable({ providedIn: 'root' })
export class DragStateService {
    readonly draggedItem = signal<DraggedItem | null>(null);
    readonly isDragging = computed(() => this.draggedItem() != null);
}
