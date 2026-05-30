import { inject, Injectable, signal } from '@angular/core';
import { FoldersAndFilesSetApi, ReadyThumbnail, ThumbnailGenerationStatus } from './folders-and-files.api';

export type ThumbnailBatch = {
    batchId: string;
    workspaceExternalId: string;
    name: string;
    total: number;
    completed: number;
    failed: number;
    pending: number;
    isDone: boolean;
};

type PersistedBatch = {
    batchId: string;
    workspaceExternalId: string;
    name: string;
    total: number;
};

const STORAGE_KEY = 'plik:thumbnail-batches';

// Leave a finished batch's bar up briefly so the result (incl. failures) is visible, then drop it.
const AUTO_DISMISS_MS = 6000;

/**
 * App-wide tracker for thumbnail generation batches. Each batch is followed over SSE; progress
 * updates arrive even while the user navigates elsewhere. Active batches are persisted to
 * localStorage and re-subscribed on construction, so an in-flight batch survives a page reload.
 */
@Injectable({ providedIn: 'root' })
export class ThumbnailBatchProgressService {
    private _api = inject(FoldersAndFilesSetApi);

    readonly batches = signal<ThumbnailBatch[]>([]);

    private _unsubscribes = new Map<string, () => void>();
    private _dismissTimers = new Map<string, ReturnType<typeof setTimeout>>();
    private _onReadyThumbnails = new Map<string, (ready: ReadyThumbnail[]) => void>();

    constructor() {
        for (const persisted of this.loadPersisted())
            this.startTracking(persisted, false);
    }

    track(workspaceExternalId: string, batchId: string, name: string, totalFiles: number, onReadyThumbnails?: (ready: ReadyThumbnail[]) => void): void {
        if (onReadyThumbnails)
            this._onReadyThumbnails.set(batchId, onReadyThumbnails);

        this.startTracking(
            { batchId, workspaceExternalId, name, total: totalFiles },
            true);
    }

    dismiss(batchId: string): void {
        this.stopTracking(batchId);
        this.batches.update(list => list.filter(b => b.batchId !== batchId));
        this.persist();
    }

    private startTracking(persisted: PersistedBatch, persist: boolean): void {
        if (this._unsubscribes.has(persisted.batchId))
            return;

        this.batches.update(list => {
            if (list.some(b => b.batchId === persisted.batchId))
                return list;

            return [...list, {
                batchId: persisted.batchId,
                workspaceExternalId: persisted.workspaceExternalId,
                name: persisted.name,
                total: persisted.total,
                completed: 0,
                failed: 0,
                pending: persisted.total,
                isDone: false,
            }];
        });

        if (persist)
            this.persist();

        const unsubscribe = this._api.subscribeThumbnailBatch(
            persisted.workspaceExternalId,
            persisted.batchId,
            status => this.onStatus(persisted.batchId, status));

        this._unsubscribes.set(persisted.batchId, unsubscribe);
    }

    private onStatus(batchId: string, status: ThumbnailGenerationStatus): void {
        this.batches.update(list => list.map(batch => batch.batchId === batchId
            ? {
                ...batch,
                total: status.total,
                completed: status.completed,
                failed: status.failed,
                pending: status.pending,
                isDone: status.pending === 0,
            }
            : batch));

        if (status.readyThumbnails?.length)
            this._onReadyThumbnails.get(batchId)?.(status.readyThumbnails);

        if (status.pending === 0)
            this.onBatchDone(batchId);
    }

    private onBatchDone(batchId: string): void {
        // Close the stream (server is done) and drop it from persistence, so a refresh during the
        // brief "completed" display won't resurrect a finished batch.
        const unsubscribe = this._unsubscribes.get(batchId);
        if (unsubscribe) {
            unsubscribe();
            this._unsubscribes.delete(batchId);
        }
        this.persist();

        this._onReadyThumbnails.delete(batchId);

        if (this._dismissTimers.has(batchId))
            return;

        const timer = setTimeout(
            () => {
                this._dismissTimers.delete(batchId);
                this.batches.update(list => list.filter(b => b.batchId !== batchId));
            },
            AUTO_DISMISS_MS);

        this._dismissTimers.set(batchId, timer);
    }

    private stopTracking(batchId: string): void {
        const unsubscribe = this._unsubscribes.get(batchId);
        if (unsubscribe) {
            unsubscribe();
            this._unsubscribes.delete(batchId);
        }

        const timer = this._dismissTimers.get(batchId);
        if (timer) {
            clearTimeout(timer);
            this._dismissTimers.delete(batchId);
        }

        this._onReadyThumbnails.delete(batchId);
    }

    private persist(): void {
        // Only still-running batches are worth resuming after a reload.
        const persisted: PersistedBatch[] = this.batches()
            .filter(batch => !batch.isDone)
            .map(batch => ({
                batchId: batch.batchId,
                workspaceExternalId: batch.workspaceExternalId,
                name: batch.name,
                total: batch.total,
            }));

        try {
            if (persisted.length === 0)
                localStorage.removeItem(STORAGE_KEY);
            else
                localStorage.setItem(STORAGE_KEY, JSON.stringify(persisted));
        } catch {
            // Storage unavailable — tracking still works, it just won't survive a reload.
        }
    }

    private loadPersisted(): PersistedBatch[] {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);

            if (raw)
                return JSON.parse(raw) as PersistedBatch[];
        } catch {
            // Corrupt/blocked storage — start with nothing to resume.
        }

        return [];
    }
}
