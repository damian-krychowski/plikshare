import { Component, input } from '@angular/core';
import { GetBoxOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-get-box-operation-details',
    standalone: true,
    imports: [],
    template: `
        <div class="explanation">
            Reads the details of a single box.
        </div>
    `
})
export class GetBoxOperationDetailsComponent {
    details = input.required<GetBoxOperationDetails>();
}
