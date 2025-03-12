import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface UpdateBoxLinkNameRequest {
    name: string;
}

export interface UpdateBoxLinkBoxRequest {
    boxExternalId: string;
}

export interface UpdateBoxLinkIsEnabledRequest {
    isEnabled: boolean;
}

export interface UpdateBoxLinkPermissionsRequest {
    allowDownload: boolean;
    allowUpload: boolean;
    allowList: boolean;
    allowDeleteFile: boolean;
    allowDeleteFolder: boolean;
    allowRenameFile: boolean;
    allowRenameFolder: boolean;
    allowMoveItems: boolean;
    allowCreateFolder: boolean;
}

export interface RegenerateAccessCodeResponse {
    accessCode: string;
}

@Injectable({
    providedIn: 'root'
})
export class BoxLinksApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async updateBoxLinkName(workspaceExternalId: string, externalId: string, request: UpdateBoxLinkNameRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(
                `/api/workspaces/${workspaceExternalId}/box-links/${externalId}/name`,
                request);

        return await firstValueFrom(call);
    }

    public async updateBoxLinkBox(workspaceExternalId: string, externalId: string, request: UpdateBoxLinkBoxRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(
                `/api/workspaces/${workspaceExternalId}/box-links/${externalId}/box`,
                request);

        return await firstValueFrom(call);
    }

    public async updateBoxLinkIsEnabled(workspaceExternalId: string, externalId: string, request: UpdateBoxLinkIsEnabledRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(
                `/api/workspaces/${workspaceExternalId}/box-links/${externalId}/is-enabled`,
                request);

        return await firstValueFrom(call);
    }

    public async updateBoxLinkPermissions(workspaceExternalId: string, externalId: string, request: UpdateBoxLinkPermissionsRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(
                `/api/workspaces/${workspaceExternalId}/box-links/${externalId}/permissions`,
                request);

        return await firstValueFrom(call);
    }

    public async regenerateAccessCode(workspaceExternalId: string, externalId: string): Promise<RegenerateAccessCodeResponse> {
        const call = this
            ._http
            .patch<RegenerateAccessCodeResponse>(
                `/api/workspaces/${workspaceExternalId}/box-links/${externalId}/regenerate-access-code`,
                {});

        return await firstValueFrom(call);
    }

    public async deleteBoxLink(workspaceExternalId: string, externalId: string): Promise<void> {
        const call = this
            ._http
            .delete<void>(
                `/api/workspaces/${workspaceExternalId}/box-links/${externalId}`);

        return await firstValueFrom(call);
    }
}