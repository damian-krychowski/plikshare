import { Component, input } from '@angular/core';
import { ListWorkspaceContentOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-list-workspace-content-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './list-workspace-content-operation-details.component.html',
    styleUrl: './list-workspace-content-operation-details.component.scss'
})
export class ListWorkspaceContentOperationDetailsComponent {
    details = input.required<ListWorkspaceContentOperationDetails>();
}
