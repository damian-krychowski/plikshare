import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { FileType } from "./file-type";
import { AppStorageEncryptionType } from "./storages.api";
import { ThumbnailVariant } from "./folders-and-files.api";

export type GetWorkspaceDetailsResponse = WorkspaceDto;

export type WorkspaceDto = {
    externalId: string;
    name: string;
    currentSizeInBytes: number;
    maxSizeInBytes: number | null;
    currentTeamMembersCount: number;
    currentBoxesTeamMembersCount: number;
    maxTeamMembers: number | null;
    owner: {
        externalId: string;
        email: string;
    };
    pendingUploadsCount: number;
    permissions: {
        allowShare: boolean;
    };
    integrations: WorkspaceIntegrations;
    storageEncryptionType: AppStorageEncryptionType;
    trashPolicy: TrashPolicyDto;
    mediaProcessingPolicy: MediaProcessingPolicyDto;
};

export interface TrashPolicyDto {
    enabled: boolean;
    retentionDays: number | null;
}

export interface UpdateWorkspaceTrashPolicyRequest {
    enabled: boolean;
    retentionDays: number | null;
}

export interface MediaProcessingPolicyDto {
    imageDimensions: ImageDimensionsPolicyDto;
    thumbnails: ThumbnailsPolicyDto;
}

export interface ImageDimensionsPolicyDto {
    extractOnUpload: boolean;
}

export interface ThumbnailsPolicyDto {
    generateOnUpload: boolean;
    variants: ThumbnailVariant[];
}

export interface UpdateWorkspaceThumbnailsPolicyRequest {
    generateOnUpload: boolean;
    variants: ThumbnailVariant[];
}

export interface UpdateWorkspaceThumbnailsPolicyResponse {
    batchId: string | null;
    totalFiles: number;
}

export interface ThumbnailsBackfillStatus {
    batchId: string | null;
    total: number;
    completed: number;
    failed: number;
    pending: number;
}

export interface ThumbnailsBackfillCount {
    fileCount: number;
}

export interface UpdateWorkspaceImageDimensionsPolicyRequest {
    extractOnUpload: boolean;
}

export interface UpdateWorkspaceImageDimensionsPolicyResponse {
    batchId: string | null;
    totalFiles: number;
}

export interface ImageDimensionsBackfillStatus {
    batchId: string | null;
    total: number;
    completed: number;
    failed: number;
    pending: number;
}

export interface BatchProgress {
    total: number;
    completed: number;
    failed: number;
    pending: number;
}

export interface ImageDimensionsBackfillCount {
    fileCount: number;
}

export interface WorkspaceIntegrations {
    textract: TextractIntegration | null;
    chatGpt: ChatGptIntegration[];
}

export interface TextractIntegration {
    externalId: string;
    name: string;
}

export interface ChatGptIntegration {
    externalId: string;
    name: string;
    models: ChatGptMode[];
    defaultModel: string;
}

export interface ChatGptMode {
    alias: string;
    supportedFileTypes: FileType[];
    maxIncludeSizeInBytes: number;
}

export interface UpdateWorkspaceNameRequest {
    name: string;
}

export interface UpdateWorkspaceOwnerRequest {
    newOwnerExternalId: string;
}

export interface UpdateWorkspaceMaxSizeRequest {
    maxSizeInBytes: number | null;
}

export interface UpdateWorkspaceMaxTeamMembersRequest {
    maxTeamMembers: number | null;
}

export interface CreateWorkspaceRequest {
    storageExternalId: string;
    name: string;
}

export interface CreateWorkspaceResponse {
    externalId: string;
    maxSizeInBytes: number | null;
}

export interface CreateWorkspaceMemberInvitationRequest {
    memberEmails: string[];
    ephemeralDekLifetimeHours: number | null;
}

export interface CreateWorkspaceMemberInvitationResponse {
    members: {
        email: string;
        externalId: string;
    }[];
}

export interface GetWorkspaceMembersList {
    items: {
        memberExternalId: string;
        memberEmail: string;
        inviterEmail: string;
        wasInvitationAccepted: boolean;
        isPendingKeyGrant: boolean;
        permissions: {
            allowShare: boolean;
        }
    }[];
}

export interface UpdateWorkspaceMemberPermissionsRequest {
    allowShare: boolean;
}

export type AcceptWorkspaceInvitationResponse = {
    workspaceCurrentSizeInBytes: number;
    workspaceMaxSizeInBytes: number | null;
    storageEncryptionType: AppStorageEncryptionType;
    isPendingKeyGrant: boolean;
};

export interface GetWorkspaceBucketStatusResponse {
    isBucketCreated: boolean;
}

