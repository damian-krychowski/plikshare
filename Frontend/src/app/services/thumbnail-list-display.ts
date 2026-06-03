import { effect, signal, Signal } from '@angular/core';

const STORAGE_PREFIX = 'plikshare:show-thumbnails:';

// Per-workspace "show thumbnails on list rows" state: a persisted toggle plus the URL builder for a
// file's Mini thumbnail. Call from an injection context (field initializer / constructor); the
// toggle reloads whenever the workspace changes.
export function thumbnailListDisplay(
    workspaceExternalId: Signal<string | null>,
    defaultShowThumbnails?: Signal<boolean>) {
    const showThumbnails = signal(false);

    effect(() => {
        const wsId = workspaceExternalId();
        const stored = wsId ? localStorage.getItem(STORAGE_PREFIX + wsId) : null;

        if (stored === 'true')
            showThumbnails.set(true);
        else if (stored === 'false')
            showThumbnails.set(false);
        else
            showThumbnails.set(defaultShowThumbnails?.() ?? false);
    });

    function setShowThumbnails(value: boolean): void {
        showThumbnails.set(value);
        const wsId = workspaceExternalId();
        if (wsId) localStorage.setItem(STORAGE_PREFIX + wsId, value ? 'true' : 'false');
    }

    function getThumbnailUrl(fileExternalId: string): string {
        const wsId = workspaceExternalId();
        return wsId
            ? `/api/workspaces/${wsId}/media/thumbnails/${fileExternalId}`
            : '';
    }

    return {
        showThumbnails: showThumbnails.asReadonly(),
        setShowThumbnails,
        getThumbnailUrl
    };
}
