import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { CreateFileOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-create-file-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './create-file-operation-details.component.html',
    styleUrl: './create-file-operation-details.component.scss'
})
export class CreateFileOperationDetailsComponent {
    details = input.required<CreateFileOperationDetails>();
    workspaceExternalId = input<string | null>(null);

    constructor(private _router: Router) {
    }

    openParent() {
        const workspaceExternalId = this.workspaceExternalId();

        if (!workspaceExternalId)
            return;

        const folderExternalId = this.details().folderExternalId;

        const path = folderExternalId
            ? ['workspaces', workspaceExternalId, 'explorer', folderExternalId]
            : ['workspaces', workspaceExternalId, 'explorer'];

        this._router.navigate(path);
    }

    formatSize(bytes: number): string {
        if (bytes < 1024)
            return `${bytes} B`;

        if (bytes < 1024 * 1024)
            return `${(bytes / 1024).toFixed(1)} KB`;

        return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    }
}
