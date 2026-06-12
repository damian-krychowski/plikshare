import { effect, signal, Signal } from '@angular/core';

const STORAGE_PREFIX = 'plikshare:show-minimap:';

export function minimapDisplay(
    workspaceExternalId: Signal<string | null>,
    defaultShowMinimap?: Signal<boolean>,
    disablePersistence?: Signal<boolean>) {
    const showMinimap = signal(false);

    effect(() => {
        const wsId = workspaceExternalId();
        const stored = (!disablePersistence?.() && wsId) ? localStorage.getItem(STORAGE_PREFIX + wsId) : null;

        if (stored === 'true')
            showMinimap.set(true);
        else if (stored === 'false')
            showMinimap.set(false);
        else
            showMinimap.set(defaultShowMinimap?.() ?? false);
    });

    function setShowMinimap(value: boolean): void {
        showMinimap.set(value);

        if (disablePersistence?.())
            return;

        const wsId = workspaceExternalId();
        if (wsId) localStorage.setItem(STORAGE_PREFIX + wsId, value ? 'true' : 'false');
    }

    return {
        showMinimap: showMinimap.asReadonly(),
        setShowMinimap
    };
}
