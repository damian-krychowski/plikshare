import { Component, computed, input, output, Signal } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { NavigationExtras, Router } from '@angular/router';
import { ConfirmOperationDirective } from '../operation-confirm/confirm-operation.directive';
import { AppBoxPermissions, BoxPermissionsListComponent } from '../box-permissions/box-permissions-list.component';
import { InAppSharing } from '../../services/in-app-sharing.service';
import { DataStore } from '../../services/data-store.service';
import { AppUser } from '../app-user';
import { UserLinkComponenet } from '../user-link/user-link.component';
import { AppWorkspaceDetails } from '../app-workspace-details';
import { ActionButtonComponent } from '../buttons/action-btn/action-btn.component';

export type AppBoxInvitation = {
    type: 'app-box-invitation',

    boxExternalId: Signal<string>;
    boxName: Signal<string>;
    inviter: Signal<AppUser>;
    owner: Signal<AppUser>;
    permissions: Signal<AppBoxPermissions>;
    workspace: Signal<AppWorkspaceDetails | undefined>;
}

@Component({
    selector: 'app-box-invitation-item',
    imports: [
        MatProgressBarModule,
        ConfirmOperationDirective,
        BoxPermissionsListComponent,
        UserLinkComponenet,
        ActionButtonComponent
    ],
    templateUrl: './box-invitation-item.component.html',
    styleUrl: './box-invitation-item.component.scss'
})
export class BoxInvitationItemComponent {
    invitation = input.required<AppBoxInvitation>();
    isAdminView = input(false);

    workspaceExternalId = computed(() => this.invitation().workspace()?.externalId);

    accepted = output<void>();
    rejected = output<void>();
    cancelled = output<void>();
    permissionsChange = output<void>();

    constructor(
        public dataStore: DataStore,
        private _inAppSharing: InAppSharing,
        private _router: Router
    ){

    }

    locateInWorkspace() {    
        if (!this.workspaceExternalId())
            return;

        const temporaryKey = this._inAppSharing.set(
            this.invitation().boxExternalId());

        const navigationExtras: NavigationExtras = {
            state: {
                boxToHighlight: temporaryKey
            }
        };

        this._router.navigate([`/workspaces/${this.workspaceExternalId()}/boxes`], navigationExtras);
    }
}
