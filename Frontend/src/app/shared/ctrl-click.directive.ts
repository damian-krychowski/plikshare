import { Directive, HostListener, output } from '@angular/core';

@Directive({
    selector: '[appCtrlClick]',
    standalone: true
})
export class CtrlClickDirective {

    ctrlClick = output<MouseEvent>();
    shiftClick = output<MouseEvent>();

    @HostListener('mousedown', ['$event'])
    onMouseDown(event: MouseEvent): void {
        if (event.shiftKey) {
            event.preventDefault();
        }
    }

    @HostListener('click', ['$event'])
    onClick(event: MouseEvent): void {
        if (event.ctrlKey || event.metaKey) {
            event.preventDefault();
            this.ctrlClick.emit(event);
        } else if (event.shiftKey) {
            event.preventDefault();
            this.shiftClick.emit(event);
        }
    }
}
