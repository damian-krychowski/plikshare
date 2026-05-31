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

        let current: T | null = null;
        for (const section of sections) {
            const top = section.getElement()?.getBoundingClientRect().top;
            if (top != null && top <= toolbarBottom) current = section.id;
        }

        stuck.set(current);
    };

    const handler = () => requestAnimationFrame(update);

    window.addEventListener('scroll', handler, { capture: true, passive: true });
    window.addEventListener('resize', handler, { passive: true });

    afterNextRender(() => update());

    inject(DestroyRef).onDestroy(() => {
        window.removeEventListener('scroll', handler, { capture: true } as EventListenerOptions);
        window.removeEventListener('resize', handler);
    });

    return stuck;
}
