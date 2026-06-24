import { Component, input } from '@angular/core';
import { CreateBoxOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-create-box-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        <div class="op-details__section-title">
            Box - create{{ details().workspaceName ? ' in ' + details().workspaceName : '' }}
        </div>

        <div class="op-details__item">
            <i class="icon icon-xl icon-nucleo-box op-details__icon"></i>
            <div class="op-details__text">
                <div class="op-details__name">{{ details().name }}</div>
            </div>
        </div>
    `
})
export class CreateBoxOperationDetailsComponent {
    details = input.required<CreateBoxOperationDetails>();
}
