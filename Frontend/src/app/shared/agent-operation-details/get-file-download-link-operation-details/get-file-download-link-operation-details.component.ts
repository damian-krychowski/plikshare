import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { GetFileDownloadLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-get-file-download-link-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './get-file-download-link-operation-details.component.html',
    styleUrl: './get-file-download-link-operation-details.component.scss'
})
export class GetFileDownloadLinkOperationDetailsComponent {
    details = input.required<GetFileDownloadLinkOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openFile() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(
            ['workspaces', workspaceExternalId, 'explorer'],
            { queryParams: { fileId: this.details().fileExternalId } });
    }
}
