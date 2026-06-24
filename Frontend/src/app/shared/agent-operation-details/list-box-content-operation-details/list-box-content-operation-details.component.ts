import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { ListBoxContentOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-box-content-operation-details',
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
            Browse folder
        </div>

        <div class="op-details__item"
            [class.op-details__item--clickable]="workspaceExternalId() && details().folderExternalId"
            (click)="openFolder()">
            <i class="icon icon-xl icon-nucleo-folder op-details__icon"></i>
            <div class="op-details__text">
                <div class="op-details__name">
                    {{ details().folderExternalId ? (details().folderName ?? details().folderExternalId) : 'Box root' }}
                </div>
            </div>
        </div>

        <div class="op-details__notice">
            <i class="icon icon-md icon-nucleo-info op-details__notice-icon"></i>
            <span>Reads the list of folders and files in this location - nothing is downloaded or changed.</span>
        </div>
    `,
    styleUrl: '../_op-details.scss'
})
export class ListBoxContentOperationDetailsComponent {
    details = input.required<ListBoxContentOperationDetails>();
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
