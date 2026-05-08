import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class DragStateService {
    readonly isDragging = signal(false);
}
