import { Directive, ElementRef, OnDestroy, OnInit, inject, input } from '@angular/core';

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
// Fully lazy: mount only attaches hover listeners. Overflow is measured on
// each mouseenter, at the one moment it matters — no ResizeObserver, no rAF,
// no layout reads while a virtualized list churns rows during scroll. The app
// is zoneless, so the handlers (which touch nothing but WAAPI and styles)
// trigger no change detection by construction.
@Directive({
    selector: '[appMarqueeOnTruncate]',
    standalone: true
})
export class MarqueeOnTruncateDirective implements OnInit, OnDestroy {
    private _el = inject(ElementRef<HTMLElement>);

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

    private _animation: Animation | null = null;
    private _hoverRoot: HTMLElement | null = null;

    ngOnInit(): void {
        const el = this._el.nativeElement;

        const selector = this.hoverWithin();
        const hoverRoot = selector
            ? ((el.closest(selector) as HTMLElement | null) ?? el)
            : el;

        this._hoverRoot = hoverRoot;
        hoverRoot.addEventListener('mouseenter', this._onEnter);
        hoverRoot.addEventListener('mouseleave', this._onLeave);
    }

    private _onEnter = () => {
        this.startAnimation();
    };

    private _onLeave = () => {
        this.stopAnimation();
    };

    private startAnimation(): void {
        this.stopAnimation();

        const el = this._el.nativeElement;
        const distance = el.scrollWidth - el.clientWidth;

        if (distance <= 0) {
            return;
        }

        const inner = el.firstElementChild as HTMLElement | null;
        if (!inner) {
            return;
        }

        // Scroll time derived from distance at a fixed velocity → constant
        // tempo. Pauses are fixed ms, so the keyframe offsets (their share of
        // the total) vary per element — exactly what CSS can't express.
        const scrollMs = (distance / MarqueeOnTruncateDirective.SCROLL_PX_PER_SECOND) * 1000;
        const startPause = MarqueeOnTruncateDirective.START_PAUSE_MS;
        const endPause = MarqueeOnTruncateDirective.END_PAUSE_MS;
        const total = startPause + scrollMs + endPause;

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
                { transform: `translateX(-${distance}px)`, offset: (startPause + scrollMs) / total },
                { transform: `translateX(-${distance}px)`, offset: 1 }
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
        if (this._hoverRoot) {
            this._hoverRoot.removeEventListener('mouseenter', this._onEnter);
            this._hoverRoot.removeEventListener('mouseleave', this._onLeave);
        }

        this.stopAnimation();
    }
}
