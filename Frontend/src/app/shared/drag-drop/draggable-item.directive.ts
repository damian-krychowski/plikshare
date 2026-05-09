import { DestroyRef, Directive, ElementRef, HostBinding, HostListener, inject, input, output } from '@angular/core';
import { DragStateService } from '../../services/drag-state.service';

export type DraggableItemType = 'folder' | 'file';

@Directive({
    selector: '[appDraggableItem]',
    standalone: true,
    exportAs: 'appDraggableItem'
})
export class DraggableItemDirective {
    private static readonly CLICK_SUPPRESS_MS = 50;

    private dragState = inject(DragStateService);
    private host = inject(ElementRef<HTMLElement>);

    externalId = input<string>('', { alias: 'draggableExternalId' });
    type = input<DraggableItemType>('folder', { alias: 'draggableType' });
    disabled = input<boolean>(false, { alias: 'draggableDisabled' });

    dragStarted = output<DragEvent>();
    dragEnded = output<void>();

    private _suppressClickAfterDrop = false;
    private _mouseDownOnHandle = false;

    constructor() {
        const handler = (event: MouseEvent) => {
            if (!this._suppressClickAfterDrop) return;
            event.stopPropagation();
            event.preventDefault();
        };
        this.host.nativeElement.addEventListener('click', handler, { capture: true });
        inject(DestroyRef).onDestroy(() => {
            this.host.nativeElement.removeEventListener('click', handler, { capture: true });
        });
    }

    @HostBinding('attr.draggable') get hostDraggable(): string | null {
        return this.disabled() ? null : 'true';
    }

    @HostBinding('attr.data-flip-key') get hostDataFlipKey(): string {
        return this.externalId();
    }

    @HostBinding('class.is-dragging-source') get hostIsDraggingSource(): boolean {
        const d = this.dragState.draggedItem();
        return d != null
            && d.type === this.type()
            && d.externalId === this.externalId();
    }

    @HostListener('mousedown', ['$event']) onMouseDown(event: MouseEvent) {
        const target = event.target as HTMLElement | null;
        this._mouseDownOnHandle = !!target?.closest('.drag-handle');
    }

    @HostListener('dragstart', ['$event']) onDragStart(event: DragEvent) {
        if (!this._mouseDownOnHandle) { event.preventDefault(); return; }
        if (this.disabled()) { event.preventDefault(); return; }
        if (event.dataTransfer) {
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', `${this.type()}:${this.externalId()}`);
        }
        this.dragStarted.emit(event);
    }

    @HostListener('dragend') onDragEnd() {
        this.suppressClickAfterDrop();
        this.dragEnded.emit();
    }

    suppressClickAfterDrop(): void {
        this._suppressClickAfterDrop = true;
        setTimeout(() => { this._suppressClickAfterDrop = false; }, DraggableItemDirective.CLICK_SUPPRESS_MS);
    }
}
