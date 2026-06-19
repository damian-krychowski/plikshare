import { Component, input } from '@angular/core';
import { InviteBoxMembersOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-invite-box-members-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        <div class="op-details__section-title">
            Box{{ details().boxName ? ' — ' + details().boxName : '' }}
        </div>

        <div class="explanation">
            Invites {{ details().memberEmails.length }} {{ details().memberEmails.length === 1 ? 'person' : 'people' }}
            to this box. They start with list-only access.
        </div>

        <div class="op-details__meta">
            @for(email of details().memberEmails; track email) {
                <div class="op-details__meta-row">
                    <i class="icon icon-lg icon-nucleo-user op-details__meta-icon"></i>
                    <span class="op-details__meta-value">{{ email }}</span>
                </div>
            }
        </div>
    `
})
export class InviteBoxMembersOperationDetailsComponent {
    details = input.required<InviteBoxMembersOperationDetails>();
}
