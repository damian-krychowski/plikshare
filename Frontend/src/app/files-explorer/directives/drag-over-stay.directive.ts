import { Directive, ElementRef, EventEmitter, HostBinding, HostListener, OnDestroy, output, Output } from '@angular/core';

@Directive({
    selector: '[appDragOverStay]',
    standalone: true
})
export class DragOverStayDirective implements OnDestroy {
    @HostBinding('class.drag-over') isDraggingOver: boolean = false;
    
    appDragOverStay = output<void>();

    private timer: any;

    constructor(private el: ElementRef) { }

    @HostListener('dragenter', ['$event'])
    onDragEnter(event: Event): void {
        event.preventDefault();
        event.stopPropagation();

        if (!this.isDraggingOver) {
            this.isDraggingOver = true;
            this.startTimer();
        }
    }

    @HostListener('dragleave', ['$event'])
    onDragLeave(event: Event): void {
        event.preventDefault();
        event.stopPropagation();
        this.clearTimer();
        this.isDraggingOver = false;
    }

    @HostListener('dragover', ['$event'])
    onDragOver(event: Event): void {                
        event.preventDefault();
        if (!this.isDraggingOver) {
            this.isDraggingOver = true;
            this.startTimer();
        }
    }

    private startTimer(): void {
        this.timer = setTimeout(() => {
            this.appDragOverStay.emit();
            this.clearTimer();
        }, 500);
    }

    private clearTimer(): void {
        if (this.timer) {
            clearTimeout(this.timer);
            this.timer = null;
        }
    }

    ngOnDestroy(): void {
        this.clearTimer();
    }
}
