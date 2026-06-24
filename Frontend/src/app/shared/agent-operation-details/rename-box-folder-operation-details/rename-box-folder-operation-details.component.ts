import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { RenameBoxFolderOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-rename-box-folder-operation-details',
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

        @if(details().currentName) {
            <div class="op-details__section-title">
                Folder - rename
            </div>

            <div class="op-details__item"
                [class.op-details__item--clickable]="workspaceExternalId()"
                (click)="openFolder()">
                <i class="icon icon-xl icon-nucleo-folder op-details__icon"></i>
                <div class="op-details__text">
                    <div class="op-details__name">
                        <span class="op-details__old-name">{{ details().currentName }}</span>
                        <span class="op-details__arrow">&#8594;</span>
                        {{ details().newName }}
                    </div>
                    @if(details().path) {
                        <div class="op-details__path">{{ details().path }}</div>
                    }
                </div>
            </div>
        } @else {
            <div class="explanation">
                This folder no longer exists.
            </div>
        }
    `,
    styleUrl: '../_op-details.scss'
})
export class RenameBoxFolderOperationDetailsComponent {
    details = input.required<RenameBoxFolderOperationDetails>();
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

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'explorer', this.details().folderExternalId]);
    }
}
