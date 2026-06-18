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

export interface GetAgentToolsResponse {
    tools: AgentToolConfig[];
}

export interface AgentToolConfig {
    name: string;
    description: string;
    scope: string;
    requiredPermission: string | null;
    isAvailable: boolean;
    isEnabled: boolean;
    requiresApproval: boolean;
    isDefault: boolean;
}

export interface UpdateAgentToolConfigRequest {
    isEnabled: boolean;
    requiresApproval: boolean;
}

export interface GetAgentWorkspaceToolsResponse {
    tools: AgentWorkspaceToolConfig[];
}

export interface AgentWorkspaceToolConfig {
    name: string;
    description: string;
    isAvailable: boolean;
    globalIsEnabled: boolean;
    globalRequiresApproval: boolean;
    overrideIsEnabled: boolean | null;
    overrideRequiresApproval: boolean | null;
    effectiveIsEnabled: boolean;
    effectiveRequiresApproval: boolean;
}

export interface UpdateAgentWorkspaceToolOverrideRequest {
    isEnabled: boolean | null;
    requiresApproval: boolean | null;
}

export interface AgentWorkspace {
    externalId: string;
    name: string;
    currentSizeInBytes: number;
    maxSizeInBytes: number | null;
    isBucketCreated: boolean;
    storageName: string;
    overriddenToolsCount: number;
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
    overriddenToolsCount: number;
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

export interface GetPendingAgentOperationsResponse {
    items: PendingAgentOperation[];
}

export interface PendingAgentOperation {
    externalId: string;
    toolName: string;
    parameters: any;
    agent: PendingAgentOperationAgent;
    workspace: PendingAgentOperationWorkspace | null;
    createdAt: string;
    expiresAt: string;
}

export interface PendingAgentOperationAgent {
    externalId: string;
    name: string;
}

export interface PendingAgentOperationWorkspace {
    externalId: string;
    name: string;
}

export type AgentOperationDetails = BulkDeleteOperationDetails | DeleteShareLinkOperationDetails | RenameFolderOperationDetails | RenameFileOperationDetails | CreateFolderOperationDetails | MoveItemsOperationDetails | CreateFileOperationDetails | RenameWorkspaceOperationDetails | CreateShareLinkOperationDetails | UpdateShareLinkOperationDetails | CreateWorkspaceOperationDetails | ReadFileOperationDetails | GetFileOperationDetails | GetFileDownloadLinkOperationDetails | ListWorkspacesOperationDetails | ListStoragesOperationDetails | ListShareLinksOperationDetails | GetShareLinkOperationDetails | SearchOperationDetails | ListWorkspaceContentOperationDetails | GetBulkDownloadLinkOperationDetails;

export interface BulkDeleteOperationDetails {
    $type: 'bulk_delete';
    folders: BulkDeleteFolderDetail[];
    files: BulkDeleteFileDetail[];
}

export interface DeleteShareLinkOperationDetails {
    $type: 'delete_share_link';
    externalId: string;
    name: string | null;
}

export interface RenameFolderOperationDetails {
    $type: 'rename_folder';
    folderExternalId: string;
    currentName: string | null;
    newName: string;
    path: string | null;
}

export interface RenameFileOperationDetails {
    $type: 'rename_file';
    fileExternalId: string;
    folderExternalId: string | null;
    currentName: string | null;
    newName: string;
    path: string | null;
}

export interface CreateFolderOperationDetails {
    $type: 'create_folder';
    name: string;
    parentFolderExternalId: string | null;
    parentLocation: string | null;
}

export interface MoveItemsOperationDetails {
    $type: 'move_items';
    destinationFolderExternalId: string | null;
    destinationName: string | null;
    destinationPath: string | null;
    folders: MoveItemDetail[];
    files: MoveItemDetail[];
}

export interface MoveItemDetail {
    externalId: string;
    name: string;
    path: string | null;
}

export interface CreateFileOperationDetails {
    $type: 'create_file';
    name: string;
    folderExternalId: string | null;
    parentLocation: string | null;
    sizeInBytes: number;
    contentPreview: string;
    isPreviewTruncated: boolean;
}

export interface RenameWorkspaceOperationDetails {
    $type: 'rename_workspace';
    workspaceExternalId: string;
    currentName: string | null;
    newName: string;
}

export interface CreateShareLinkOperationDetails {
    $type: 'create_share_link';
    name: string;
    sharedFolders: ShareLinkItemDetail[];
    sharedFiles: ShareLinkItemDetail[];
    excludedFolders: ShareLinkItemDetail[];
    excludedFiles: ShareLinkItemDetail[];
    expiresAt: string | null;
    maxDownloads: number | null;
    hasPassword: boolean;
}

export interface ShareLinkItemDetail {
    externalId: string;
    name: string;
    path: string | null;
}

export interface UpdateShareLinkOperationDetails {
    $type: 'update_share_link';
    shareLinkExternalId: string;
    currentName: string | null;
    updateName: boolean;
    newName: string | null;
    updateExpiration: boolean;
    expiresAt: string | null;
    updateMaxDownloads: boolean;
    maxDownloads: number | null;
    updatePassword: boolean;
    passwordSet: boolean;
}

export interface CreateWorkspaceOperationDetails {
    $type: 'create_workspace';
    name: string;
    storageExternalId: string;
    storageName: string | null;
}

export interface ReadFileOperationDetails {
    $type: 'read_file';
    fileExternalId: string;
    name: string | null;
    path: string | null;
    offset: number;
    maxBytes: number | null;
}

export interface GetFileOperationDetails {
    $type: 'get_file';
    fileExternalId: string;
    name: string | null;
    path: string | null;
}

export interface GetFileDownloadLinkOperationDetails {
    $type: 'get_file_download_link';
    fileExternalId: string;
    name: string | null;
    path: string | null;
    expiresInMinutes: number | null;
}

export interface ListWorkspacesOperationDetails {
    $type: 'list_workspaces';
}

export interface ListStoragesOperationDetails {
    $type: 'list_storages';
}

export interface ListShareLinksOperationDetails {
    $type: 'list_share_links';
}

export interface GetShareLinkOperationDetails {
    $type: 'get_share_link';
    shareLinkExternalId: string;
    name: string | null;
}

export interface SearchOperationDetails {
    $type: 'search';
    nameContains: string[];
    types: string[];
    extensions: string[];
}

export interface ListWorkspaceContentOperationDetails {
    $type: 'list_workspace_content';
    folderExternalId: string | null;
    folderName: string | null;
    type: string | null;
}

export interface GetBulkDownloadLinkOperationDetails {
    $type: 'get_bulk_download_link';
    folders: BulkDownloadItemDetail[];
    files: BulkDownloadItemDetail[];
    excludedFolders: BulkDownloadItemDetail[];
    excludedFiles: BulkDownloadItemDetail[];
    expiresInMinutes: number | null;
}

export interface BulkDownloadItemDetail {
    externalId: string;
    name: string;
    path: string | null;
}

export interface BulkDeleteFolderDetail {
    externalId: string;
    name: string;
    path: string | null;
}

export interface BulkDeleteFileDetail {
    externalId: string;
    folderExternalId: string | null;
    name: string;
    path: string | null;
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

