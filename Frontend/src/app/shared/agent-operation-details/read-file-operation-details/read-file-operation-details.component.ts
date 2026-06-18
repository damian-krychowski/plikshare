import { Component, input } from '@angular/core';
import { ReadFileOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-read-file-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './read-file-operation-details.component.html',
    styleUrl: './read-file-operation-details.component.scss'
})
export class ReadFileOperationDetailsComponent {
    details = input.required<ReadFileOperationDetails>();
}
