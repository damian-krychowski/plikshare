import { Component, input } from '@angular/core';
import { RenameWorkspaceOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-rename-workspace-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './rename-workspace-operation-details.component.html',
    styleUrl: '../_op-details.scss'
})
export class RenameWorkspaceOperationDetailsComponent {
    details = input.required<RenameWorkspaceOperationDetails>();
}
