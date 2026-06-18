import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { CreateFolderOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-create-folder-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './create-folder-operation-details.component.html',
    styleUrl: './create-folder-operation-details.component.scss'
})
export class CreateFolderOperationDetailsComponent {
    details = input.required<CreateFolderOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openParent() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        const parentFolderExternalId = this.details().parentFolderExternalId;

        const path = parentFolderExternalId
            ? ['workspaces', workspaceExternalId, 'explorer', parentFolderExternalId]
            : ['workspaces', workspaceExternalId, 'explorer'];

        this._router.navigate(path);
    }
}
