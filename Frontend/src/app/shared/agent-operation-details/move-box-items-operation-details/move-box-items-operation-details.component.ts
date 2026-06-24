import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { MoveItemDetail, MoveBoxItemsOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-move-box-items-operation-details',
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
            Destination
        </div>

        <div class="op-details__item"
            [class.op-details__item--clickable]="workspaceExternalId()"
            (click)="openDestination()">
            <i class="icon icon-xl icon-nucleo-folder op-details__icon"></i>
            <div class="op-details__text">
                @if(details().destinationName) {
                    <div class="op-details__name">{{ details().destinationName }}</div>
                    @if(details().destinationPath) {
                        <div class="op-details__path">{{ details().destinationPath }}</div>
                    }
                } @else {
                    <div class="op-details__name">Box root</div>
                }
            </div>
        </div>

        @if(details().folders.length > 0) {
            <div class="op-details__section-title">
                Folders - moved with everything inside
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
    `,
    styleUrl: '../_op-details.scss'
})
export class MoveBoxItemsOperationDetailsComponent {
    details = input.required<MoveBoxItemsOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openBox() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'boxes', this.details().boxExternalId]);
    }

    openDestination() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        const destinationFolderExternalId = this.details().destinationFolderExternalId;

        const path = destinationFolderExternalId
            ? ['workspaces', workspaceExternalId, 'explorer', destinationFolderExternalId]
            : ['workspaces', workspaceExternalId, 'explorer'];

        this._router.navigate(path);
    }

    openFolder(folder: MoveItemDetail) {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'explorer', folder.externalId]);
    }

    openFile(file: MoveItemDetail) {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(
            ['workspaces', workspaceExternalId, 'explorer'],
            { queryParams: { fileId: file.externalId } });
    }
}
