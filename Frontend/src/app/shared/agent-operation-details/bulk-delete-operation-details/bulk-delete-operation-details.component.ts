import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { BulkDeleteFileDetail, BulkDeleteFolderDetail, BulkDeleteOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-bulk-delete-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './bulk-delete-operation-details.component.html',
    styleUrl: '../_op-details.scss'
})
export class BulkDeleteOperationDetailsComponent {
    details = input.required<BulkDeleteOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openFolder(folder: BulkDeleteFolderDetail) {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'explorer', folder.externalId]);
    }

    openFile(file: BulkDeleteFileDetail) {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        const path = file.folderExternalId
            ? ['workspaces', workspaceExternalId, 'explorer', file.folderExternalId]
            : ['workspaces', workspaceExternalId, 'explorer'];

        this._router.navigate(path, { queryParams: { fileId: file.externalId } });
    }
}
