import { Component, input } from '@angular/core';
import { DeleteBoxOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-delete-box-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        @if(details().boxName) {
            <div class="op-details__section-title">
                Box — delete
            </div>

            <div class="op-details__item">
                <i class="icon icon-xl icon-nucleo-box op-details__icon"></i>
                <div class="op-details__text">
                    <div class="op-details__name">{{ details().boxName }}</div>
                </div>
            </div>
        } @else {
            <div class="explanation">
                This box no longer exists.
            </div>
        }
    `
})
export class DeleteBoxOperationDetailsComponent {
    details = input.required<DeleteBoxOperationDetails>();
}
