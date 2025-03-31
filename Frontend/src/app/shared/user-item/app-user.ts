import { Signal, WritableSignal } from "@angular/core";

export type AppUserDetails = {
    externalId: WritableSignal<string | null>;
    email: Signal<string>;
    isEmailConfirmed: Signal<boolean>;
    workspacesCount: Signal<number>;
    roles: AppUserRoles;
    permissions: AppUserPermissions;
    maxWorkspaceNumber: WritableSignal<number | null>;
    defaultMaxWorkspaceSizeInBytes: WritableSignal<number | null>;

    isHighlighted: WritableSignal<boolean>;
}

export type AppUserRoles = {
    isAppOwner: Signal<boolean>;
    isAdmin: WritableSignal<boolean>;
}

export type AppUserPermissions = {
    canAddWorkspace: WritableSignal<boolean>;
    canManageGeneralSettings: WritableSignal<boolean>;
    canManageUsers: WritableSignal<boolean>;
    canManageStorages: WritableSignal<boolean>;
    canManageEmailProviders: WritableSignal<boolean>;
}