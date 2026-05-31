import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ThumbnailBatchProgressService } from '../../services/thumbnail-batch-progress.service';

// Compact, self-contained thumbnail-generation indicator for the toolbar: reads the app-wide batch
// tracker and renders an icon + mini bar + done/total. Renders nothing when no batch is tracked.
@Component({
    selector: 'app-thumbnail-progress',
    standalone: true,
    imports: [MatTooltipModule],
    templateUrl: './thumbnail-progress.component.html',
    styleUrl: './thumbnail-progress.component.scss'
})
export class ThumbnailProgressComponent implements OnInit {
    private _batches = inject(ThumbnailBatchProgressService);
    private _destroyRef = inject(DestroyRef);

    // TEMP: live wall-clock ticker for the ffmpeg-vs-Skia comparison. Wakes the elapsed computed
    // every 100ms so the readout counts up smoothly. Drop along with `elapsedLabel`.
    private _now = signal(Date.now());

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

    // TEMP: elapsed wall-clock for the oldest tracked batch. While the batch is running we tick
    // against `_now`; once it finishes we freeze on `finishedAt`. Drop after the A/B.
    elapsedLabel = computed(() => {
        const batches = this._batches.batches();
        if (batches.length === 0)
            return null;

        const oldest = batches.reduce(
            (acc, batch) => batch.startedAt < acc.startedAt ? batch : acc);

        const end = oldest.finishedAt ?? this._now();
        return formatElapsed(end - oldest.startedAt);
    });

    // Batches that still have not-yet-started work worth cancelling.
    activeBatchIds = computed(() =>
        this._batches.batches()
            .filter(batch => !batch.isDone)
            .map(batch => batch.batchId));

    ngOnInit(): void {
        const handle = setInterval(
            () => this._now.set(Date.now()),
            100);

        this._destroyRef.onDestroy(() => clearInterval(handle));
    }

    cancel(): void {
        for (const batchId of this.activeBatchIds())
            this._batches.cancel(batchId);
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
