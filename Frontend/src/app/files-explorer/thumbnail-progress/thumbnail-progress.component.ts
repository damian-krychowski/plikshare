import { Component, computed, DestroyRef, effect, ElementRef, inject, viewChild } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ThumbnailBatchProgressService } from '../../services/thumbnail-batch-progress.service';
import { ConfirmOperationDirective } from '../../shared/operation-confirm/confirm-operation.directive';

// Compact, self-contained thumbnail-generation indicator for the toolbar: reads the app-wide batch
// tracker and renders an icon + mini bar + done/total + elapsed wall-clock. Renders nothing when no
// batch is tracked.
@Component({
    selector: 'app-thumbnail-progress',
    standalone: true,
    imports: [MatTooltipModule, ConfirmOperationDirective],
    templateUrl: './thumbnail-progress.component.html',
    styleUrl: './thumbnail-progress.component.scss'
})
export class ThumbnailProgressComponent {
    private _batches = inject(ThumbnailBatchProgressService);
    private _destroyRef = inject(DestroyRef);

    private _elapsedEl = viewChild<ElementRef<HTMLElement>>('elapsed');
    private _tickHandle: ReturnType<typeof setInterval> | null = null;

    progress = computed(() => {
        const batches = this._batches.batches();

        if (batches.length === 0)
            return null;

        let done = 0;
        let total = 0;
        let failed = 0;

        for (const batch of batches) {
            done += batch.completed + batch.failed;
            total += batch.total;
            failed += batch.failed;
        }

        return {
            done,
            total,
            failed,
            percent: total > 0 ? Math.round((done / total) * 100) : 0
        };
    });

    // Batches that still have not-yet-started work worth cancelling.
    activeBatchIds = computed(() =>
        this._batches.batches()
            .filter(batch => !batch.isDone)
            .map(batch => batch.batchId));

    constructor() {
        effect(() => {
            const el = this._elapsedEl()?.nativeElement;
            const batches = this._batches.batches();

            if (!el || batches.length === 0) {
                this.stopTicker();
                return;
            }

            this.writeElapsed(el);

            if (batches.some(batch => batch.finishedAt === null))
                this.startTicker();
            else
                this.stopTicker();
        });

        this._destroyRef.onDestroy(() => this.stopTicker());
    }

    cancel(): void {
        for (const batchId of this.activeBatchIds())
            this._batches.cancel(batchId);
    }

    private startTicker(): void {
        if (this._tickHandle !== null)
            return;

        this._tickHandle = setInterval(
            () => {
                const el = this._elapsedEl()?.nativeElement;

                if (el)
                    this.writeElapsed(el);
            },
            100);
    }

    private stopTicker(): void {
        if (this._tickHandle !== null) {
            clearInterval(this._tickHandle);
            this._tickHandle = null;
        }
    }

    private writeElapsed(el: HTMLElement): void {
        const batches = this._batches.batches();

        if (batches.length === 0)
            return;

        const oldest = batches.reduce(
            (acc, batch) => batch.startedAt < acc.startedAt ? batch : acc);

        const end = oldest.finishedAt ?? Date.now();
        el.textContent = formatElapsed(end - oldest.startedAt);
    }
}

// Sub-minute readouts get one decimal of seconds (perceptible delta on small batches); longer
// runs switch to m:ss for legibility.
function formatElapsed(ms: number): string {
    if (ms < 0)
        ms = 0;

    const totalSeconds = ms / 1000;

    if (totalSeconds < 60)
        return `${totalSeconds.toFixed(1)}s`;

    const minutes = Math.floor(totalSeconds / 60);
    const seconds = Math.floor(totalSeconds % 60);
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}
