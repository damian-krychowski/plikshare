import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface GetUserDetailsResponse {
    user: {
        externalId: string;
        email: string;
        isEmailConfirmed: boolean;
        roles: {
            isAppOwner: boolean;
            isAdmin: boolean;
        };        
        permissions: {
            canAddWorkspace: boolean;
            canManageGeneralSettings: boolean;
            canManageUsers: boolean;
            canManageStorages: boolean;
            canManageEmailProviders: boolean;
        };
        maxWorkspaceNumber: number | null;
        defaultMaxWorkspaceSizeInBytes: number | null;
    };

    workspaces: {
        externalId: string;
        name: string;
        storageName: string;
        currentSizeInBytes: number;
        maxSizeInBytes: number | null;
        isUsedByIntegration: boolean;
        isBucketCreated: boolean;
    }[];

    sharedWorkspaces: {
        externalId: string;
        name: string;
        storageName: string;
        currentSizeInBytes: number;
        maxSizeInBytes: number | null;
        owner: {
            externalId: string;
            email:string;
        },
        inviter: {
            externalId: string;
            email:string;
        },
        wasInvitationAccepted: boolean;
        isUsedByIntegration: boolean;
        permissions: {
            allowShare: boolean;
        };
        isBucketCreated: boolean;
    }[];

    sharedBoxes: {
        workspaceExternalId: string;
        workspaceName: string;
        storageName: string;
        owner: {
            externalId: string;
            email:string;
        },
        boxExternalId: string;
        boxName: string;
        inviter: {
            externalId: string;
            email:string;
        },
        wasInvitationAccepted: boolean;
        permissions: {
            allowDownload: boolean;
            allowUpload: boolean;
            allowList: boolean;
            allowDeleteFile: boolean;
            allowRenameFile: boolean;
            allowMoveItems: boolean;
            allowCreateFolder: boolean;
            allowRenameFolder: boolean;
            allowDeleteFolder: boolean;
        };
    }[];
};

export interface GetUsersResponseDto {
    items: UserItemDto[];
}

export interface UserItemDto {
    externalId: string;
    email: string;
    isEmailConfirmed: boolean;
    workspacesCount: number;
    roles: {
        isAppOwner: boolean;
        isAdmin: boolean;
    };
    permissions: {
        canAddWorkspace: boolean;
        canManageGeneralSettings: boolean;
        canManageUsers: boolean;
        canManageStorages: boolean;
        canManageEmailProviders: boolean;
    };
    maxWorkspaceNumber: number | null;
    defaultMaxWorkspaceSizeInBytes: number | null;
}

export interface SetIsAdminRequest {
    isAdmin: boolean;
}

export interface UpdateUserPermissionRequest {
    permissionName: UserPermission;
    operation: 'add-permission' | 'remove-permission';
}

export type UserPermission = 
    'add:workspace' 
    | 'manage:general-settings' 
    | 'manage:users' 
    | 'manage:storages' 
    | 'manage:email-providers';

export interface InviteUsersRequest {
    emails: string[];
}

export interface InviteUsersResponse {
    users: {
        email: string;
        externalId: string;
    }[];
}

export interface UpdateUserDefaultMaxWorkspaceSizeInBytesRequest {
    defaultMaxWorkspaceSizeInBytes: number | null;
}

export interface UpdateUserMaxWorkspaceNumberRequest {
    maxWorkspaceNumber: number | null;
}

@Injectable({
    providedIn: 'root'
})
export class UsersApi {
    constructor(
        private _http: HttpClient) {
    }

    public async getUserDetails(userExternalId: string): Promise<GetUserDetailsResponse> {
        const call = this
            ._http
            .get<GetUserDetailsResponse>(
                `/api/users/${userExternalId}`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async getUsers(): Promise<GetUsersResponseDto> {
        const call = this
            ._http
            .get<GetUsersResponseDto>(
                `/api/users`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async inviteUsers(request: InviteUsersRequest): Promise<InviteUsersResponse> {
        const call = this
            ._http
            .post<InviteUsersResponse>(
                `/api/users`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async setIsAdmin(userExternalId: string, request: SetIsAdminRequest) {
        const call = this
            ._http
            .patch(
                `/api/users/${userExternalId}/is-admin`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }


    public async updateUserPermission(userExternalId: string, request: UpdateUserPermissionRequest) {
        const call = this
            ._http
            .patch(
                `/api/users/${userExternalId}/permission`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateUserMaxWorkspaceNumber(userExternalId: string, request: UpdateUserMaxWorkspaceNumberRequest) {
        const call = this
            ._http
            .patch(
                `/api/users/${userExternalId}/max-workspace-number`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateUserDefaultMaxWorkspaceSizeInBytes(userExternalId: string, request: UpdateUserDefaultMaxWorkspaceSizeInBytesRequest) {
        const call = this
            ._http
            .patch(
                `/api/users/${userExternalId}/default-max-workspace-size-in-bytes`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async deleteUser(userExternalId: string) {
        const call = this
            ._http
            .delete(
                `/api/users/${userExternalId}`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }
}