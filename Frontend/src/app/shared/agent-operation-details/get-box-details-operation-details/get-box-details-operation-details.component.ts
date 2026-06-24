import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { GetBoxDetailsOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-get-box-details-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="op-details__section-title">
            Box - read details
        </div>

        <div class="op-details__item"
            [class.op-details__item--clickable]="workspaceExternalId()"
            (click)="openBox()">
            <i class="icon icon-xl icon-nucleo-box op-details__icon"></i>
            <div class="op-details__text">
                <div class="op-details__name">{{ details().boxName ?? details().boxExternalId }}</div>
            </div>
        </div>

        <div class="op-details__notice">
            <i class="icon icon-md icon-nucleo-info op-details__notice-icon"></i>
            <span>Reads the box's configuration - nothing is downloaded or changed.</span>
        </div>
    `,
    styleUrl: '../_op-details.scss'
})
export class GetBoxDetailsOperationDetailsComponent {
    details = input.required<GetBoxDetailsOperationDetails>();
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
