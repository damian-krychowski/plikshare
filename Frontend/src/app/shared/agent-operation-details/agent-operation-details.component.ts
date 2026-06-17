import { Component, input } from '@angular/core';
import { AgentOperationDetails } from '../../services/agents.api';
import { BulkDeleteOperationDetailsComponent } from './bulk-delete-operation-details/bulk-delete-operation-details.component';

@Component({
    selector: 'app-agent-operation-details',
    standalone: true,
    imports: [
        BulkDeleteOperationDetailsComponent
    ],
    template: `
        @switch(details().$type) {
            @case('bulk_delete') {
                <app-bulk-delete-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-bulk-delete-operation-details>
            }
            @default {
                <div class="explanation">No details available for this operation.</div>
            }
        }
    `
})
export class AgentOperationDetailsComponent {
    details = input.required<AgentOperationDetails>();
    workspaceExternalId = input<string | null>(null);
}
