import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { BoxPermissions } from "./boxes.api";
import { UserStorageAccessMode } from "./general-settings.api";

export interface GetAgentsResponse {
    items: AgentListItem[];
}

export interface AgentListItem {
    externalId: string;
    name: string;
    isEnabled: boolean;
    createdAt: string;
}

export interface GetAgentDetailsResponse {
    agent: AgentDetails;
    ownedWorkspaces: AgentWorkspace[];
    sharedWorkspaces: AgentSharedWorkspace[];
    sharedBoxes: AgentSharedBox[];
}

export interface AgentDetails {
    externalId: string;
    name: string;
    isEnabled: boolean;
    createdAt: string;
    owner: AgentOwner;
    tokenMasked: string;
    tokenLastUsedAt: string | null;
    roles: AgentRoles;
    permissions: AgentPermissions;
    maxWorkspaceNumber: number | null;
    defaultMaxWorkspaceSizeInBytes: number | null;
    defaultMaxWorkspaceTeamMembers: number | null;
    storageAccess: AgentStorageAccess;
}

export interface AgentOwner {
    externalId: string;
    email: string;
}

export interface AgentRoles {
    isAdmin: boolean;
}

export interface AgentPermissions {
    canAddWorkspace: boolean;
    canManageGeneralSettings: boolean;
    canManageUsers: boolean;
    canManageStorages: boolean;
    canManageEmailProviders: boolean;
    canManageAuth: boolean;
    canManageIntegrations: boolean;
    canManageAuditLog: boolean;
    canManageAgents: boolean;
}

export interface AgentStorageAccess {
    mode: UserStorageAccessMode;
    storageExternalIds: string[];
}

export interface AgentWorkspace {
    externalId: string;
    name: string;
    currentSizeInBytes: number;
    maxSizeInBytes: number | null;
    isBucketCreated: boolean;
    storageName: string;
}

export interface AgentSharedWorkspace {
    externalId: string;
    name: string;
    storageExternalId: string;
    storageName: string;
    currentSizeInBytes: number;
    maxSizeInBytes: number | null;
    owner: AgentOwner;
    isBucketCreated: boolean;
    storageEncryptionType: string;
}

export interface AgentSharedBox {
    workspaceExternalId: string;
    workspaceName: string;
    storageName: string;
    owner: AgentOwner;
    boxExternalId: string;
    boxName: string;
    permissions: BoxPermissions;
}

export interface CreateAgentRequest {
    name: string;
}

export interface CreateAgentResponse {
    externalId: string;
    token: string;
    tokenMasked: string;
}

export interface RotateAgentTokenResponse {
    token: string;
    tokenMasked: string;
}

export interface UpdateAgentPermissionsAndRolesRequest {
    isAdmin: boolean;
    canAddWorkspace: boolean;
    canManageGeneralSettings: boolean;
    canManageUsers: boolean;
    canManageStorages: boolean;
    canManageEmailProviders: boolean;
    canManageAuth: boolean;
    canManageIntegrations: boolean;
    canManageAuditLog: boolean;
}

export interface ListWorkspaceBoxesResponse {
    items: WorkspaceBoxItem[];
}

export interface WorkspaceBoxItem {
    externalId: string;
    name: string;
}

@Injectable({
    providedIn: 'root'
})
export class AgentsApi {
    constructor(
        private _http: HttpClient) {
    }

    public async getAgents(): Promise<GetAgentsResponse> {
        const call = this
            ._http
            .get<GetAgentsResponse>(
                `/api/agents`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async getAgentDetails(externalId: string): Promise<GetAgentDetailsResponse> {
        const call = this
            ._http
            .get<GetAgentDetailsResponse>(
                `/api/agents/${externalId}`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async createAgent(request: CreateAgentRequest): Promise<CreateAgentResponse> {
        const call = this
            ._http
            .post<CreateAgentResponse>(
                `/api/agents`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async deleteAgent(externalId: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/agents/${externalId}`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async rotateToken(externalId: string): Promise<RotateAgentTokenResponse> {
        const call = this
            ._http
            .post<RotateAgentTokenResponse>(
                `/api/agents/${externalId}/token/rotate`, {}, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updatePermissionsAndRoles(externalId: string, request: UpdateAgentPermissionsAndRolesRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/agents/${externalId}/permissions-and-roles`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateMaxWorkspaceNumber(externalId: string, maxWorkspaceNumber: number | null): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/agents/${externalId}/max-workspace-number`, { maxWorkspaceNumber }, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateDefaultMaxWorkspaceSize(externalId: string, maxSizeInBytes: number | null): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/agents/${externalId}/default-max-workspace-size`, { maxSizeInBytes }, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateDefaultMaxWorkspaceTeamMembers(externalId: string, maxTeamMembers: number | null): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/agents/${externalId}/default-max-workspace-team-members`, { maxTeamMembers }, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateStorageAccess(externalId: string, mode: UserStorageAccessMode, storageExternalIds: string[]): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/agents/${externalId}/storage-access`, { mode, storageExternalIds }, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async grantWorkspaceAccess(externalId: string, workspaceExternalId: string): Promise<void> {
        const call = this
            ._http
            .put(
                `/api/agents/${externalId}/workspaces/${workspaceExternalId}`, {}, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async revokeWorkspaceAccess(externalId: string, workspaceExternalId: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/agents/${externalId}/workspaces/${workspaceExternalId}`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async grantBoxAccess(externalId: string, boxExternalId: string, permissions: BoxPermissions): Promise<void> {
        const call = this
            ._http
            .put(
                `/api/agents/${externalId}/boxes/${boxExternalId}`, permissions, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async revokeBoxAccess(externalId: string, boxExternalId: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/agents/${externalId}/boxes/${boxExternalId}`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async listWorkspaceBoxes(workspaceExternalId: string): Promise<ListWorkspaceBoxesResponse> {
        const call = this
            ._http
            .get<ListWorkspaceBoxesResponse>(
                `/api/agents/workspaces/${workspaceExternalId}/boxes`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }
}
