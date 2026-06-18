import { Component, input } from '@angular/core';
import { DeleteShareLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-delete-share-link-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './delete-share-link-operation-details.component.html',
    styleUrl: './delete-share-link-operation-details.component.scss'
})
export class DeleteShareLinkOperationDetailsComponent {
    details = input.required<DeleteShareLinkOperationDetails>();
}
