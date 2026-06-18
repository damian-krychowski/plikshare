import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { GetFileOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-get-file-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './get-file-operation-details.component.html',
    styleUrl: './get-file-operation-details.component.scss'
})
export class GetFileOperationDetailsComponent {
    details = input.required<GetFileOperationDetails>();
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
