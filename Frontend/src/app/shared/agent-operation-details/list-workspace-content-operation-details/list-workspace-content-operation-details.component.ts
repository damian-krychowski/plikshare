import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { ListWorkspaceContentOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-workspace-content-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './list-workspace-content-operation-details.component.html',
    styleUrl: '../_op-details.scss'
})
export class ListWorkspaceContentOperationDetailsComponent {
    details = input.required<ListWorkspaceContentOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openFolder() {
        const workspaceExternalId = this.workspaceExternalId();
        const folderExternalId = this.details().folderExternalId;

        if (!workspaceExternalId || !folderExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'explorer', folderExternalId]);
    }
}
