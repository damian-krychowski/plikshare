import { Component, input } from '@angular/core';
import { CreateWorkspaceOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-create-workspace-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './create-workspace-operation-details.component.html',
    styleUrl: '../_op-details.scss'
})
export class CreateWorkspaceOperationDetailsComponent {
    details = input.required<CreateWorkspaceOperationDetails>();
}
