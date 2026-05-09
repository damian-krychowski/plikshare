import { Injectable, signal } from '@angular/core';

export type DraggedItem = {
    type: 'folder' | 'file';
    externalId: string;
    sourceFolderExternalId: string | null;
};

@Injectable({ providedIn: 'root' })
export class DragStateService {
    readonly isDragging = signal(false);
    readonly draggedItem = signal<DraggedItem | null>(null);
}
