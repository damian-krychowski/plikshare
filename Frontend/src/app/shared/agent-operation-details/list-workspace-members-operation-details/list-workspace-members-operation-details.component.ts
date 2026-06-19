import { Component, input } from '@angular/core';
import { ListWorkspaceMembersOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-workspace-members-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="explanation">
            Lists all members of the workspace.
        </div>
    `
})
export class ListWorkspaceMembersOperationDetailsComponent {
    details = input.required<ListWorkspaceMembersOperationDetails>();
}
