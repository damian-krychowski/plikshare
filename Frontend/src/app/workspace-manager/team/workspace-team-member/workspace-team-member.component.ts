import { Component, input, computed, output, Signal, WritableSignal } from "@angular/core";
import { ConfirmOperationDirective } from "../../../shared/operation-confirm/confirm-operation.directive";
import { ActionButtonComponent } from "../../../shared/buttons/action-btn/action-btn.component";

export type AppWorkspaceTeamMember = {
    memberExternalId: Signal<string>;
    email: Signal<string>;
    isPendingKeyGrant: WritableSignal<boolean>;
    permissions: {
        allowShare: Signal<boolean>;
    }
}

@Component({
    selector: 'app-workspace-team-member',
    imports: [
        ActionButtonComponent
    ],
    templateUrl: './workspace-team-member.component.html',
    styleUrl: './workspace-team-member.component.scss'
})
export class WorkspaceTeamMemberComponent {
    member = input.required<AppWorkspaceTeamMember>();
    canGrantEncryptionAccess = input<boolean>(false);

    revoked = output<void>();
    encryptionAccessGrantRequested = output<void>();

    memberEmail = computed(() => this.member().email());
    isPendingKeyGrant = computed(() => this.member().isPendingKeyGrant());
}