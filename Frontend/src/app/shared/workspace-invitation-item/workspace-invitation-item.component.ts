import { Component, computed, input, output, WritableSignal } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ConfirmOperationDirective } from '../operation-confirm/confirm-operation.directive';
import { AppUser } from '../app-user';
import { UserLinkComponenet } from '../user-link/user-link.component';
import { ActionButtonComponent } from '../buttons/action-btn/action-btn.component';

export type AppWorkspaceInvitation = {
    type: 'app-workspace-invitation';

    externalId: WritableSignal<string>;
    name: string;
    inviter: AppUser;
    owner: AppUser;
    permissions: {
        allowShare: boolean;
    },
    isUsedByIntegration: boolean;
    isBucketCreated: boolean;
}

@Component({
    selector: 'app-workspace-invitation-item',
    imports: [
        MatProgressBarModule,
        ConfirmOperationDirective,
        UserLinkComponenet,
        ActionButtonComponent
    ],
    templateUrl: './workspace-invitation-item.component.html',
    styleUrl: './workspace-invitation-item.component.scss'
})
export class WorkspaceInvitationItemComponent {
    invitation = input.required<AppWorkspaceInvitation>();
    isAdminView = input(false);

    accepted = output<void>();
    rejected = output<void>();
    cancelled = output<void>();

    name = computed(() => this.invitation().name);
    inviter = computed(() => this.invitation().inviter);
}
