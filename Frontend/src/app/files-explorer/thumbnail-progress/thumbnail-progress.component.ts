import { Component, computed, inject } from '@angular/core';
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
export class ThumbnailProgressComponent {
    private _batches = inject(ThumbnailBatchProgressService);

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

    cancel(): void {
        for (const batchId of this.activeBatchIds())
            this._batches.cancel(batchId);
    }
}
