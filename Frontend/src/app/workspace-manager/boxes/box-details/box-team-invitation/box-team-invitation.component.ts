import { Component, input, computed, output, WritableSignal } from "@angular/core";
import { AppBoxPermissions, BoxPermissionsListComponent } from "../../../../shared/box-permissions/box-permissions-list.component";
import { ActionButtonComponent } from "../../../../shared/buttons/action-btn/action-btn.component";

export type AppBoxTeamInvitation = {
    memberExternalId: WritableSignal<string | null>;
    email: string;
    inviterEmail: string;
    permissions: AppBoxPermissions;
}

@Component({
    selector: 'app-box-team-invitation',
    imports: [
        BoxPermissionsListComponent,        
        ActionButtonComponent
    ],
    templateUrl: './box-team-invitation.component.html',
    styleUrl: './box-team-invitation.component.scss'
})
export class BoxTeamInvitationComponent {
    invitaiton = input.required<AppBoxTeamInvitation>();

    canceled = output<void>();
    permissionsChanged = output<void>();

    inviteeEmail = computed(() => this.invitaiton().email);
    inviterEmail = computed(() => this.invitaiton().inviterEmail);
    permissions = computed(() => this.invitaiton().permissions);
}