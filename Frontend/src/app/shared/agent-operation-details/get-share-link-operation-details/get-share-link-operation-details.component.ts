import { Component, input } from '@angular/core';
import { GetShareLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-get-share-link-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './get-share-link-operation-details.component.html',
    styleUrl: './get-share-link-operation-details.component.scss'
})
export class GetShareLinkOperationDetailsComponent {
    details = input.required<GetShareLinkOperationDetails>();
}
