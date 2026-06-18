import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { CreateShareLinkOperationDetails, ShareLinkItemDetail } from '../../../services/agents.api';

@Component({
    selector: 'app-create-share-link-operation-details',
    standalone: true,
    imports: [DatePipe],
    templateUrl: './create-share-link-operation-details.component.html',
    styleUrl: './create-share-link-operation-details.component.scss'
})
export class CreateShareLinkOperationDetailsComponent {
    details = input.required<CreateShareLinkOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openFolder(folder: ShareLinkItemDetail) {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(['workspaces', workspaceExternalId, 'explorer', folder.externalId]);
    }

    openFile(file: ShareLinkItemDetail) {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        this._router.navigate(
            ['workspaces', workspaceExternalId, 'explorer'],
            { queryParams: { fileId: file.externalId } });
    }
}
