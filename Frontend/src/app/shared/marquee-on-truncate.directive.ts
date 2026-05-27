import { Directive, ElementRef, OnDestroy, Renderer2, inject, signal } from '@angular/core';

// Attached to any element with `overflow: hidden; white-space: nowrap` to flag
// when its inner content overflows the visible width. Sets an `is-truncated`
// class on the host (which CSS rules pick up to enable the marquee animation)
// and a `--marquee-distance` CSS variable equal to `scrollWidth - clientWidth`
// (used by the keyframes as the translate-X target).
//
// Exposes `isTruncated` as a signal so callers can wire conditional UI —
// typically matTooltip that only fires when the text is actually cut off.
//
// Reactivity: ResizeObserver catches container resizes (window/flex parent
// changes), MutationObserver catches content changes (innerHTML updates from
// signal-bound bindings). Both are needed; either alone misses cases.
@Directive({
    selector: '[appMarqueeOnTruncate]',
    exportAs: 'marqueeTruncate',
    standalone: true
})
export class MarqueeOnTruncateDirective implements OnDestroy {
    private _el = inject(ElementRef<HTMLElement>);
    private _renderer = inject(Renderer2);

    isTruncated = signal(false);

    private _ro: ResizeObserver | null = null;
    private _rafHandle = 0;

    constructor() {
        // Defer observer creation + initial measurement to the next frame.
        // The directive runs on every item in a virtualized list (~60+
        // instances), and ResizeObserver instantiation is not free; doing
        // it synchronously in the constructor stalls the initial render
        // of each row. requestAnimationFrame fires after the browser has
        // rendered the row, so the user sees content fast and the
        // truncation check kicks in shortly after.
        //
        // Note: there's no MutationObserver — we rely on the fact that text
        // content changes typically come with a width change that
        // ResizeObserver picks up. The rare edge case (text length changes
        // without container resize) means a stale is-truncated state until
        // the next layout event; acceptable for this UX.
        this._rafHandle = requestAnimationFrame(() => {
            this._rafHandle = 0;
            const el = this._el.nativeElement;

            this._ro = new ResizeObserver(() => this.update());
            this._ro.observe(el);

            this.update();
        });
    }

    // Constant scroll velocity (pixels per second) during the active scroll
    // segments of the animation. Picked for comfortable read pace — too fast
    // is nauseating, too slow is boring. Adjust if needed.
    private static readonly SCROLL_PX_PER_SECOND = 80;

    // Keyframes have scroll occupy 85% of the cycle (10% leading pause,
    // small trailing pause, instant snap back). Must match the SCSS
    // @keyframes percentages — see editable-txt.scss.
    private static readonly SCROLL_FRACTION_OF_CYCLE = 0.85;

    private update(): void {
        const el = this._el.nativeElement;
        const distance = el.scrollWidth - el.clientWidth;
        const truncated = distance > 0;

        if (truncated !== this.isTruncated()) {
            this.isTruncated.set(truncated);
        }

        if (truncated) {
            this._renderer.addClass(el, 'is-truncated');
            // Use native setProperty for CSS custom properties — Renderer2's
            // setStyle doesn't reliably handle `--*` names across Angular
            // versions, the var ends up unset and the animation translates
            // by `var(--marquee-distance, 0px)` = 0 (no visible movement).
            el.style.setProperty('--marquee-distance', `${distance}px`);

            // Total animation duration scales with distance so the scroll
            // segment runs at a fixed px/s — short and long titles look
            // identical in tempo. derived: scroll_segment_time = distance /
            // speed, total = scroll_segment / fraction.
            const durationS = distance
                / MarqueeOnTruncateDirective.SCROLL_PX_PER_SECOND
                / MarqueeOnTruncateDirective.SCROLL_FRACTION_OF_CYCLE;
            el.style.setProperty('--marquee-duration', `${durationS.toFixed(2)}s`);
        } else {
            this._renderer.removeClass(el, 'is-truncated');
            el.style.removeProperty('--marquee-distance');
            el.style.removeProperty('--marquee-duration');
        }
    }

    ngOnDestroy(): void {
        if (this._rafHandle !== 0) {
            cancelAnimationFrame(this._rafHandle);
        }
        this._ro?.disconnect();
    }
}
