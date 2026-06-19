import { Component, input } from '@angular/core';
import { CreateFileOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-create-file-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './create-file-operation-details.component.html',
    styleUrl: '../_op-details.scss'
})
export class CreateFileOperationDetailsComponent {
    details = input.required<CreateFileOperationDetails>();

    formatSize(bytes: number): string {
        if (bytes < 1024)
            return `${bytes} B`;

        if (bytes < 1024 * 1024)
            return `${(bytes / 1024).toFixed(1)} KB`;

        return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    }
}
