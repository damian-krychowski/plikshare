import { Directive, ElementRef, HostBinding, HostListener, inject, input, output, signal } from '@angular/core';
import { DragStateService, getDraggedExternalId } from '../../services/drag-state.service';
import { DraggableItemDirective, DraggableItemType } from './draggable-item.directive';

export type DropZonePosition = 'before' | 'into' | 'after';
export type DropTargetMode = 'three-zone' | 'two-zone';

const THREE_MIN_FACTOR = 0.40;
const THREE_MAX_FACTOR = 0.60;

@Directive({
    selector: '[appDropTarget]',
    standalone: true,
    exportAs: 'appDropTarget'
})
export class DropTargetDirective {
    private host = inject(ElementRef<HTMLElement>);
    private dragState = inject(DragStateService);
    private dragSource = inject(DraggableItemDirective, { self: true, optional: true });

    mode = input<DropTargetMode>('three-zone', { alias: 'dropMode' });
    selfType = input<DraggableItemType | null>(null, { alias: 'dropSelfType' });
    selfExternalId = input<string | null>(null, { alias: 'dropSelfExternalId' });
    allowSelf = input<boolean>(false, { alias: 'dropAllowSelf' });
    dragOverStayMs = input<number | null>(null);

    dragOverItem = output<{ position: DropZonePosition }>();
    droppedAt = output<{ position: DropZonePosition }>();
    dragOverStay = output<void>();

    private _dropIntoActive = signal(false);
    private _stayTimer: any = null;

    @HostBinding('class.drop-into-target') get hostDropIntoTarget(): boolean {
        return this._dropIntoActive();
    }

    @HostListener('dragover', ['$event']) onDragOver(event: DragEvent) {
        const dragged = this.dragState.draggedItem();
        if (!dragged) 
            return;

        if (!this.allowSelf() && dragged.type === this.selfType() && getDraggedExternalId(dragged) === this.selfExternalId())
            return;

        event.preventDefault();
        event.stopPropagation();
        if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';

        const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
        const y = event.clientY - rect.top;
        const h = rect.height;
        const position = this.computePosition(y, h);
        this._dropIntoActive.set(position === 'into');

        const stayMs = this.dragOverStayMs();
        if (stayMs != null) {
            const inMiddle = y >= h * THREE_MIN_FACTOR && y <= h * THREE_MAX_FACTOR;
            if (inMiddle) this.startStayTimer(stayMs);
            else this.clearStayTimer();
        }

        this.dragOverItem.emit({ position });
    }

    @HostListener('dragleave', ['$event']) onDragLeave(event: DragEvent) {
        const related = event.relatedTarget as Node | null;
        if (related && this.host.nativeElement.contains(related)) return;
        this._dropIntoActive.set(false);
        this.clearStayTimer();
    }

    @HostListener('drop', ['$event']) onDrop(event: DragEvent) {
        event.preventDefault();
        event.stopPropagation();
        const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
        const y = event.clientY - rect.top;
        const h = rect.height;
        const position = this.computePosition(y, h);
        this._dropIntoActive.set(false);
        this.clearStayTimer();
        this.dragSource?.suppressClickAfterDrop();
        this.droppedAt.emit({ position });
    }

    @HostListener('dragend') onDragEnd() {
        this._dropIntoActive.set(false);
        this.clearStayTimer();
    }

    private computePosition(y: number, h: number): DropZonePosition {
        if (this.mode() === 'two-zone') {
            return y < h / 2 ? 'before' : 'after';
        }
        if (y < h * THREE_MIN_FACTOR) return 'before';
        if (y > h * THREE_MAX_FACTOR) return 'after';
        return 'into';
    }

    private startStayTimer(ms: number) {
        if (this._stayTimer != null) return;
        this._stayTimer = setTimeout(() => {
            this._stayTimer = null;
            this.dragOverStay.emit();
        }, ms);
    }

    private clearStayTimer() {
        if (this._stayTimer != null) {
            clearTimeout(this._stayTimer);
            this._stayTimer = null;
        }
    }
}
