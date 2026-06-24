import { Component, input } from '@angular/core';
import { CreateBoxLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-create-box-link-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        <div class="op-details__section-title">
            Box link - create{{ details().boxName ? ' for ' + details().boxName : '' }}
        </div>

        <div class="op-details__item">
            <i class="icon icon-xl icon-nucleo-link op-details__icon"></i>
            <div class="op-details__text">
                <div class="op-details__name">{{ details().name }}</div>
            </div>
        </div>
    `
})
export class CreateBoxLinkOperationDetailsComponent {
    details = input.required<CreateBoxLinkOperationDetails>();
}
