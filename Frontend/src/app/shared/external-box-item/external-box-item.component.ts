import { Component, computed, input, output, Signal, WritableSignal } from "@angular/core";
import { NavigationExtras, Router } from "@angular/router";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { PrefetchDirective } from "../prefetch.directive";
import { DataStore } from "../../services/data-store.service";
import { BoxExternalAccessApi } from "../../services/box-external-access.api";
import { InAppSharing } from "../../services/in-app-sharing.service";
import { AppBoxPermissions, BoxPermissionsListComponent } from "../box-permissions/box-permissions-list.component";
import { AppUser } from '../app-user';
import { UserLinkComponenet } from "../user-link/user-link.component";
import { AppWorkspaceDetails } from "../app-workspace-details";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";

export type AppExternalBox = {
    type: 'app-external-box';

    boxExternalId: Signal<string>;
    boxName: Signal<string>;    
    owner: Signal<AppUser>;
    isHighlighted: WritableSignal<boolean>;
    permissions: Signal<AppBoxPermissions>;
    workspace: Signal<AppWorkspaceDetails | undefined>;
}

@Component({
    selector: 'app-external-box-item',
    imports: [
        ConfirmOperationDirective,
        PrefetchDirective,
        BoxPermissionsListComponent,
        UserLinkComponenet,
        ActionButtonComponent
    ],
    templateUrl: './external-box-item.component.html',
    styleUrl: './external-box-item.component.scss'
})
export class ExternalBoxItemComponent {
    externalBox = input.required<AppExternalBox>();

    canOpen = input(false);
    canLocate = input(false);
    isAdminView = input(false);

    leave = output<void>();
    permissionsChange = output<void>();
    accessRevoked = output<void>();

    externalId = computed(() => this.externalBox().boxExternalId());
    name = computed(() => this.externalBox().boxName());
    owner = computed(() => this.externalBox().owner());
    permissions = computed(() => this.externalBox().permissions());
    isHighlighted = observeIsHighlighted(this.externalBox);

    workspace = computed(() => this.externalBox().workspace());
    workspaceExternalId = computed(() => this.externalBox().workspace()?.externalId);

    constructor(
        private _inAppSharing: InAppSharing,
        private _router: Router,
        public dataStore: DataStore,
        private _api: BoxExternalAccessApi
    ) { }

    prefetchExternalBox() {
        this.dataStore.prefetchExternalBoxDetailsAndContent(
            this.externalId()
        );
    }

    openExternalBox() {
        if (!this.canOpen()) {
            return;
        }
        
        if(this.isHighlighted()) {
            this.externalBox().isHighlighted.set(false);
        }

        this._router.navigate([`/box/${this.externalId()}`]);
    }

    locateInWorkspace() {        
        if (!this.workspaceExternalId()) {
            return;
        }

        const temporaryKey = this._inAppSharing.set(
            this.externalId());

        const navigationExtras: NavigationExtras = {
            state: {
                boxToHighlight: temporaryKey
            }
        };

        this._router.navigate([`/workspaces/${this.workspaceExternalId()}/boxes`], navigationExtras);
    }

    async leaveBox() {
        this.leave.emit();

        await this._api.leaveBoxMembership(
            this.externalId()
        );
    }

    locate() {
        const temporaryKey = this._inAppSharing.set(
            this.externalId());

        const navigationExtras: NavigationExtras = {
            state: {
                externalBoxToHighlight: temporaryKey
            }
        };

        this._router.navigate([`/workspaces`], navigationExtras);
    }
}