import { Component, input } from '@angular/core';
import { ListStoragesOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-storages-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="explanation">
            Lists all storages the agent can use to create workspaces.
        </div>
    `
})
export class ListStoragesOperationDetailsComponent {
    details = input.required<ListStoragesOperationDetails>();
}
