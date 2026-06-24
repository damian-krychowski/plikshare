import { Component, input } from '@angular/core';
import { ListBoxesOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-boxes-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="explanation">
            Lists the boxes shared directly with the agent.
        </div>
    `
})
export class ListBoxesOperationDetailsComponent {
    details = input.required<ListBoxesOperationDetails>();
}
