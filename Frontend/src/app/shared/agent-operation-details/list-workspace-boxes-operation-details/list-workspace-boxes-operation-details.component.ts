import { Component, input } from '@angular/core';
import { ListWorkspaceBoxesOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-workspace-boxes-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="explanation">
            Lists all boxes of the workspace.
        </div>
    `
})
export class ListWorkspaceBoxesOperationDetailsComponent {
    details = input.required<ListWorkspaceBoxesOperationDetails>();
}
