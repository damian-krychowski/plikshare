import { Component, input } from '@angular/core';
import { ListBoxesOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-boxes-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="explanation">
            Lists all boxes of the workspace.
        </div>
    `
})
export class ListBoxesOperationDetailsComponent {
    details = input.required<ListBoxesOperationDetails>();
}
