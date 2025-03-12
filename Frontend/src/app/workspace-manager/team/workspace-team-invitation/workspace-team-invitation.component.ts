import { Component, input, computed, output, Signal, WritableSignal } from "@angular/core";
import { ActionButtonComponent } from "../../../shared/buttons/action-btn/action-btn.component";

export type AppWorkspaceTeamInvitation = {
    memberExternalId: WritableSignal<string | null>;
    inviterEmail: Signal<string>;
    email: Signal<string>;
}

@Component({
    selector: 'app-workspace-team-invitation',
    imports: [
        ActionButtonComponent,
    ],
    templateUrl: './workspace-team-invitation.component.html',
    styleUrl: './workspace-team-invitation.component.scss'
})
export class WorkspaceTeamInvitationComponent {
    invitation = input.required<AppWorkspaceTeamInvitation>();
    cancelled = output<void>();

    memberEmail = computed(() => this.invitation().email());
    inviterEmail = computed(() => this.invitation().inviterEmail());
}