export interface AdminWorkspaceListItem {
    externalId: string;
    name: string;
    currentSizeInBytes: number;
    maxSizeInBytes: number | null;
    isBucketCreated: boolean;
    storage: {
        externalId: string;
        name: string;
        encryptionType: AppStorageEncryptionType;
    };
    owner: {
        externalId: string;
        email: string;
    };
}

export interface GetAllWorkspacesAdminResponse {
    items: AdminWorkspaceListItem[];
}

export interface AdminAddWorkspaceMemberRequest {
    memberExternalId: string;
    allowShare: boolean;
}

export interface AdminAddWorkspaceMemberResponse {
    email: string;
    externalId: string;
}

@Injectable({
    providedIn: 'root'
})
export class WorkspacesApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async deleteWorkspace(workspaceExternalId: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/workspaces/${workspaceExternalId}`);

        await firstValueFrom(call);
    }

    public async updateMemberPermissions(workspaceExternalId: string, memberExternalId: string, request: UpdateWorkspaceMemberPermissionsRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${workspaceExternalId}/members/${memberExternalId}/permissions`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async revokeWorkspaceMember(workspaceExternalId: string, memberExternalId: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/workspaces/${workspaceExternalId}/members/${memberExternalId}`);

        await firstValueFrom(call);
    }

    public async grantWorkspaceMemberEncryptionAccess(workspaceExternalId: string, memberExternalId: string): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/workspaces/${workspaceExternalId}/members/${memberExternalId}/grant-encryption-access`, {});

        await firstValueFrom(call);
    }

    public async getWorkspaceMemberList(workspaceExternalId: string): Promise<GetWorkspaceMembersList> {
        const call = this
            ._http
            .get<GetWorkspaceMembersList>(`/api/workspaces/${workspaceExternalId}/members`);

        return await firstValueFrom(call);
    }

    public async getWorkspaceBucketStatus(workspaceExternalId: string): Promise<GetWorkspaceBucketStatusResponse> {
        const call = this
            ._http
            .get<GetWorkspaceBucketStatusResponse>(`/api/workspaces/${workspaceExternalId}/is-bucket-created`);

        return await firstValueFrom(call);
    }

    public async leaveWorkspace(externalId: string): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/workspaces/${externalId}/members/leave`, {});

        await firstValueFrom(call);
    }

    public async getWorkspace(externalId: string): Promise<GetWorkspaceDetailsResponse> {
        const call = this
            ._http
            .get<GetWorkspaceDetailsResponse>(
                `/api/workspaces/${externalId}`);

        return await firstValueFrom(call);
    }

    public async updateName(externalId: string, request: UpdateWorkspaceNameRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${externalId}/name`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateOwner(externalId: string, request: UpdateWorkspaceOwnerRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${externalId}/owner`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateMaxSize(externalId: string, request: UpdateWorkspaceMaxSizeRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${externalId}/max-size`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateMaxTeamMembers(externalId: string, request: UpdateWorkspaceMaxTeamMembersRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${externalId}/max-team-members`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateTrashPolicy(externalId: string, request: UpdateWorkspaceTrashPolicyRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${externalId}/trash-policy`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateImageDimensionsPolicy(
        externalId: string,
        request: UpdateWorkspaceImageDimensionsPolicyRequest
    ): Promise<UpdateWorkspaceImageDimensionsPolicyResponse> {
        const call = this
            ._http
            .patch<UpdateWorkspaceImageDimensionsPolicyResponse>(
                `/api/workspaces/${externalId}/media-processing-policy/image-dimensions`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updateThumbnailsPolicy(
        externalId: string,
        request: UpdateWorkspaceThumbnailsPolicyRequest
    ): Promise<UpdateWorkspaceThumbnailsPolicyResponse> {
        const call = this
            ._http
            .patch<UpdateWorkspaceThumbnailsPolicyResponse>(
                `/api/workspaces/${externalId}/media-processing-policy/thumbnails`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async getThumbnailsBackfillStatus(
        externalId: string
    ): Promise<ThumbnailsBackfillStatus> {
        const call = this
            ._http
            .get<ThumbnailsBackfillStatus>(
                `/api/workspaces/${externalId}/media/thumbnails/backfill`);

        return await firstValueFrom(call);
    }

    // How many existing images are missing at least one of the given variants — drives the
    // "generate for N images" confirmation dialog before the policy is turned on.
    public async getThumbnailsBackfillCount(
        externalId: string,
        variants: ThumbnailVariant[]
    ): Promise<ThumbnailsBackfillCount> {
        const query = variants
            .map(variant => `variants=${encodeURIComponent(variant)}`)
            .join('&');

        const call = this
            ._http
            .get<ThumbnailsBackfillCount>(
                `/api/workspaces/${externalId}/media/thumbnails/backfill/count?${query}`);

        return await firstValueFrom(call);
    }

    // SSE: server pushes thumbnail-batch status (total/completed/failed/pending) on every change
    // and closes once Pending hits 0. Returns an unsubscribe that closes the connection.
    public subscribeThumbnailsBatch(
        externalId: string,
        batchId: string,
        onProgress: (progress: BatchProgress) => void
    ): () => void {
        const eventSource = new EventSource(
            `/api/workspaces/${externalId}/media/thumbnails/batches/${batchId}/events`,
            { withCredentials: true }
        );

        eventSource.onmessage = (event) => {
            try {
                onProgress(JSON.parse(event.data));
            } catch (err) {
                console.error('Failed to parse thumbnails batch event:', err);
            }
        };

        eventSource.onerror = () => {
            // EventSource reconnects on its own; the caller closes us once the batch is terminal.
        };

        return () => eventSource.close();
    }

    // The backfill batchId lives on the queue jobs (server-side), so anyone opening the workspace
    // settings — and the same user after a reload — discovers an in-progress backfill here.
    // batchId is null when nothing is running.
    public async getImageDimensionsBackfillStatus(
        externalId: string
    ): Promise<ImageDimensionsBackfillStatus> {
        const call = this
            ._http
            .get<ImageDimensionsBackfillStatus>(
                `/api/workspaces/${externalId}/media/image-dimensions/backfill`);

        return await firstValueFrom(call);
    }

    // How many existing images a backfill would process — drives the "extract for N images"
    // confirmation dialog before the policy is turned on.
    public async getImageDimensionsBackfillCount(
        externalId: string
    ): Promise<ImageDimensionsBackfillCount> {
        const call = this
            ._http
            .get<ImageDimensionsBackfillCount>(
                `/api/workspaces/${externalId}/media/image-dimensions/backfill/count`);

        return await firstValueFrom(call);
    }

    // SSE: server pushes {total, completed, failed, pending} on every batch change and closes once
    // Pending hits 0. Returns an unsubscribe that closes the connection.
    public subscribeImageDimensionsBatch(
        externalId: string,
        batchId: string,
        onProgress: (progress: BatchProgress) => void
    ): () => void {
        const eventSource = new EventSource(
            `/api/workspaces/${externalId}/media/image-dimensions/batches/${batchId}/events`,
            { withCredentials: true }
        );

        eventSource.onmessage = (event) => {
            try {
                onProgress(JSON.parse(event.data));
            } catch (err) {
                console.error('Failed to parse image-dimensions batch event:', err);
            }
        };

        eventSource.onerror = () => {
            // EventSource reconnects on its own; the caller closes us once the batch is terminal.
        };

        return () => eventSource.close();
    }

    public async createWorkspace(request: CreateWorkspaceRequest): Promise<CreateWorkspaceResponse> {
        const call = this
            ._http
            .post<CreateWorkspaceResponse>(
                `/api/workspaces`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async createMemberInvitation(workspaceExternalId: string, request: CreateWorkspaceMemberInvitationRequest): Promise<CreateWorkspaceMemberInvitationResponse> {
        const call = this
            ._http
            .post<CreateWorkspaceMemberInvitationResponse>(
                `/api/workspaces/${workspaceExternalId}/members`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async acceptWorkspaceInvitation(workspaceExternalId: string): Promise<AcceptWorkspaceInvitationResponse> {
        const call = this
            ._http
            .post<AcceptWorkspaceInvitationResponse>(
                `/api/workspaces/${workspaceExternalId}/accept-invitation`, {});

        return await firstValueFrom(call);
    }

    public async rejectWorkspaceInvitation(workspaceExternalId: string): Promise<void> {
        const call = this
            ._http
            .post<void>(
                `/api/workspaces/${workspaceExternalId}/reject-invitation`, {});

        return await firstValueFrom(call);
    }

    public async getAllWorkspacesAdmin(excludeMemberOrOwnerExternalId?: string): Promise<GetAllWorkspacesAdminResponse> {
        let url = `/api/workspaces/admin-list-all`;

        if (excludeMemberOrOwnerExternalId) {
            url += `?excludeMemberOrOwnerExternalId=${encodeURIComponent(excludeMemberOrOwnerExternalId)}`;
        }

        const call = this
            ._http
            .get<GetAllWorkspacesAdminResponse>(url);

        return await firstValueFrom(call);
    }

    public async adminAddWorkspaceMember(workspaceExternalId: string, request: AdminAddWorkspaceMemberRequest): Promise<AdminAddWorkspaceMemberResponse> {
        const call = this
            ._http
            .post<AdminAddWorkspaceMemberResponse>(
                `/api/workspaces/${workspaceExternalId}/members/assign`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }
}