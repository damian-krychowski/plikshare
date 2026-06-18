import { Component, input } from '@angular/core';
import { ListWorkspacesOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-workspaces-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="explanation">
            Lists all workspaces the agent can access.
        </div>
    `
})
export class ListWorkspacesOperationDetailsComponent {
    details = input.required<ListWorkspacesOperationDetails>();
}
