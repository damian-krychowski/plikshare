import { Component, input } from '@angular/core';
import { InviteWorkspaceMembersOperationDetails } from '../../../services/agents.api';

@Component({
    selector: 'app-invite-workspace-members-operation-details',
    standalone: true,
    imports: [],
    styleUrl: '../_op-details.scss',
    template: `
        <div class="op-details__section-title">
            Workspace{{ details().workspaceName ? ' — ' + details().workspaceName : '' }}
        </div>

        <div class="explanation">
            Invites {{ details().memberEmails.length }} {{ details().memberEmails.length === 1 ? 'person' : 'people' }}
            to this workspace.
            {{ details().allowShare ? 'They will be able to invite further members.' : 'They will not be able to invite further members.' }}
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
export class InviteWorkspaceMembersOperationDetailsComponent {
    details = input.required<InviteWorkspaceMembersOperationDetails>();
}
