import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { DeleteShareLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-delete-share-link-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './delete-share-link-operation-details.component.html',
    styleUrl: './delete-share-link-operation-details.component.scss'
})
export class DeleteShareLinkOperationDetailsComponent {
    details = input.required<DeleteShareLinkOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openShareLink() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(
            ['workspaces', workspaceExternalId, 'quick-shares', this.details().externalId]);
    }
}
