import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { CreateBoxFolderOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-create-box-folder-operation-details',
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
            Folder - create
        </div>

        <div class="op-details__item">
            <i class="icon icon-xl icon-nucleo-folder op-details__icon"></i>
            <div class="op-details__text">
                <div class="op-details__name">{{ details().name }}</div>
                @if(details().parentLocation) {
                    <div class="op-details__path">in {{ details().parentLocation }}</div>
                } @else {
                    <div class="op-details__path">at the box root</div>
                }
            </div>
        </div>
    `,
    styleUrl: '../_op-details.scss'
})
export class CreateBoxFolderOperationDetailsComponent {
    details = input.required<CreateBoxFolderOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openBox() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'boxes', this.details().boxExternalId]);
    }
}
