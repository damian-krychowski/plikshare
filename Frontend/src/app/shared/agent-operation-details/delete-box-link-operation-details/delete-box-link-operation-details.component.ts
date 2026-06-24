import { Component, input } from '@angular/core';
import { DeleteBoxLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-delete-box-link-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        @if(details().boxLinkName) {
            <div class="op-details__section-title">
                Box link - delete
            </div>

            <div class="op-details__item">
                <i class="icon icon-xl icon-nucleo-link op-details__icon"></i>
                <div class="op-details__text">
                    <div class="op-details__name">{{ details().boxLinkName }}</div>
                </div>
            </div>
        } @else {
            <div class="explanation">
                This box link no longer exists.
            </div>
        }
    `
})
export class DeleteBoxLinkOperationDetailsComponent {
    details = input.required<DeleteBoxLinkOperationDetails>();
}
