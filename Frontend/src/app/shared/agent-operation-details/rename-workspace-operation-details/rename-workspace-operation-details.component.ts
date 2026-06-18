import { Component, input } from '@angular/core';
import { RenameWorkspaceOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-rename-workspace-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './rename-workspace-operation-details.component.html',
    styleUrl: './rename-workspace-operation-details.component.scss'
})
export class RenameWorkspaceOperationDetailsComponent {
    details = input.required<RenameWorkspaceOperationDetails>();
}
