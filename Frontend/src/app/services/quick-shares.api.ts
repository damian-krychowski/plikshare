import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export type QuickShareMode = 'browser' | 'direct';

export interface QuickShareItemsDto {
    selectedFiles: string[];
    selectedFolders: string[];
    excludedFiles: string[];
    excludedFolders: string[];
}

export interface CreateQuickShareRequest {
    name: string;
    customSlug: string | null;
    selectedFiles: string[];
    selectedFolders: string[];
    excludedFiles: string[];
    excludedFolders: string[];
    mode: QuickShareMode;
    allowIndividualFileDownload: boolean;
    expiresAt: string | null;
    password: string | null;
    maxDownloads: number | null;
}

export interface CreateQuickShareResponse {
    externalId: string;
    slug: string;
    url: string;
}

export interface GetQuickShareResponse {
    externalId: string;
    name: string;
    creatorExternalId: string;
    createdAt: string;
    expiresAt: string | null;
    hasPassword: boolean;
    maxDownloads: number | null;
    downloadsCount: number;
    mode: QuickShareMode;
    allowIndividualFileDownload: boolean;
    lastAccessedAt: string | null;
    slug: string;
    hasSecret: boolean;
    url: string | null;
    items: QuickShareItemsDto;
}

export interface GetQuickSharesItem {
    externalId: string;
    name: string;
    createdAt: string;
    expiresAt: string | null;
    hasPassword: boolean;
    maxDownloads: number | null;
    downloadsCount: number;
    mode: QuickShareMode;
    allowIndividualFileDownload: boolean;
    lastAccessedAt: string | null;
    slug: string;
    hasSecret: boolean;
    url: string | null;
    selectedFilesCount: number;
    selectedFoldersCount: number;
    excludedFilesCount: number;
    excludedFoldersCount: number;
}

export interface GetQuickSharesResponse {
    items: GetQuickSharesItem[];
}

export interface UpdateQuickShareNameRequest {
    name: string;
}

export interface UpdateQuickShareSlugRequest {
    slug: string;
}

export interface UpdateQuickShareExpirationRequest {
    expiresAt: string | null;
}

export interface UpdateQuickSharePasswordRequest {
    password: string | null;
}

export interface UpdateQuickShareMaxDownloadsRequest {
    maxDownloads: number | null;
}

export interface UpdateQuickShareModeRequest {
    mode: QuickShareMode;
    allowIndividualFileDownload: boolean;
}

export interface UpdateQuickShareItemsRequest {
    selectedFiles: string[];
    selectedFolders: string[];
    excludedFiles: string[];
    excludedFolders: string[];
}

@Injectable({
    providedIn: 'root'
})
export class QuickSharesApi {
    constructor(private _http: HttpClient) {
    }

    public async createQuickShare(
        workspaceExternalId: string,
        request: CreateQuickShareRequest
    ): Promise<CreateQuickShareResponse> {
        const call = this._http.post<CreateQuickShareResponse>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/`,
            request);

        return await firstValueFrom(call);
    }

    public async getQuickShares(
        workspaceExternalId: string
    ): Promise<GetQuickSharesResponse> {
        const call = this._http.get<GetQuickSharesResponse>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/`);

        return await firstValueFrom(call);
    }

    public async getQuickShare(
        workspaceExternalId: string,
        externalId: string
    ): Promise<GetQuickShareResponse> {
        const call = this._http.get<GetQuickShareResponse>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/${externalId}`);

        return await firstValueFrom(call);
    }

    public async deleteQuickShare(
        workspaceExternalId: string,
        externalId: string
    ): Promise<void> {
        const call = this._http.delete<void>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/${externalId}`);

        return await firstValueFrom(call);
    }

    public async updateQuickShareName(
        workspaceExternalId: string,
        externalId: string,
        request: UpdateQuickShareNameRequest
    ): Promise<void> {
        const call = this._http.patch<void>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/${externalId}/name`,
            request);

        return await firstValueFrom(call);
    }

    public async updateQuickShareSlug(
        workspaceExternalId: string,
        externalId: string,
        request: UpdateQuickShareSlugRequest
    ): Promise<void> {
        const call = this._http.patch<void>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/${externalId}/slug`,
            request);

        return await firstValueFrom(call);
    }

    public async updateQuickShareExpiration(
        workspaceExternalId: string,
        externalId: string,
        request: UpdateQuickShareExpirationRequest
    ): Promise<void> {
        const call = this._http.patch<void>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/${externalId}/expiration`,
            request);

        return await firstValueFrom(call);
    }

    public async updateQuickSharePassword(
        workspaceExternalId: string,
        externalId: string,
        request: UpdateQuickSharePasswordRequest
    ): Promise<void> {
        const call = this._http.patch<void>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/${externalId}/password`,
            request);

        return await firstValueFrom(call);
    }

    public async updateQuickShareMaxDownloads(
        workspaceExternalId: string,
        externalId: string,
        request: UpdateQuickShareMaxDownloadsRequest
    ): Promise<void> {
        const call = this._http.patch<void>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/${externalId}/max-downloads`,
            request);

        return await firstValueFrom(call);
    }

    public async updateQuickShareMode(
        workspaceExternalId: string,
        externalId: string,
        request: UpdateQuickShareModeRequest
    ): Promise<void> {
        const call = this._http.patch<void>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/${externalId}/mode`,
            request);

        return await firstValueFrom(call);
    }

    public async updateQuickShareItems(
        workspaceExternalId: string,
        externalId: string,
        request: UpdateQuickShareItemsRequest
    ): Promise<void> {
        const call = this._http.patch<void>(
            `/api/workspaces/${workspaceExternalId}/quick-shares/${externalId}/items`,
            request);

        return await firstValueFrom(call);
    }
}
