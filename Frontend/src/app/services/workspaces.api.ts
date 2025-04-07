import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { FileType } from "./filte-type";

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
};

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
};

export interface GetWorkspaceBucketStatusResponse {
    isBucketCreated: boolean;
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
}