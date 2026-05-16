import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { QuickShareMode } from "./quick-shares.api";
import { ContentDisposition } from "./folders-and-files.api";

export interface GetQuickShareInfoResponse {
    name: string;
    mode: QuickShareMode;
    allowIndividualFileDownload: boolean;
    requiresPassword: boolean;
    isUnlocked: boolean;
    isExpired: boolean;
    isExhausted: boolean;
    expiresAt: string | null;
    maxDownloads: number | null;
    downloadsCount: number;
}

export interface UnlockQuickShareRequest {
    password: string;
}

export interface QuickShareContentFile {
    externalId: string;
    filePath: string;
    name: string;
    extension: string;
    sizeInBytes: number;
}

export interface GetQuickShareContentResponse {
    files: QuickShareContentFile[];
    totalSizeInBytes: number;
}

export interface GetQuickShareBulkDownloadLinkResponse {
    preSignedUrl: string;
}

export interface GetQuickShareFileDownloadLinkResponse {
    downloadPreSignedUrl: string;
}

@Injectable({
    providedIn: 'root'
})
export class QuickShareExternalAccessApi {
    constructor(private _http: HttpClient) {
    }

    public async getInfo(slug: string, token: string | null): Promise<GetQuickShareInfoResponse> {
        const call = this._http.get<GetQuickShareInfoResponse>(
            `/api/quick-shares/${slug}/info`,
            { params: this.tokenParams(token) });

        return await firstValueFrom(call);
    }

    public async unlock(slug: string, token: string | null, request: UnlockQuickShareRequest): Promise<void> {
        const call = this._http.post<void>(
            `/api/quick-shares/${slug}/unlock`,
            request,
            { params: this.tokenParams(token) });

        return await firstValueFrom(call);
    }

    public async getContent(slug: string, token: string | null): Promise<GetQuickShareContentResponse> {
        const call = this._http.get<GetQuickShareContentResponse>(
            `/api/quick-shares/${slug}/content`,
            { params: this.tokenParams(token) });

        return await firstValueFrom(call);
    }

    public async getBulkDownloadLink(slug: string, token: string | null): Promise<GetQuickShareBulkDownloadLinkResponse> {
        const call = this._http.post<GetQuickShareBulkDownloadLinkResponse>(
            `/api/quick-shares/${slug}/bulk-download-link`,
            {},
            { params: this.tokenParams(token) });

        return await firstValueFrom(call);
    }

    public async getFileDownloadLink(
        slug: string,
        token: string | null,
        fileExternalId: string,
        contentDisposition: ContentDisposition
    ): Promise<GetQuickShareFileDownloadLinkResponse> {
        let params = new HttpParams().set('contentDisposition', contentDisposition);
        if (token) params = params.set('token', token);

        const call = this._http.get<GetQuickShareFileDownloadLinkResponse>(
            `/api/quick-shares/${slug}/files/${fileExternalId}/download-link`,
            { params });

        return await firstValueFrom(call);
    }

    private tokenParams(token: string | null): HttpParams {
        return token ? new HttpParams().set('token', token) : new HttpParams();
    }
}
