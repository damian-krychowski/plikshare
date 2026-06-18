import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { RenameFolderOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-rename-folder-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './rename-folder-operation-details.component.html',
    styleUrl: './rename-folder-operation-details.component.scss'
})
export class RenameFolderOperationDetailsComponent {
    details = input.required<RenameFolderOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openFolder() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'explorer', this.details().folderExternalId]);
    }
}
