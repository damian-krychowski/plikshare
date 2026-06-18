import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { RenameFileOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-rename-file-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './rename-file-operation-details.component.html',
    styleUrl: './rename-file-operation-details.component.scss'
})
export class RenameFileOperationDetailsComponent {
    details = input.required<RenameFileOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openFile() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        const folderExternalId = this.details().folderExternalId;

        const path = folderExternalId
            ? ['workspaces', workspaceExternalId, 'explorer', folderExternalId]
            : ['workspaces', workspaceExternalId, 'explorer'];

        this._router.navigate(path, { queryParams: { fileId: this.details().fileExternalId } });
    }
}
