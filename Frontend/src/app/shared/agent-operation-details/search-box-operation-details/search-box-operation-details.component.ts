import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { SearchBoxOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-search-box-operation-details',
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
            Search
        </div>

        <div class="op-details__meta">
            <div class="op-details__meta-row">
                <span class="op-details__meta-label">Phrase</span>
                <span class="op-details__meta-value">{{ details().phrase }}</span>
            </div>
        </div>

        @if(details().folderExternalId) {
            <div class="op-details__section-title">Scoped to folder</div>
            <div class="op-details__item"
                [class.op-details__item--clickable]="workspaceExternalId()"
                (click)="openFolder()">
                <i class="icon icon-xl icon-nucleo-folder op-details__icon"></i>
                <div class="op-details__text">
                    <div class="op-details__name">{{ details().folderName ?? details().folderExternalId }}</div>
                </div>
            </div>
        }

        <div class="op-details__notice">
            <i class="icon icon-md icon-nucleo-info op-details__notice-icon"></i>
            <span>Searches file names and content the agent can access in this box - nothing is downloaded or changed.</span>
        </div>
    `,
    styleUrl: '../_op-details.scss'
})
export class SearchBoxOperationDetailsComponent {
    details = input.required<SearchBoxOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openBox() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'boxes', this.details().boxExternalId]);
    }

    openFolder() {
        const workspaceExternalId = this.workspaceExternalId();
        const folderExternalId = this.details().folderExternalId;

        if (!workspaceExternalId || !folderExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'explorer', folderExternalId]);
    }
}
