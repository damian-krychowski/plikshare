import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { BulkDeleteFileDetail, BulkDeleteFolderDetail, DeleteBoxItemsOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-delete-box-items-operation-details',
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

        @if(details().folders.length > 0) {
            <div class="op-details__section-title">
                Folders - deleted with everything inside
            </div>

            @for(folder of details().folders; track folder.externalId) {
                <div class="op-details__item"
                    [class.op-details__item--clickable]="workspaceExternalId()"
                    (click)="openFolder(folder)">
                    <i class="icon icon-xl icon-nucleo-folder op-details__icon"></i>
                    <div class="op-details__text">
                        <div class="op-details__name">{{ folder.name }}</div>
                        @if(folder.path) {
                            <div class="op-details__path">{{ folder.path }}</div>
                        }
                    </div>
                </div>
            }
        }

        @if(details().files.length > 0) {
            <div class="op-details__section-title">
                Files
            </div>

            @for(file of details().files; track file.externalId) {
                <div class="op-details__item"
                    [class.op-details__item--clickable]="workspaceExternalId()"
                    (click)="openFile(file)">
                    <i class="icon icon-xl icon-nucleo-file op-details__icon"></i>
                    <div class="op-details__text">
                        <div class="op-details__name">{{ file.name }}</div>
                        @if(file.path) {
                            <div class="op-details__path">{{ file.path }}</div>
                        }
                    </div>
                </div>
            }
        }

        @if(details().folders.length === 0 && details().files.length === 0) {
            <div class="explanation">
                These items no longer exist.
            </div>
        }
    `,
    styleUrl: '../_op-details.scss'
})
export class DeleteBoxItemsOperationDetailsComponent {
    details = input.required<DeleteBoxItemsOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openBox() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'boxes', this.details().boxExternalId]);
    }

    openFolder(folder: BulkDeleteFolderDetail) {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'explorer', folder.externalId]);
    }

    openFile(file: BulkDeleteFileDetail) {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        const path = file.folderExternalId
            ? ['workspaces', workspaceExternalId, 'explorer', file.folderExternalId]
            : ['workspaces', workspaceExternalId, 'explorer'];

        this._router.navigate(path, { queryParams: { fileId: file.externalId } });
    }
}
