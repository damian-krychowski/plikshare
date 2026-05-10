import { Directive, ElementRef, HostBinding, HostListener, inject, input, output, signal } from '@angular/core';
import { DragStateService, getDraggedExternalId } from '../../services/drag-state.service';
import { DraggableItemDirective } from './draggable-item.directive';

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
    dragOverStayMs = input<number | null>(null);

    dragOverItem = output<{ position: DropZonePosition }>();
    droppedAt = output<{ position: DropZonePosition }>();
    dragOverStay = output<void>();

    private _dropIntoActive = signal(false);
    private _stayPending = signal(false);
    private _stayTimer: any = null;

    @HostBinding('class.drop-into-target') get hostDropIntoTarget(): boolean {
        return this._dropIntoActive();
    }

    @HostBinding('class.drop-stay-pending') get hostDropStayPending(): boolean {
        return this._stayPending();
    }

    @HostListener('dragover', ['$event']) onDragOver(event: DragEvent) {
        const dragged = this.dragState.draggedItem();

        if (!dragged) {
            // OS-file drag (no internal item picked up) — drive drill-in stay
            // timer + full-item highlight so the user can hover a folder to
            // enter it. No reorder semantics.
            if (this.isOsFileDrag(event)) {
                event.preventDefault();
                this._dropIntoActive.set(true);
                const stayMs = this.dragOverStayMs();
                if (stayMs != null) this.startStayTimer(stayMs);
            }
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';

        const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
        const y = event.clientY - rect.top;
        const h = rect.height;
        const position = this.computePosition(y, h);
        const overSelf = this.isOverSelf();

        this._dropIntoActive.set(position === 'into' && !overSelf);

        const stayMs = this.dragOverStayMs();
        if (stayMs != null) {
            // Trigger drill-in countdown whenever we're in an 'into' zone —
            // this covers both same-type drag (middle 20%) and foreign drag (full item).
            // Suppressed when hovering over the dragged item itself.
            if (position === 'into' && !overSelf) this.startStayTimer(stayMs);
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
        // OS-file drop: directive only drives drill-down on hover. Let the
        // drop bubble so an outer appDropFiles can pick it up.
        if (this.dragState.draggedItem() == null) {
            this.clearStayTimer();
            return;
        }

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
        const mode = this.mode();

        // Foreign drag onto a three-zone target — the host can only accept it
        // as 'into' (no reorder semantics for cross-type drops). Whole item
        // becomes the drop zone, with full highlight via drop-into-target.
        if (mode === 'three-zone' && this.isForeignDrag()) {
            return 'into';
        }

        if (mode === 'two-zone') {
            return y < h / 2 ? 'before' : 'after';
        }

        if (y < h * THREE_MIN_FACTOR) return 'before';
        if (y > h * THREE_MAX_FACTOR) return 'after';
        return 'into';
    }

    private isOsFileDrag(event: DragEvent): boolean {
        const types = event.dataTransfer?.types;
        if (!types) return false;
        for (let i = 0; i < types.length; i++) {
            if (types[i] === 'Files') return true;
        }
        return false;
    }

    private isForeignDrag(): boolean {
        const dragged = this.dragState.draggedItem();

        if (dragged == null || this.dragSource == null)
            return false;

        return dragged.type !== this.dragSource.type();
    }

    private isOverSelf(): boolean {
        const dragged = this.dragState.draggedItem();

        if (dragged == null || this.dragSource == null)
            return false;

        return dragged.type === this.dragSource.type()
            && getDraggedExternalId(dragged) === this.dragSource.externalId();
    }

    private startStayTimer(ms: number) {
        if (this._stayTimer != null) return;
        this._stayPending.set(true);
        this._stayTimer = setTimeout(() => {
            this._stayTimer = null;
            this._stayPending.set(false);
            this.dragOverStay.emit();
        }, ms);
    }

    private clearStayTimer() {
        if (this._stayTimer != null) {
            clearTimeout(this._stayTimer);
            this._stayTimer = null;
        }
        this._stayPending.set(false);
    }
}
