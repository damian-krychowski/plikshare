import { Component, Signal, WritableSignal, computed, input } from "@angular/core";
import { toggle } from "../signal-utils";
import { Debouncer } from "../../services/debouncer";
import { AuthService } from "../../services/auth.service";
import { AppUserPermissions, AppUserRoles } from "../user-item/app-user";
import { UserPermission, UsersApi } from "../../services/users.api";
import { PermissionButtonComponent } from "../buttons/permission-btn/permission-btn.component";

export type AppUserPermissionsAndRoles = {
    externalId: Signal<string | null>;
    roles: AppUserRoles;
    permissions: AppUserPermissions;
}

export function hasUserAnyPermission(userSignal: Signal<AppUserPermissionsAndRoles>): Signal<boolean> {
    return computed(() => {
        const user = userSignal();

        return user.roles.isAdmin() 
        || user.permissions.canManageGeneralSettings()
        || user.permissions.canAddWorkspace()
        || user.permissions.canManageUsers()
        || user.permissions.canManageStorages()
        || user.permissions.canManageEmailProviders()
    });
}

@Component({
    selector: 'app-user-permissions-list',
    imports: [
        PermissionButtonComponent
    ],
    styleUrl: './user-permissions-list.component.scss',
    templateUrl: './user-permissions-list.component.html'
})
export class UserPermissionsListComponent {
    user = input.required<AppUserPermissionsAndRoles>();
    isReadOnly = input(false);

    userExternalId = computed(() => this.user().externalId());

    canAddWorkspace = computed(() => this.user().permissions.canAddWorkspace());
    isCanAddWorkspaceReadOnly = computed(() => this.isReadOnly());

    isAdmin = computed(() => this.user().roles.isAdmin());
    isAdminReadOnly = computed(() => this.isReadOnly() || !this.auth.isAppOwner());

    canManageGeneralSettings = computed(() => this.user().permissions.canManageGeneralSettings());
    isCanManageGeneralSettingsReadOnly = computed(() => this.isReadOnly() || !this.auth.isAppOwner());

    canManageUsers = computed(() => this.user().permissions.canManageUsers());
    isCanManageUsersReadOnly = computed(() => this.isReadOnly() || !this.auth.isAppOwner());

    canManageStorages = computed(() => this.user().permissions.canManageStorages());
    isCanManageStoragesReadOnly = computed(() => this.isReadOnly() || !this.auth.isAppOwner());
    
    canManageEmailProviders = computed(() => this.user().permissions.canManageEmailProviders());
    isCanManageEmailProvidersReadOnly = computed(() => this.isReadOnly() || !this.auth.isAppOwner());

    private _isAdminDebouncer = new Debouncer(500);
    private _canAddWorkspaceDebouncer = new Debouncer(500);
    private _canManageGeneralSettingsDebouncer = new Debouncer(500);
    private _canManageUsersDebouncer = new Debouncer(500);
    private _canManageStoragesDebouncer = new Debouncer(500);
    private _canManageEmailProvidersDebouncer = new Debouncer(500);

    constructor(
        public auth: AuthService,
        private _usersApi: UsersApi
    ) {}

    public onCanAddWorkspaceChange() {
        toggle(this.user().permissions.canAddWorkspace);
        
        this._canAddWorkspaceDebouncer.debounce(() => this.changePermission(
            this.user().permissions.canAddWorkspace, 
            "add:workspace"));
    }

    public onIsAdminChange() {
        toggle(this.user().roles.isAdmin);

        this._isAdminDebouncer.debounce(() => this.changeIsAdmin());
    }   

    public onCanManageGeneralSettingsChange() {
        toggle(this.user().permissions.canManageGeneralSettings);

        this._canManageGeneralSettingsDebouncer.debounce(() => this.changePermission(
            this.user().permissions.canManageGeneralSettings, 
            "manage:general-settings"));
    }    

    public onCanManageUsersChange() {
        toggle(this.user().permissions.canManageUsers);

        this._canManageUsersDebouncer.debounce(() => this.changePermission(
            this.user().permissions.canManageUsers, 
            "manage:users"));
    }    

    public onCanManageStoragesChange() {
        toggle(this.user().permissions.canManageStorages);

        this._canManageStoragesDebouncer.debounce(() => this.changePermission(
            this.user().permissions.canManageStorages, 
            "manage:storages"));
    }   

    public onCanManageEmailProvidersChange() {
        toggle(this.user().permissions.canManageEmailProviders);

        this._canManageEmailProvidersDebouncer.debounce(() => this.changePermission(
            this.user().permissions.canManageEmailProviders, 
            "manage:email-providers"));
    }   

    async changeIsAdmin() {
        const user = this.user();
        const userExternalId = user.externalId();

        if(!userExternalId)
            return;

        const originalValue = !user.roles.isAdmin();
        const originalManageGeneralSettings = user.permissions.canManageGeneralSettings();
        const originalManageUsers = user.permissions.canManageUsers();
        const originalManageStorages = user.permissions.canManageStorages();
        const originalManageEmailProviders = user.permissions.canManageEmailProviders();

        try {
            if(!user.roles.isAdmin()) {
                user.permissions.canManageGeneralSettings.set(false);
                user.permissions.canManageUsers.set(false);
                user.permissions.canManageStorages.set(false);
                user.permissions.canManageEmailProviders.set(false);
            }

            await this._usersApi.setIsAdmin(userExternalId, {
                isAdmin: user.roles.isAdmin()
            });
        } catch(error) {
            console.error(error);
            user.roles.isAdmin.set(originalValue);
            user.permissions.canManageGeneralSettings.set(originalManageGeneralSettings);
            user.permissions.canManageUsers.set(originalManageUsers);
            user.permissions.canManageStorages.set(originalManageStorages);
            user.permissions.canManageEmailProviders.set(originalManageEmailProviders);
        }
    }

    async changePermission(permission: WritableSignal<boolean>, permissionName: UserPermission) {
        const user = this.user();
        const userExternalId = user.externalId();

        if(!userExternalId)
            return;

        const originalValue = !permission();

        try {
            await this._usersApi.updateUserPermission(userExternalId, {
                permissionName: permissionName,
                operation: permission() ? 'add-permission' : 'remove-permission'
            });
        } catch(error) {
            console.error(error);
            permission.set(originalValue);
        }
    }
}