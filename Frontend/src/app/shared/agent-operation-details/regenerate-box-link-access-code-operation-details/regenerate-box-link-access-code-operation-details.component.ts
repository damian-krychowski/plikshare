import { Component, input } from '@angular/core';
import { RegenerateBoxLinkAccessCodeOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-regenerate-box-link-access-code-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        <div class="op-details__section-title">
            Box link — regenerate access code
        </div>

        @if(details().boxLinkName) {
            <div class="op-details__item">
                <i class="icon icon-xl icon-nucleo-link op-details__icon"></i>
                <div class="op-details__text">
                    <div class="op-details__name">{{ details().boxLinkName }}</div>
                </div>
            </div>
        }

        <div class="op-details__notice">
            <i class="icon icon-lg icon-nucleo-info op-details__notice-icon"></i>
            <span>The link's current URL will stop working immediately.</span>
        </div>
    `
})
export class RegenerateBoxLinkAccessCodeOperationDetailsComponent {
    details = input.required<RegenerateBoxLinkAccessCodeOperationDetails>();
}
