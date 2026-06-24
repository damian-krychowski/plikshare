import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
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
    maxWorkspaceNumber: number | null;
    defaultMaxWorkspaceSizeInBytes: number | null;
    defaultMaxWorkspaceTeamMembers: number | null;
    storageAccess: AgentStorageAccess;
}

export interface AgentOwner {
    externalId: string;
    email: string;
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
    kind: string;
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

export interface GetAgentBoxToolsResponse {
    tools: AgentBoxToolConfig[];
}

export interface AgentBoxToolConfig {
    name: string;
    description: string;
    globalIsEnabled: boolean;
    globalRequiresApproval: boolean;
    overrideIsEnabled: boolean | null;
    overrideRequiresApproval: boolean | null;
    effectiveIsEnabled: boolean;
    effectiveRequiresApproval: boolean;
}

export interface UpdateAgentBoxToolOverrideRequest {
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
    overriddenToolsCount: number;
}

export interface ListWorkspaceBoxesResponse {
    items: WorkspaceBoxItem[];
}

export interface WorkspaceBoxItem {
    externalId: string;
    name: string;
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

export type AgentOperationDetails = BulkDeleteOperationDetails | DeleteShareLinkOperationDetails | RenameFolderOperationDetails | RenameFileOperationDetails | CreateFolderOperationDetails | MoveItemsOperationDetails | CreateFileOperationDetails | RenameWorkspaceOperationDetails | CreateShareLinkOperationDetails | UpdateShareLinkOperationDetails | CreateWorkspaceOperationDetails | ReadFileOperationDetails | GetFileOperationDetails | GetFileDownloadLinkOperationDetails | ListWorkspacesOperationDetails | ListStoragesOperationDetails | ListShareLinksOperationDetails | GetShareLinkOperationDetails | SearchOperationDetails | ListWorkspaceContentOperationDetails | GetBulkDownloadLinkOperationDetails | ListWorkspaceMembersOperationDetails | InviteWorkspaceMembersOperationDetails | UpdateWorkspaceMemberPermissionsOperationDetails | RevokeWorkspaceMemberOperationDetails | ListWorkspaceBoxesOperationDetails | ListBoxesOperationDetails | GetBoxDetailsOperationDetails | ListBoxContentOperationDetails | ReadBoxFileOperationDetails | GetBoxFileDownloadLinkOperationDetails | GetBoxBulkDownloadLinkOperationDetails | SearchBoxOperationDetails | CreateBoxFolderOperationDetails | CreateBoxFileOperationDetails | RenameBoxFileOperationDetails | RenameBoxFolderOperationDetails | MoveBoxItemsOperationDetails | DeleteBoxItemsOperationDetails | GetBoxOperationDetails |CreateBoxOperationDetails | UpdateBoxOperationDetails | DeleteBoxOperationDetails | ListBoxLinksOperationDetails | CreateBoxLinkOperationDetails | UpdateBoxLinkOperationDetails | DeleteBoxLinkOperationDetails | RegenerateBoxLinkAccessCodeOperationDetails | ListBoxMembersOperationDetails | InviteBoxMembersOperationDetails | UpdateBoxMemberPermissionsOperationDetails | RevokeBoxMemberOperationDetails;

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

export interface ListWorkspaceMembersOperationDetails {
    $type: 'list_workspace_members';
    workspaceExternalId: string;
}

export interface InviteWorkspaceMembersOperationDetails {
    $type: 'invite_workspace_members';
    workspaceExternalId: string;
    workspaceName: string | null;
    memberEmails: string[];
    allowShare: boolean;
}

export interface UpdateWorkspaceMemberPermissionsOperationDetails {
    $type: 'update_workspace_member_permissions';
    workspaceExternalId: string;
    workspaceName: string | null;
    memberExternalId: string;
    memberEmail: string | null;
    allowShare: boolean;
}

export interface RevokeWorkspaceMemberOperationDetails {
    $type: 'revoke_workspace_member';
    workspaceExternalId: string;
    workspaceName: string | null;
    memberExternalId: string;
    memberEmail: string | null;
}

export interface ListWorkspaceBoxesOperationDetails {
    $type: 'list_workspace_boxes';
    workspaceExternalId: string;
}

export interface ListBoxesOperationDetails {
    $type: 'list_boxes';
}

export interface GetBoxDetailsOperationDetails {
    $type: 'get_box_details';
    boxExternalId: string;
    boxName: string | null;
}

export interface ListBoxContentOperationDetails {
    $type: 'list_box_content';
    boxExternalId: string;
    boxName: string | null;
    folderExternalId: string | null;
    folderName: string | null;
}

export interface ReadBoxFileOperationDetails {
    $type: 'read_box_file';
    boxExternalId: string;
    boxName: string | null;
    fileExternalId: string;
    name: string | null;
    path: string | null;
    offset: number;
    maxBytes: number | null;
}

export interface GetBoxFileDownloadLinkOperationDetails {
    $type: 'get_box_file_download_link';
    boxExternalId: string;
    boxName: string | null;
    fileExternalId: string;
    name: string | null;
    path: string | null;
    expiresInMinutes: number | null;
}

export interface GetBoxBulkDownloadLinkOperationDetails {
    $type: 'get_box_bulk_download_link';
    boxExternalId: string;
    boxName: string | null;
    folders: BulkDownloadItemDetail[];
    files: BulkDownloadItemDetail[];
    excludedFolders: BulkDownloadItemDetail[];
    excludedFiles: BulkDownloadItemDetail[];
    expiresInMinutes: number | null;
}

export interface SearchBoxOperationDetails {
    $type: 'search_box';
    boxExternalId: string;
    boxName: string | null;
    phrase: string;
    folderExternalId: string | null;
    folderName: string | null;
}

export interface CreateBoxFolderOperationDetails {
    $type: 'create_box_folder';
    boxExternalId: string;
    boxName: string | null;
    name: string;
    parentFolderExternalId: string | null;
    parentLocation: string | null;
}

export interface CreateBoxFileOperationDetails {
    $type: 'create_box_file';
    boxExternalId: string;
    boxName: string | null;
    name: string;
    folderExternalId: string | null;
    parentLocation: string | null;
    sizeInBytes: number;
    contentPreview: string;
    isPreviewTruncated: boolean;
}

export interface RenameBoxFileOperationDetails {
    $type: 'rename_box_file';
    boxExternalId: string;
    boxName: string | null;
    fileExternalId: string;
    folderExternalId: string | null;
    currentName: string | null;
    newName: string;
    path: string | null;
}

export interface RenameBoxFolderOperationDetails {
    $type: 'rename_box_folder';
    boxExternalId: string;
    boxName: string | null;
    folderExternalId: string;
    currentName: string | null;
    newName: string;
    path: string | null;
}

export interface MoveBoxItemsOperationDetails {
    $type: 'move_box_items';
    boxExternalId: string;
    boxName: string | null;
    destinationFolderExternalId: string | null;
    destinationName: string | null;
    destinationPath: string | null;
    folders: MoveItemDetail[];
    files: MoveItemDetail[];
}

export interface DeleteBoxItemsOperationDetails {
    $type: 'delete_box_items';
    boxExternalId: string;
    boxName: string | null;
    folders: BulkDeleteFolderDetail[];
    files: BulkDeleteFileDetail[];
}

export interface GetBoxOperationDetails {
    $type: 'get_box';
    workspaceExternalId: string;
    boxExternalId: string;
}

export interface CreateBoxOperationDetails {
    $type: 'create_box';
    workspaceExternalId: string;
    workspaceName: string | null;
    name: string;
    folderExternalId: string;
}

export interface UpdateBoxOperationDetails {
    $type: 'update_box';
    workspaceExternalId: string;
    boxExternalId: string;
    currentName: string | null;
    updateName: boolean;
    newName: string | null;
    updateIsEnabled: boolean;
    isEnabled: boolean | null;
    updateFolder: boolean;
    folderExternalId: string | null;
}

export interface DeleteBoxOperationDetails {
    $type: 'delete_box';
    workspaceExternalId: string;
    boxExternalId: string;
    boxName: string | null;
}

export interface ListBoxLinksOperationDetails {
    $type: 'list_box_links';
    workspaceExternalId: string;
    boxExternalId: string;
}

export interface CreateBoxLinkOperationDetails {
    $type: 'create_box_link';
    workspaceExternalId: string;
    boxExternalId: string;
    boxName: string | null;
    name: string;
}

export interface UpdateBoxLinkOperationDetails {
    $type: 'update_box_link';
    workspaceExternalId: string;
    boxLinkExternalId: string;
    currentName: string | null;
    updateName: boolean;
    newName: string | null;
    updateIsEnabled: boolean;
    isEnabled: boolean | null;
    updatePermissions: boolean;
    updateWidgetOrigins: boolean;
}

export interface DeleteBoxLinkOperationDetails {
    $type: 'delete_box_link';
    workspaceExternalId: string;
    boxLinkExternalId: string;
    boxLinkName: string | null;
}

export interface RegenerateBoxLinkAccessCodeOperationDetails {
    $type: 'regenerate_box_link_access_code';
    workspaceExternalId: string;
    boxLinkExternalId: string;
    boxLinkName: string | null;
}

export interface ListBoxMembersOperationDetails {
    $type: 'list_box_members';
    workspaceExternalId: string;
    boxExternalId: string;
}

export interface InviteBoxMembersOperationDetails {
    $type: 'invite_box_members';
    workspaceExternalId: string;
    boxExternalId: string;
    boxName: string | null;
    memberEmails: string[];
}

export interface UpdateBoxMemberPermissionsOperationDetails {
    $type: 'update_box_member_permissions';
    workspaceExternalId: string;
    boxExternalId: string;
    boxName: string | null;
    memberExternalId: string;
    memberEmail: string | null;
}

export interface RevokeBoxMemberOperationDetails {
    $type: 'revoke_box_member';
    workspaceExternalId: string;
    boxExternalId: string;
    boxName: string | null;
    memberExternalId: string;
    memberEmail: string | null;
}

export interface BulkDownloadItemDetail {
    externalId: string;
    name: string;
    path: string | null;
    folderExternalId?: string | null;
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

    public async grantBoxAccess(externalId: string, boxExternalId: string): Promise<void> {
        const call = this
            ._http
            .put(
                `/api/agents/${externalId}/boxes/${boxExternalId}`, {}, {
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

    public async getAgentBoxTools(externalId: string, boxExternalId: string): Promise<GetAgentBoxToolsResponse> {
        const call = this
            ._http
            .get<GetAgentBoxToolsResponse>(
                `/api/agents/${externalId}/boxes/${boxExternalId}/tools`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updateAgentBoxToolOverride(externalId: string, boxExternalId: string, toolName: string, request: UpdateAgentBoxToolOverrideRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/agents/${externalId}/boxes/${boxExternalId}/tools/${toolName}`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async resetAgentBoxToolOverride(externalId: string, boxExternalId: string, toolName: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/agents/${externalId}/boxes/${boxExternalId}/tools/${toolName}`, {
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
}
