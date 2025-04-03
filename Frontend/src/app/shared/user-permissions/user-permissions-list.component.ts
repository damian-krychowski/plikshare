import { Component, Signal, WritableSignal, computed, input, output } from "@angular/core";
import { toggle } from "../signal-utils";
import { AuthService } from "../../services/auth.service";
import { AppUserPermissions } from "../user-item/app-user";
import { PermissionButtonComponent } from "../buttons/permission-btn/permission-btn.component";

export type AppUserPermissionsAndRoles = {
    roles: {
        isAdmin: WritableSignal<boolean>;
    };

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

export type UserPermissionsAndRolesChangedEvent = {    
    isAdmin: boolean;

    canAddWorkspace: boolean;
    canManageGeneralSettings: boolean;
    canManageUsers: boolean;
    canManageStorages: boolean;
    canManageEmailProviders: boolean;
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
    configChanged = output<UserPermissionsAndRolesChangedEvent>();

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

    constructor(
        public auth: AuthService
    ) {}

    public onCanAddWorkspaceChange() {
        toggle(this.user().permissions.canAddWorkspace);
        this.emitConfigChange();
    }

    public onIsAdminChange() {
        toggle(this.user().roles.isAdmin);
        this.emitConfigChange();
    }   

    public onCanManageGeneralSettingsChange() {
        toggle(this.user().permissions.canManageGeneralSettings);
        this.emitConfigChange();
    }    

    public onCanManageUsersChange() {
        toggle(this.user().permissions.canManageUsers);
        this.emitConfigChange();
    }    

    public onCanManageStoragesChange() {
        toggle(this.user().permissions.canManageStorages);
        this.emitConfigChange();
    }   

    public onCanManageEmailProvidersChange() {
        toggle(this.user().permissions.canManageEmailProviders);
        this.emitConfigChange();
    }   

    private emitConfigChange() {
        const isAdmin = this.isAdmin();

        this.configChanged.emit({
            isAdmin: isAdmin,
            
            canAddWorkspace: this.canAddWorkspace(),

            canManageEmailProviders: isAdmin && this.canManageEmailProviders(),
            canManageGeneralSettings: isAdmin && this.canManageGeneralSettings(),
            canManageStorages: isAdmin && this.canManageStorages(),
            canManageUsers: isAdmin && this.canManageUsers()
        });
    }
}