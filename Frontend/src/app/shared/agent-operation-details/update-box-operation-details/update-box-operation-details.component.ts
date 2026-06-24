import { Component, input } from '@angular/core';
import { UpdateBoxOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-update-box-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        <div class="op-details__section-title">
            Box - update
        </div>

        @if(details().currentName) {
            <div class="op-details__item">
                <i class="icon icon-xl icon-nucleo-box op-details__icon"></i>
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
                <li>{{ details().isEnabled ? 'Enable' : 'Disable' }} the box</li>
            }
            @if(details().updateFolder) {
                <li>Point at folder <strong>{{ details().folderExternalId }}</strong></li>
            }
        </ul>
    `
})
export class UpdateBoxOperationDetailsComponent {
    details = input.required<UpdateBoxOperationDetails>();
}
