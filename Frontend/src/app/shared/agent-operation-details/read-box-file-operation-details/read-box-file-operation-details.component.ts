import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { ReadBoxFileOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-read-box-file-operation-details',
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
            File - read content
        </div>

        <div class="op-details__item"
            [class.op-details__item--clickable]="workspaceExternalId()"
            (click)="openFile()">
            <i class="icon icon-xl icon-nucleo-file op-details__icon"></i>
            <div class="op-details__text">
                <div class="op-details__name">{{ details().name ?? details().fileExternalId }}</div>
                @if(details().path) {
                    <div class="op-details__path">{{ details().path }}</div>
                }
                @if(details().offset > 0) {
                    <div class="op-details__path">from byte {{ details().offset }}</div>
                }
            </div>
        </div>
    `,
    styleUrl: '../_op-details.scss'
})
export class ReadBoxFileOperationDetailsComponent {
    details = input.required<ReadBoxFileOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openBox() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'boxes', this.details().boxExternalId]);
    }

    openFile() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(
            ['workspaces', workspaceExternalId, 'explorer'],
            { queryParams: { fileId: this.details().fileExternalId } });
    }
}