    public async getAgentTools(externalId: string): Promise<GetAgentToolsResponse> {
        const call = this
            ._http
            .get<GetAgentToolsResponse>(
                `/api/agents/${externalId}/tools`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updateAgentToolConfig(externalId: string, toolName: string, request: UpdateAgentToolConfigRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/agents/${externalId}/tools/${toolName}`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async resetAgentToolConfig(externalId: string, toolName: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/agents/${externalId}/tools/${toolName}`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async getAgentWorkspaceTools(externalId: string, workspaceExternalId: string): Promise<GetAgentWorkspaceToolsResponse> {
        const call = this
            ._http
            .get<GetAgentWorkspaceToolsResponse>(
                `/api/agents/${externalId}/workspaces/${workspaceExternalId}/tools`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updateAgentWorkspaceToolOverride(externalId: string, workspaceExternalId: string, toolName: string, request: UpdateAgentWorkspaceToolOverrideRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/agents/${externalId}/workspaces/${workspaceExternalId}/tools/${toolName}`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async resetAgentWorkspaceToolOverride(externalId: string, workspaceExternalId: string, toolName: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/agents/${externalId}/workspaces/${workspaceExternalId}/tools/${toolName}`, {
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

    public async getPendingOperations(): Promise<GetPendingAgentOperationsResponse> {
        const call = this
            ._http
            .get<GetPendingAgentOperationsResponse>(
                `/api/agents/operations/pending`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async getOperationDetails(operationExternalId: string): Promise<AgentOperationDetails> {
        const call = this
            ._http
            .get<AgentOperationDetails>(
                `/api/agents/operations/${operationExternalId}/details`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async approveOperation(agentExternalId: string, operationExternalId: string): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/agents/${agentExternalId}/operations/${operationExternalId}/approve`, {}, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async denyOperation(agentExternalId: string, operationExternalId: string): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/agents/${agentExternalId}/operations/${operationExternalId}/deny`, {}, {
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
