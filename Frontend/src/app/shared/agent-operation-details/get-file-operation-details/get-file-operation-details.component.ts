import { Component, input } from '@angular/core';
import { GetFileOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-get-file-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './get-file-operation-details.component.html',
    styleUrl: './get-file-operation-details.component.scss'
})
export class GetFileOperationDetailsComponent {
    details = input.required<GetFileOperationDetails>();
}
