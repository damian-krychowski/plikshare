import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { MoveItemDetail, MoveItemsOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-move-items-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './move-items-operation-details.component.html',
    styleUrl: './move-items-operation-details.component.scss'
})
export class MoveItemsOperationDetailsComponent {
    details = input.required<MoveItemsOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
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
}
