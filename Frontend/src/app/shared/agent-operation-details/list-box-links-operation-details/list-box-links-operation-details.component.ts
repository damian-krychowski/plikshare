import { Component, input } from '@angular/core';
import { ListBoxLinksOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-box-links-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="explanation">
            Lists all public links of the box.
        </div>
    `
})
export class ListBoxLinksOperationDetailsComponent {
    details = input.required<ListBoxLinksOperationDetails>();
}
