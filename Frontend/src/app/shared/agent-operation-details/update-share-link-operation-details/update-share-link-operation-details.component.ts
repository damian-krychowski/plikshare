import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { UpdateShareLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-update-share-link-operation-details',
    standalone: true,
    imports: [DatePipe],
    templateUrl: './update-share-link-operation-details.component.html',
    styleUrl: '../_op-details.scss'
})
export class UpdateShareLinkOperationDetailsComponent {
    details = input.required<UpdateShareLinkOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openShareLink() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(
            ['workspaces', workspaceExternalId, 'quick-shares', this.details().shareLinkExternalId]);
    }
}
