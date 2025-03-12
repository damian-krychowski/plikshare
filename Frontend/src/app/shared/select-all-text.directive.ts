import { Directive, ElementRef, AfterViewInit, NgZone } from '@angular/core';

@Directive({
    selector: '[selectAllText]',
    standalone: true
})
export class SelectAllTextDirective implements AfterViewInit {

    constructor(
        private el: ElementRef<HTMLInputElement>,
        private ngZone: NgZone
    ) { }

    ngAfterViewInit() {
        this.ngZone.runOutsideAngular(() => {
            setTimeout(() => {
                this.el.nativeElement.select(); 
            });
        });
    }
}
