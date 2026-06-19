import { Component, input } from '@angular/core';
import { RevokeWorkspaceMemberOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-revoke-workspace-member-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        @if(details().memberEmail) {
            <div class="op-details__section-title">
                Workspace member — remove
            </div>

            <div class="op-details__meta">
                <div class="op-details__meta-row">
                    <span class="op-details__meta-label">Member</span>
                    <span class="op-details__meta-value">{{ details().memberEmail }}</span>
                </div>
                @if(details().workspaceName) {
                    <div class="op-details__meta-row">
                        <span class="op-details__meta-label">Workspace</span>
                        <span class="op-details__meta-value">{{ details().workspaceName }}</span>
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
export class RevokeWorkspaceMemberOperationDetailsComponent {
    details = input.required<RevokeWorkspaceMemberOperationDetails>();
}
