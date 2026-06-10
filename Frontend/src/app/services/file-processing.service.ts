import { computed, Injectable, signal } from '@angular/core';
import { FileProcessingEvent } from './folders-and-files.api';

export const THUMBNAIL_GENERATION_JOB_TYPE = 'generate-image-thumbnails';

// Transport handler the owning component injects, so the service stays transport-agnostic —
// mirrors the ThumbnailBatchHandlers pattern.
export type FileProcessingHandlers = {
    subscribe: (onEvent: (event: FileProcessingEvent) => void) => () => void;
};

/**
 * Live per-file processing state for one explorer, fed entirely by the stateful workspace SSE
 * stream: the first event carries the full set of files currently being processed, every later
 * event only the diff (`processing` / `processingFinished`). The service folds those into:
 *  - `processingFileIds` — files with queue work in flight (drives the per-row spinner),
 *  - `refreshedMiniEtags` — cache-buster per file whose thumbnail generation just finished, so the
 *    list re-fetches the mini thumbnail (the real ETag handles caching server-side).
 *
 * Provided at the explorer component level (not root): each explorer owns its connection and the
 * state dies with the view. An EventSource reconnect re-delivers the full set as the first event —
 * the state is reset on it implicitly, because the server reports every active file again.
 */
@Injectable()
export class FileProcessingService {
    private _activeJobTypesByFileId = signal<ReadonlyMap<string, ReadonlySet<string>>>(new Map());
    private _refreshedMiniEtags = signal<ReadonlyMap<string, string>>(new Map());

    readonly processingFileIds = computed<ReadonlySet<string>>(
        () => new Set(this._activeJobTypesByFileId().keys()));

    readonly refreshedMiniEtags = computed<ReadonlyMap<string, string>>(
        () => this._refreshedMiniEtags());

    private _unsubscribe: (() => void) | null = null;

    connect(handlers: FileProcessingHandlers): void {
        this.disconnect();

        this._unsubscribe = handlers.subscribe(
            event => this.onEvent(event));
    }

    disconnect(): void {
        if (this._unsubscribe) {
            this._unsubscribe();
            this._unsubscribe = null;
        }

        this._activeJobTypesByFileId.set(new Map());
        this._refreshedMiniEtags.set(new Map());
    }

    private onEvent(event: FileProcessingEvent): void {
        const next = new Map<string, Set<string>>();

        for (const [fileId, jobTypes] of this._activeJobTypesByFileId())
            next.set(fileId, new Set(jobTypes));

        // Finished first, (re)started second — a file present in both (a job finished while
        // another of the same type keeps running) nets out to "still processing".
        for (const [jobType, fileIds] of Object.entries(event.processingFinished ?? {})) {
            for (const fileId of fileIds) {
                const jobTypes = next.get(fileId);

                if (!jobTypes)
                    continue;

                jobTypes.delete(jobType);

                if (jobTypes.size === 0)
                    next.delete(fileId);
            }
        }

        for (const [jobType, fileIds] of Object.entries(event.processing ?? {})) {
            for (const fileId of fileIds) {
                const jobTypes = next.get(fileId);

                if (jobTypes)
                    jobTypes.add(jobType);
                else
                    next.set(fileId, new Set([jobType]));
            }
        }

        this._activeJobTypesByFileId.set(next);

        const finishedThumbnails = (event.processingFinished ?? {})[THUMBNAIL_GENERATION_JOB_TYPE];

        if (finishedThumbnails && finishedThumbnails.length > 0) {
            const refreshed = new Map(this._refreshedMiniEtags());

            for (const fileId of finishedThumbnails)
                refreshed.set(fileId, `${Date.now()}`);

            this._refreshedMiniEtags.set(refreshed);
        }
    }
}
