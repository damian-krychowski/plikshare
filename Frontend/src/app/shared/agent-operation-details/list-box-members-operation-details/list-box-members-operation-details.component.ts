import { Component, input } from '@angular/core';
import { ListBoxMembersOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-box-members-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="explanation">
            Lists all members of the box.
        </div>
    `
})
export class ListBoxMembersOperationDetailsComponent {
    details = input.required<ListBoxMembersOperationDetails>();
}
