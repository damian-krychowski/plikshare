import { Component, computed, input, output, signal } from "@angular/core";
import { MatTooltipModule } from "@angular/material/tooltip";
import { NavigationExtras, Router } from "@angular/router";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { PrefetchDirective } from "../prefetch.directive";
import { InAppSharing } from "../../services/in-app-sharing.service";
import { hasUserAnyPermission, UserPermissionsAndRolesChangedEvent, UserPermissionsListComponent } from "../user-permissions/user-permissions-list.component";
import { AuthService } from "../../services/auth.service";
import { DataStore } from "../../services/data-store.service";
import { AppUserDetails } from "./app-user";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { Operations, OptimisticOperation } from "../../services/optimistic-operation";
import { UsersApi } from "../../services/users.api";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";
import { MaxWorkspaceSizePipe } from "../storage-size.pipe";
import { Debouncer } from "../../services/debouncer";

@Component({
    selector: 'app-user-item',
    imports: [
    MatTooltipModule,
    ConfirmOperationDirective,
    PrefetchDirective,
    UserPermissionsListComponent,
    ActionButtonComponent,
    MaxWorkspaceSizePipe
],
    templateUrl: './user-item.component.html',
    styleUrl: './user-item.component.scss'
})
export class UserItemComponent {
    user = input.required<AppUserDetails>();
    searchPhrase = input<string | null>(null);
    canLocate = input(false);
    pickerMode = input(false);
    hideBorder = input(false)

    clicked = output<void>();
    deleted = output<OptimisticOperation>();

   
    userExternalId = computed(() => this.user().externalId());
    userEmail = computed(() => this.getUserEmailWithHighlight(this.user(), this.searchPhrase()));
    userWorkspacesCount = computed(() => this.user().workspacesCount());
    hasAnyWorkspaces = computed(() => this.userWorkspacesCount() > 0);
    
    canAddWorkspace = computed(() => this.user()?.permissions.canAddWorkspace() ?? false);
    maxWorkspaceNumber  = computed(() => this.user()?.maxWorkspaceNumber());    
    defaultMaxWorkspaceSizeInBytes  = computed(() => this.user()?.defaultMaxWorkspaceSizeInBytes());

    isAdmin = computed(() => this.user().roles.isAdmin());
    isAppOwner = computed(() => this.user().roles.isAppOwner());
    isLoggedInUser = computed(() => this.userExternalId() == this._auth.userExternalId());
    isHighlighted = observeIsHighlighted(this.user);
    isEmailConfirmed = computed(() => this.user().isEmailConfirmed()); 

    canGoToUserDetails = computed(() => this.userExternalId() != null);

    canUserBeDeleted = computed(() => 
        !this.isAppOwner() 
        && !this.isLoggedInUser() 
        && this._auth.canManageUsers()
        && ((this.isAdmin() && this._auth.isAppOwner()) || !this.isAdmin()));

    arePremissionsVisible = computed(() => !this.isAppOwner() && !(this.pickerMode() && !this.hasAnyPermission()));    
    arePermissionsReadOnly = computed(() => this.pickerMode() || this.isBeingCreated());

    hasAnyPermission = hasUserAnyPermission(this.user);

    areActionsVisible = signal(false);

    isBeingCreated = computed(() => this.userExternalId() == null);

    constructor(
        public dataStore: DataStore,
        private _inAppSharing: InAppSharing,
        private _router: Router,
        private _auth: AuthService,
        private _usersApi: UsersApi
    ) { }

    prefetchUserDetails() {
        const externalId = this.userExternalId();

        if(!externalId)
            return;

        this.dataStore.prefetchUserDetails(externalId);
    }

    locate() {
        const externalId = this.userExternalId();

        if(!externalId)
            return;

        const temporaryKey = this._inAppSharing.set(
            externalId);

        const navigationExtras: NavigationExtras = {
            state: {
                userToHighlight: temporaryKey
            }
        };

        // this._router.navigate([`/workspaces`], navigationExtras);
        throw new Error("not yet implemented");
    }  

    toggleActions() {
        this.areActionsVisible.update(value => !value);
    }

    goToUserDetails() {
        const externalId = this.userExternalId();

        if(!externalId)
            return;

        this._router.navigate([`settings/users/${externalId}`])
    }
    
    onClicked() {
        this.clicked.emit();
    }

    private getUserEmailWithHighlight(user: AppUserDetails, searchPhrase: string | null): string {
        const email = user.email()
        const emailLowered = email.toLowerCase();

        const searchPhraseLowered = searchPhrase?.toLowerCase();
       
        if (!searchPhraseLowered || !emailLowered.includes(searchPhraseLowered)) {
          return email;
        }
    
        const startIndex = emailLowered.indexOf(searchPhraseLowered);
        const endIndex = startIndex + searchPhraseLowered.length;
        const highlightedEmail = `${email.slice(0, startIndex)}<strong>${email.slice(startIndex, endIndex)}</strong>${email.slice(endIndex)}`;
        
        return highlightedEmail;
    }

    async deleteUser() {
        const externalId = this.userExternalId();

        if(!externalId)
            return;

        const operation = Operations.optimistic();
        this.deleted.emit(operation);

        try {
            await this._usersApi.deleteUser(externalId);
            operation.succeeded()
        } catch (error) {
            console.error(error);
            operation.failed(error);
        }
    }

    private _permissionsAndRolesDebouncer = new Debouncer(500);
    public onUserPermissionsAndRolesChange(event: UserPermissionsAndRolesChangedEvent) {
        this._permissionsAndRolesDebouncer.debounceAsync(() => this.savePermissionsAndRoles(event));
    }

    private async savePermissionsAndRoles(event: UserPermissionsAndRolesChangedEvent) {
        const externalId = this.userExternalId();

        if(!externalId)
            return;
        
        try {            
            await this._usersApi.updatePermissionsAndRoles(externalId, event);
        } catch (error) {
            console.error(error);
        }
    }
}