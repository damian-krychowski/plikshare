import { Directive, ElementRef, HostBinding, HostListener, OnDestroy, inject, output } from '@angular/core';
import { DragStateService } from '../../services/drag-state.service';

@Directive({
    selector: '[appCdkDragOverStay]',
    standalone: true
})
export class CdkDragOverStayDirective implements OnDestroy {
    @HostBinding('class.cdk-drag-over') isDraggingOver = false;

    cdkDragOverStay = output<void>();

    private readonly _el = inject(ElementRef<HTMLElement>);
    private readonly _dragState = inject(DragStateService);
    private _timer: any = null;

    @HostListener('pointerenter')
    onPointerEnter(): void {
        if (!this._dragState.isDragging()) return;
        this.isDraggingOver = true;
        this.startTimer();
    }

    @HostListener('pointerleave')
    onPointerLeave(): void {
        this.clearTimer();
        this.isDraggingOver = false;
    }

    private startTimer(): void {
        this.clearTimer();
        this._timer = setTimeout(() => {
            if (this._dragState.isDragging()) {
                this.cdkDragOverStay.emit();
            }
            this._timer = null;
        }, 500);
    }

    private clearTimer(): void {
        if (this._timer) {
            clearTimeout(this._timer);
            this._timer = null;
        }
    }

    ngOnDestroy(): void {
        this.clearTimer();
    }
}
