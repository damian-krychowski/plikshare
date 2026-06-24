import { Component, input } from '@angular/core';
import { UpdateBoxMemberPermissionsOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-update-box-member-permissions-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        @if(details().memberEmail) {
            <div class="op-details__section-title">
                Box member - permissions
            </div>

            <div class="op-details__meta">
                <div class="op-details__meta-row">
                    <span class="op-details__meta-label">Member</span>
                    <span class="op-details__meta-value">{{ details().memberEmail }}</span>
                </div>
                @if(details().boxName) {
                    <div class="op-details__meta-row">
                        <span class="op-details__meta-label">Box</span>
                        <span class="op-details__meta-value">{{ details().boxName }}</span>
                    </div>
                }
            </div>
        } @else {
            <div class="explanation">
                This member no longer exists.
            </div>
        }
    `
})
export class UpdateBoxMemberPermissionsOperationDetailsComponent {
    details = input.required<UpdateBoxMemberPermissionsOperationDetails>();
}
