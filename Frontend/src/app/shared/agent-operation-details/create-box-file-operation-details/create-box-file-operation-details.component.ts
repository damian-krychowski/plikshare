import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { CreateBoxFileOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-create-box-file-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="op-details__section-title">
            Box
        </div>

        <div class="op-details__item"
            [class.op-details__item--clickable]="workspaceExternalId()"
            (click)="openBox()">
            <i class="icon icon-xl icon-nucleo-box op-details__icon"></i>
            <div class="op-details__text">
                <div class="op-details__name">{{ details().boxName ?? details().boxExternalId }}</div>
            </div>
        </div>

        <div class="op-details__section-title">
            File - create
        </div>

        <div class="op-details__item">
            <i class="icon icon-xl icon-nucleo-file op-details__icon"></i>
            <div class="op-details__text">
                <div class="op-details__name">{{ details().name }}</div>
                <div class="op-details__path">
                    @if(details().parentLocation) {
                        in {{ details().parentLocation }}
                    } @else {
                        at the box root
                    }
                    · {{ formatSize(details().sizeInBytes) }}
                </div>
            </div>
        </div>

        @if(details().contentPreview) {
            <div class="op-details__preview">
                <pre>{{ details().contentPreview }}</pre>
                @if(details().isPreviewTruncated) {
                    <div class="op-details__preview-more">… preview truncated</div>
                }
            </div>
        }
    `,
    styleUrl: '../_op-details.scss'
})
export class CreateBoxFileOperationDetailsComponent {
    details = input.required<CreateBoxFileOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openBox() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'boxes', this.details().boxExternalId]);
    }

    formatSize(sizeInBytes: number): string {
        if (sizeInBytes < 1024)
            return `${sizeInBytes} B`;

        if (sizeInBytes < 1024 * 1024)
            return `${(sizeInBytes / 1024).toFixed(1)} KB`;

        return `${(sizeInBytes / (1024 * 1024)).toFixed(1)} MB`;
    }
}
