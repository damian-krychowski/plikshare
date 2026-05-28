import { Directive, ElementRef, OnDestroy, inject, input, signal } from '@angular/core';

// Attached to a clip element (`overflow: hidden; white-space: nowrap;
// text-overflow: ellipsis`) whose FIRST child element is the text span. When
// the text overflows, hovering the row scrolls it like a marquee.
//
// Why JS (Web Animations API) instead of a CSS @keyframes animation: we need
// BOTH a constant scroll velocity (so every name reads at the same tempo) AND
// a constant pause before each repeat (so a name overflowing by one letter
// waits just as long as a very long one). With CSS the keyframe offsets are
// percentages of the duration, so you can fix one or the other but not both —
// a fixed pause needs a per-element keyframe offset, which only JS can compute.
//
// Exposes `isTruncated` as a signal so callers can wire conditional UI.
@Directive({
    selector: '[appMarqueeOnTruncate]',
    exportAs: 'marqueeTruncate',
    standalone: true
})
export class MarqueeOnTruncateDirective implements OnDestroy {
    private _el = inject(ElementRef<HTMLElement>);

    isTruncated = signal(false);

    // Optional CSS selector of an ancestor whose hover starts the marquee.
    // Empty (the default) means hover on the host element itself, so each
    // marquee target reacts only to its own hover — a row with several targets
    // (e.g. tree node name + path) scrolls just the one under the cursor.
    // Pass a selector (e.g. ".item-bar") to widen the trigger to a whole row,
    // matching the old `:host-context(.item-bar:hover)` behaviour in list-view.
    hoverWithin = input<string>('', { alias: 'appMarqueeOnTruncate' });

    // Scroll velocity (px/s) — constant across all names, same as the original
    // CSS implementation.
    private static readonly SCROLL_PX_PER_SECOND = 80;

    // Short pause at the start (lets the beginning of the name register before
    // it moves) and a fixed pause at the end before the instant snap-back.
    // Both are constant wall-clock times, independent of overflow distance.
    private static readonly START_PAUSE_MS = 400;
    private static readonly END_PAUSE_MS = 1000;

    private _ro: ResizeObserver | null = null;
    private _rafHandle = 0;
    private _animation: Animation | null = null;

    // The element whose hover starts the marquee. We walk up to the row
    // container so hovering anywhere on the row (icon, actions) scrolls the
    // name — matching the previous CSS `:host-context(.item-bar:hover)` /
    // `.node-content:hover` behaviour. Falls back to the host element.
    private _hoverRoot: HTMLElement | null = null;
    private _isHovered = false;
    private _distance = 0;

    constructor() {
        // Defer setup to the next frame — ResizeObserver instantiation isn't
        // free and the directive runs on every row of a virtualized list, so
        // doing it synchronously stalls each row's initial render.
        this._rafHandle = requestAnimationFrame(() => {
            this._rafHandle = 0;
            const el = this._el.nativeElement;

            this._ro = new ResizeObserver(() => this.measure());
            this._ro.observe(el);

            const selector = this.hoverWithin();
            const hoverRoot = selector
                ? ((el.closest(selector) as HTMLElement | null) ?? el)
                : el;
            this._hoverRoot = hoverRoot;
            hoverRoot.addEventListener('mouseenter', this._onEnter);
            hoverRoot.addEventListener('mouseleave', this._onLeave);

            this.measure();
        });
    }

    private _onEnter = () => {
        this._isHovered = true;
        this.startAnimation();
    };

    private _onLeave = () => {
        this._isHovered = false;
        this.stopAnimation();
    };

    private measure(): void {
        const el = this._el.nativeElement;
        const distance = el.scrollWidth - el.clientWidth;
        const truncated = distance > 0;

        if (truncated !== this.isTruncated()) {
            this.isTruncated.set(truncated);
        }

        this._distance = truncated ? distance : 0;

        // Re-sync a running animation if the row resized mid-hover.
        if (this._isHovered) {
            this.startAnimation();
        }
    }

    private startAnimation(): void {
        this.stopAnimation();

        if (this._distance <= 0) {
            return;
        }

        const inner = this._el.nativeElement.firstElementChild as HTMLElement | null;
        if (!inner) {
            return;
        }

        // Scroll time derived from distance at a fixed velocity → constant
        // tempo. Pauses are fixed ms, so the keyframe offsets (their share of
        // the total) vary per element — exactly what CSS can't express.
        const scrollMs = (this._distance / MarqueeOnTruncateDirective.SCROLL_PX_PER_SECOND) * 1000;
        const startPause = MarqueeOnTruncateDirective.START_PAUSE_MS;
        const endPause = MarqueeOnTruncateDirective.END_PAUSE_MS;
        const total = startPause + scrollMs + endPause;

        const el = this._el.nativeElement;
        // Ellipsis would sit next to the moving text — clip it while scrolling.
        el.style.textOverflow = 'clip';
        // translateX only moves inline-block/block boxes; the span is inline by
        // default so ellipsis works when not animating.
        inner.style.display = 'inline-block';
        inner.style.willChange = 'transform';

        this._animation = inner.animate(
            [
                { transform: 'translateX(0)', offset: 0 },
                { transform: 'translateX(0)', offset: startPause / total },
                { transform: `translateX(-${this._distance}px)`, offset: (startPause + scrollMs) / total },
                { transform: `translateX(-${this._distance}px)`, offset: 1 }
            ],
            {
                duration: total,
                iterations: Infinity,
                easing: 'linear'
            }
        );
    }

    private stopAnimation(): void {
        this._animation?.cancel();
        this._animation = null;

        const el = this._el.nativeElement;
        el.style.removeProperty('text-overflow');

        const inner = el.firstElementChild as HTMLElement | null;
        if (inner) {
            inner.style.removeProperty('display');
            inner.style.removeProperty('will-change');
        }
    }

    ngOnDestroy(): void {
        if (this._rafHandle !== 0) {
            cancelAnimationFrame(this._rafHandle);
        }
        this._ro?.disconnect();

        if (this._hoverRoot) {
            this._hoverRoot.removeEventListener('mouseenter', this._onEnter);
            this._hoverRoot.removeEventListener('mouseleave', this._onLeave);
        }

        this.stopAnimation();
    }
}
