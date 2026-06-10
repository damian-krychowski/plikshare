import { afterNextRender, DestroyRef, inject, signal, Signal } from '@angular/core';

// Reports which of the given section elements has scrolled up to (or past) the bottom edge of the
// toolbar — a "scroll spy". Window-scroll + bounding-rect based (capture listener), so it works no
// matter which element actually scrolls. Later sections win when several are stuck at once. Call
// from an injection context; listeners are cleaned up on destroy.
export function trackStuckSection<T extends string>(
    getToolbar: () => HTMLElement | undefined,
    sections: { id: T; getElement: () => HTMLElement | undefined }[]
): Signal<T | null> {
    const stuck = signal<T | null>(null);

    const update = () => {
        const toolbar = getToolbar();
        if (!toolbar) return;

        const toolbarBottom = toolbar.getBoundingClientRect().bottom;

        // Later sections win, so walk from the end and stop at the first stuck one — no need to
        // measure the sections above it.
        let current: T | null = null;
        for (let i = sections.length - 1; i >= 0; i--) {
            const top = sections[i].getElement()?.getBoundingClientRect().top;
            if (top != null && top <= toolbarBottom) {
                current = sections[i].id;
                break;
            }
        }

        stuck.set(current);
    };

    // Coalesce the scroll-event burst into one measurement per frame: smooth scrolling emits
    // several events between paints (and the capture listener hears every scrollable container),
    // so an uncoalesced handler would queue that many rAF callbacks and re-measure layout each time.
    let rafPending = false;

    const handler = () => {
        if (rafPending) return;

        rafPending = true;
        requestAnimationFrame(() => {
            rafPending = false;
            update();
        });
    };

    window.addEventListener('scroll', handler, { capture: true, passive: true });
    window.addEventListener('resize', handler, { passive: true });

    afterNextRender(() => update());

    inject(DestroyRef).onDestroy(() => {
        window.removeEventListener('scroll', handler, { capture: true } as EventListenerOptions);
        window.removeEventListener('resize', handler);
    });

    return stuck;
}
