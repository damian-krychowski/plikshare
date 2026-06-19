import { Component, input } from '@angular/core';
import { SearchOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-search-operation-details',
    standalone: true,
    imports: [],
    templateUrl: './search-operation-details.component.html',
    styleUrl: '../_op-details.scss'
})
export class SearchOperationDetailsComponent {
    details = input.required<SearchOperationDetails>();
}
