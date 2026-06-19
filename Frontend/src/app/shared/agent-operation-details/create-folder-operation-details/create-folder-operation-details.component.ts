import { Component, input } from '@angular/core';
import { CreateFolderOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-create-folder-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './create-folder-operation-details.component.html',
    styleUrl: '../_op-details.scss'
})
export class CreateFolderOperationDetailsComponent {
    details = input.required<CreateFolderOperationDetails>();
}
