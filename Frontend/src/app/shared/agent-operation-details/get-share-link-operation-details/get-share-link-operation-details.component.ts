import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { GetShareLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-get-share-link-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './get-share-link-operation-details.component.html',
    styleUrl: '../_op-details.scss'
})
export class GetShareLinkOperationDetailsComponent {
    details = input.required<GetShareLinkOperationDetails>();
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
