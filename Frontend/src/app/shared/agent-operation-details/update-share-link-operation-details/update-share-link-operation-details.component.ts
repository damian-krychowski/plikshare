import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { UpdateShareLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-update-share-link-operation-details',
    standalone: true,
    imports: [DatePipe],
    templateUrl: './update-share-link-operation-details.component.html',
    styleUrl: './update-share-link-operation-details.component.scss'
})
export class UpdateShareLinkOperationDetailsComponent {
    details = input.required<UpdateShareLinkOperationDetails>();
}
