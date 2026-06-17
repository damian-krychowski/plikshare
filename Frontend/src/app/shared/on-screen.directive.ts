import { Directive, ElementRef, OnDestroy, OnInit, output } from '@angular/core';

@Directive({
    selector: '[appOnScreen]',
    standalone: true
})
export class OnScreenDirective implements OnInit, OnDestroy {
    visibilityChange = output<boolean>();

    private _observer?: IntersectionObserver;

    constructor(private _el: ElementRef<HTMLElement>) {
    }

    ngOnInit(): void {
        this._observer = new IntersectionObserver(entries => {
            this.visibilityChange.emit(entries[0]?.isIntersecting ?? false);
        });

        this._observer.observe(this._el.nativeElement);
    }

    ngOnDestroy(): void {
        this._observer?.disconnect();
    }
}
