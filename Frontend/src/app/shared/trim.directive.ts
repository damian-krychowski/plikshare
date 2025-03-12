import { Directive, ElementRef, HostListener, Renderer2 } from "@angular/core";
import { NgControl } from "@angular/forms";

@Directive({
    selector: '[appTrim]',
    standalone: true
})
export class TrimDirective {
    constructor(
        private el: ElementRef,
        private renderer: Renderer2,
        private ngControl: NgControl
    ) { }

    @HostListener('blur')
    onBlur() {
        this.trim();
    }

    private trim() {
        if (this.ngControl && this.ngControl.value && this.ngControl.control) {
            const trimmedValue = this.ngControl.value.trim();
            this.ngControl.control.setValue(trimmedValue);
            this.renderer.setProperty(this.el.nativeElement, 'value', trimmedValue);
        }
    }
}