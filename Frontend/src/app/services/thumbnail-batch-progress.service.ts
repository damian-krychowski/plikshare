import { computed, Injectable, signal } from '@angular/core';
import { ThumbnailGenerationStatus } from './folders-and-files.api';

// Per-batch live state derived from the SSE stream. Aggregated app-wide (file external ids are
// globally unique) so any explorer can read it reactively — including after a reload, where the
// owning component resubscribes the persisted batches and this state simply re-fills.
type BatchLiveState = {
    processingFileIds: ReadonlySet<string>;
    readyMiniEtagByFileId: ReadonlyMap<string, string>;
};

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

// Transport handlers the owning component injects per batch. The service stays transport-agnostic
// so explorers backed by different APIs (workspace, box, external-link, ...) can each plug in their
// own implementation — instead of the service hardcoding one (workspace-only) API and silently
// breaking everywhere else.
export type ThumbnailBatchHandlers = {
    subscribe: (
        batchId: string,
        onStatus: (status: ThumbnailGenerationStatus) => void,
        includeOutstandingFileIds: boolean
    ) => () => void;

    cancel: (batchId: string) => Promise<unknown>;
};

export type PersistedBatch = {
    batchId: string;
    workspaceExternalId: string;
    name: string;
    total: number;
};

const STORAGE_KEY = 'plik:thumbnail-batches';

// Leave a finished batch's bar up briefly so the result (incl. failures) is visible, then drop it.
const AUTO_DISMISS_MS = 2500;

/**
 * App-wide tracker for thumbnail generation batches. Each batch is followed over SSE; progress
 * updates arrive even while the user navigates elsewhere. The service is transport-agnostic — the
 * owning component supplies `subscribe` / `cancel` handlers per batch. Active batches are persisted
 * to localStorage; the owning component resurrects them after a reload by reading
 * `getPersistedBatches()` and calling `resume(...)` with handlers from its own API.
 */
@Injectable({ providedIn: 'root' })
export class ThumbnailBatchProgressService {
    readonly batches = signal<ThumbnailBatch[]>([]);

    private _liveByBatch = signal<ReadonlyMap<string, BatchLiveState>>(new Map());

    // Union of files still being processed across all tracked batches — drives the per-file
    // "processing" indicator.
    readonly processingFileIds = computed<ReadonlySet<string>>(() => {
        const union = new Set<string>();
        for (const live of this._liveByBatch().values())
            for (const fileId of live.processingFileIds)
                union.add(fileId);
        return union;
    });

    // Merged fileExternalId -> Mini-thumbnail etag across batches. A consumer applies these to its
    // file items so freshly-generated thumbnails appear live (and after a reload).
    readonly readyMiniEtags = computed<ReadonlyMap<string, string>>(() => {
        const merged = new Map<string, string>();
        for (const live of this._liveByBatch().values())
            for (const [fileId, etag] of live.readyMiniEtagByFileId)
                merged.set(fileId, etag);
        return merged;
    });

    private _unsubscribes = new Map<string, () => void>();
    private _dismissTimers = new Map<string, ReturnType<typeof setTimeout>>();
    private _handlersByBatch = new Map<string, ThumbnailBatchHandlers>();

    // Persisted batches the caller may want to resurrect after a reload. Pure read — the caller
    // decides which ones it owns (e.g. by matching workspaceExternalId) and calls `resume(...)`.
    getPersistedBatches(): PersistedBatch[] {
        return this.loadPersisted();
    }

    // Fresh start: the caller knows exactly which files it triggered, so we seed the spinner set
    // locally and tell the server not to resend that (potentially huge) outstanding list.
    track(args: {
        workspaceExternalId: string;
        batchId: string;
        name: string;
        fileExternalIds: readonly string[];
        handlers: ThumbnailBatchHandlers;
    }): void {
        this.startTracking(
            { batchId: args.batchId, workspaceExternalId: args.workspaceExternalId, name: args.name, total: args.fileExternalIds.length },
            {
                persist: true,
                includeOutstandingFileIds: false,
                initialProcessingIds: args.fileExternalIds,
                handlers: args.handlers,
            });
    }

