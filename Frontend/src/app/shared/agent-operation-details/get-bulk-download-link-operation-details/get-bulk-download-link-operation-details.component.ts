import { Component, input } from '@angular/core';
import { GetBulkDownloadLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-get-bulk-download-link-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './get-bulk-download-link-operation-details.component.html',
    styleUrl: './get-bulk-download-link-operation-details.component.scss'
})
export class GetBulkDownloadLinkOperationDetailsComponent {
    details = input.required<GetBulkDownloadLinkOperationDetails>();
}
