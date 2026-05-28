import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { ThumbnailBatch, ThumbnailBatchProgressService } from '../../services/thumbnail-batch-progress.service';

@Component({
    selector: 'app-thumbnail-batches-bar',
    standalone: true,
    imports: [
        MatProgressBarModule,
        MatTooltipModule,
        ActionButtonComponent,
    ],
    templateUrl: './thumbnail-batches-bar.component.html',
    styleUrl: './thumbnail-batches-bar.component.scss',
})
export class ThumbnailBatchesBarComponent {
    private _service = inject(ThumbnailBatchProgressService);

    batches = this._service.batches;

    progress(batch: ThumbnailBatch): number {
        if (batch.total <= 0)
            return 0;

        return Math.round(((batch.completed + batch.failed) / batch.total) * 100);
    }

    dismiss(batchId: string): void {
        this._service.dismiss(batchId);
    }
}
