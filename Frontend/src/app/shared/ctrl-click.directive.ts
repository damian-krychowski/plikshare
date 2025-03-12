import { Directive, EventEmitter, HostListener, output, Output } from '@angular/core';

@Directive({
    selector: '[appCtrlClick]', // Use this selector to apply the directive
    standalone: true
})
export class CtrlClickDirective {

    ctrlClick = output<MouseEvent>();

    constructor() { }

    @HostListener('click', ['$event'])
    onClick(event: MouseEvent): void {
        // Check for Ctrl or Meta (Cmd) key
        if (event.ctrlKey || event.metaKey) {
            event.preventDefault(); // Prevent default if needed
            this.ctrlClick.emit(event); // Emit the event
        }
    }
}