    // Resume after reload: we no longer know which files were outstanding, so we ask the server for
    // the full outstanding list in the first event to repopulate the spinners.
    resume(args: {
        batchId: string;
        workspaceExternalId: string;
        name: string;
        total: number;
        handlers: ThumbnailBatchHandlers;
    }): void {
        this.startTracking(
            { batchId: args.batchId, workspaceExternalId: args.workspaceExternalId, name: args.name, total: args.total },
            {
                persist: false,
                includeOutstandingFileIds: true,
                handlers: args.handlers,
            });
    }

    dismiss(batchId: string): void {
        this.stopTracking(batchId);
        this.batches.update(list => list.filter(b => b.batchId !== batchId));
        this.persist();
    }

    // Cancels not-yet-started jobs of a batch through the handler the owner registered. Files
    // already in flight finish (and still show their thumbnail); the server notifies, so the SSE
    // push updates counts and drives the batch to done as usual — no local teardown needed here.
    async cancel(batchId: string): Promise<void> {
        const handlers = this._handlersByBatch.get(batchId);
        if (!handlers)
            return;

        await handlers.cancel(batchId);
    }

    private startTracking(
        persisted: PersistedBatch,
        options: {
            persist: boolean;
            includeOutstandingFileIds: boolean;
            initialProcessingIds?: readonly string[];
            handlers: ThumbnailBatchHandlers;
        }): void {
        if (this._unsubscribes.has(persisted.batchId))
            return;

        this._handlersByBatch.set(persisted.batchId, options.handlers);

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

        // Seed the spinner set from the ids the caller triggered (fresh start), so we don't depend
        // on the server echoing them back.
        if (options.initialProcessingIds && options.initialProcessingIds.length > 0) {
            this._liveByBatch.update(map => {
                const next = new Map(map);
                next.set(persisted.batchId, {
                    processingFileIds: new Set(options.initialProcessingIds),
                    readyMiniEtagByFileId: new Map(),
                });
                return next;
            });
        }

        if (options.persist)
            this.persist();

        const unsubscribe = options.handlers.subscribe(
            persisted.batchId,
            status => this.onStatus(persisted.batchId, status),
            options.includeOutstandingFileIds);

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

        this._liveByBatch.update(map => {
            const next = new Map(map);
            const previous = next.get(batchId);

            // processingFileExternalIds is sent ONLY on the first event when the subscriber asked
            // for it (reload case); later events omit it. So seed the spinner set from it, then
            // remove each file that completes (readyThumbnails is a per-event delta). Both
            // accumulate across events.
            const processing = new Set(previous?.processingFileIds ?? []);
            for (const fileId of status.processingFileExternalIds ?? [])
                processing.add(fileId);

            const readyMini = new Map(previous?.readyMiniEtagByFileId ?? []);

            for (const ready of status.readyThumbnails ?? [])
            {
                processing.delete(ready.fileExternalId);

                const mini = ready.variants.find(variant => variant.variant === 'Mini');
                if (mini)
                    readyMini.set(ready.fileExternalId, mini.etag);
            }

            next.set(batchId, {
                processingFileIds: processing,
                readyMiniEtagByFileId: readyMini
            });

            return next;
        });

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

        if (this._dismissTimers.has(batchId))
            return;

        const timer = setTimeout(
            () => {
                this._dismissTimers.delete(batchId);
                this.batches.update(list => list.filter(b => b.batchId !== batchId));
                this.removeLiveState(batchId);
                this._handlersByBatch.delete(batchId);
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

        this.removeLiveState(batchId);
        this._handlersByBatch.delete(batchId);
    }

    private removeLiveState(batchId: string): void {
        this._liveByBatch.update(map => {
            if (!map.has(batchId))
                return map;

            const next = new Map(map);
            next.delete(batchId);
            return next;
        });
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
