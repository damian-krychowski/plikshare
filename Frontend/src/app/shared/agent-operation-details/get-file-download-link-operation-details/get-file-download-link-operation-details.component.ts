import { Component, input } from '@angular/core';
import { GetFileDownloadLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-get-file-download-link-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './get-file-download-link-operation-details.component.html',
    styleUrl: './get-file-download-link-operation-details.component.scss'
})
export class GetFileDownloadLinkOperationDetailsComponent {
    details = input.required<GetFileDownloadLinkOperationDetails>();
}
