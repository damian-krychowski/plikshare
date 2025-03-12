import { Component, input, computed, output, Signal } from "@angular/core";
import { AppBoxPermissions, BoxPermissionsListComponent } from "../../../../shared/box-permissions/box-permissions-list.component";
import { ConfirmOperationDirective } from "../../../../shared/operation-confirm/confirm-operation.directive";
import { ActionButtonComponent } from "../../../../shared/buttons/action-btn/action-btn.component";

export type AppBoxTeamMember = {
    memberExternalId: Signal<string>;
    email: string;
    permissions: AppBoxPermissions;
}

@Component({
    selector: 'app-box-team-member',
    imports: [
        BoxPermissionsListComponent,
        ActionButtonComponent
    ],
    templateUrl: './box-team-member.component.html',
    styleUrl: './box-team-member.component.scss'
})
export class BoxTeamMemberComponent {
    member = input.required<AppBoxTeamMember>();

    revoked = output<void>();
    permissionsChanged = output<void>();

    memberEmail = computed(() => this.member().email);
    permissions = computed(() => this.member().permissions);
}