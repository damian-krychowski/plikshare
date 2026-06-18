import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { BulkDownloadItemDetail, GetBulkDownloadLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-get-bulk-download-link-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './get-bulk-download-link-operation-details.component.html',
    styleUrl: './get-bulk-download-link-operation-details.component.scss'
})
export class GetBulkDownloadLinkOperationDetailsComponent {
    details = input.required<GetBulkDownloadLinkOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openFolder(folder: BulkDownloadItemDetail) {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'explorer', folder.externalId]);
    }

    openFile(file: BulkDownloadItemDetail) {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        const path = file.folderExternalId
            ? ['workspaces', workspaceExternalId, 'explorer', file.folderExternalId]
            : ['workspaces', workspaceExternalId, 'explorer'];

        this._router.navigate(path, { queryParams: { fileId: file.externalId } });
    }
}
