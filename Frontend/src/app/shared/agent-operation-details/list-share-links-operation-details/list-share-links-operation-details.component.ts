import { Component, input } from '@angular/core';
import { ListShareLinksOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-share-links-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="explanation">
            Lists all public share links in the workspace.
        </div>
    `
})
export class ListShareLinksOperationDetailsComponent {
    details = input.required<ListShareLinksOperationDetails>();
}
