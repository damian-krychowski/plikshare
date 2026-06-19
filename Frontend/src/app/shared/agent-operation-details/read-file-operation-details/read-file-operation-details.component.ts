import { Component, input } from '@angular/core';
import { Router } from '@angular/router';
import { ReadFileOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-read-file-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './read-file-operation-details.component.html',
    styleUrl: '../_op-details.scss'
})
export class ReadFileOperationDetailsComponent {
    details = input.required<ReadFileOperationDetails>();
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
