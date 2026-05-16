import { HttpClient } from "@angular/common/http";
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

    public async getInfo(accessCode: string): Promise<GetQuickShareInfoResponse> {
        const call = this._http.get<GetQuickShareInfoResponse>(
            `/api/quick-shares/${accessCode}/info`);

        return await firstValueFrom(call);
    }

    public async unlock(accessCode: string, request: UnlockQuickShareRequest): Promise<void> {
        const call = this._http.post<void>(
            `/api/quick-shares/${accessCode}/unlock`,
            request);

        return await firstValueFrom(call);
    }

    public async getContent(accessCode: string): Promise<GetQuickShareContentResponse> {
        const call = this._http.get<GetQuickShareContentResponse>(
            `/api/quick-shares/${accessCode}/content`);

        return await firstValueFrom(call);
    }

    public async getBulkDownloadLink(accessCode: string): Promise<GetQuickShareBulkDownloadLinkResponse> {
        const call = this._http.post<GetQuickShareBulkDownloadLinkResponse>(
            `/api/quick-shares/${accessCode}/bulk-download-link`,
            {});

        return await firstValueFrom(call);
    }

    public async getFileDownloadLink(
        accessCode: string,
        fileExternalId: string,
        contentDisposition: ContentDisposition
    ): Promise<GetQuickShareFileDownloadLinkResponse> {
        const call = this._http.get<GetQuickShareFileDownloadLinkResponse>(
            `/api/quick-shares/${accessCode}/files/${fileExternalId}/download-link`,
            {
                params: { contentDisposition }
            });

        return await firstValueFrom(call);
    }
}
