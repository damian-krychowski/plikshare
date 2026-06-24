import { Component, input } from '@angular/core';
import { UpdateBoxLinkOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-update-box-link-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        <div class="op-details__section-title">
            Box link - update
        </div>

        @if(details().currentName) {
            <div class="op-details__item">
                <i class="icon icon-xl icon-nucleo-link op-details__icon"></i>
                <div class="op-details__text">
                    <div class="op-details__name">{{ details().currentName }}</div>
                </div>
            </div>
        }

        <ul class="op-details__changes">
            @if(details().updateName) {
                <li>Rename to <strong>{{ details().newName }}</strong></li>
            }
            @if(details().updateIsEnabled) {
                <li>{{ details().isEnabled ? 'Enable' : 'Disable' }} the link</li>
            }
            @if(details().updatePermissions) {
                <li>Update permissions</li>
            }
            @if(details().updateWidgetOrigins) {
                <li>Update widget origins</li>
            }
        </ul>
    `
})
export class UpdateBoxLinkOperationDetailsComponent {
    details = input.required<UpdateBoxLinkOperationDetails>();
}
