import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { QuickShareMode } from "./quick-shares.api";
import { ContentDisposition, GetFileDownloadLinkResponse, GetZipBulkDownloadLinkRequest, GetZipBulkDownloadLinkResponse } from "./folders-and-files.api";
import { ZipPreviewDetails } from "../files-explorer/file-inline-preview/file-inline-preview.component";
import { ZipEntry } from "./zip";
import { ProtoHttp } from "./protobuf-http.service";
import { getZipFileDetailsDtoProtobuf } from "../protobuf/zip-file-details-dto.protobuf";

const zipFileDetailsDtoProtobuf = getZipFileDetailsDtoProtobuf();

export interface GetQuickShareInfoResponse {
    name: string;
    mode: QuickShareMode;
    allowIndividualFileDownload: boolean;
    requiresPassword: boolean;
    isUnlocked: boolean;
    isExpired: boolean;
    isExhausted: boolean;
    isOwnerPreview: boolean;
    expiresAt: string | null;
    maxDownloads: number | null;
    downloadsCount: number;
}

export interface UnlockQuickShareRequest {
    password: string;
}

export interface QuickShareContentFolder {
    externalId: string;
    parentExternalId: string | null;
    name: string;
}

export interface QuickShareContentFile {
    externalId: string;
    folderExternalId: string | null;
    name: string;
    extension: string;
    sizeInBytes: number;
}

export interface GetQuickShareContentResponse {
    folders: QuickShareContentFolder[];
    files: QuickShareContentFile[];
    totalSizeInBytes: number;
}

export interface GetQuickShareBulkDownloadLinkRequest {
    selectedFolderExternalIds: string[];
    excludedFolderExternalIds: string[];
    selectedFileExternalIds: string[];
    excludedFileExternalIds: string[];
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
    constructor(
        private _http: HttpClient,
        private _protoHttp: ProtoHttp
    ) {
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

    public async getBulkDownloadLink(
        slug: string,
        token: string | null,
        request?: GetQuickShareBulkDownloadLinkRequest
    ): Promise<GetQuickShareBulkDownloadLinkResponse> {
        const call = this._http.post<GetQuickShareBulkDownloadLinkResponse>(
            `/api/quick-shares/${slug}/bulk-download-link`,
            request ?? {},
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

    public async getFilePreviewLink(
        slug: string,
        token: string | null,
        fileExternalId: string
    ): Promise<GetQuickShareFileDownloadLinkResponse> {
        const call = this._http.get<GetQuickShareFileDownloadLinkResponse>(
            `/api/quick-shares/${slug}/files/${fileExternalId}/preview-link`,
            { params: this.tokenParams(token) });

        return await firstValueFrom(call);
    }

    public getZipPreviewDetails(
        slug: string,
        token: string | null,
        fileExternalId: string
    ): Promise<ZipPreviewDetails> {
        // ProtoHttp.get doesn't take HttpParams — bake the token into the URL.
        return this._protoHttp.get<ZipPreviewDetails>({
            route: this.withToken(`/api/quick-shares/${slug}/files/${fileExternalId}/zip-details`, token),
            responseProtoType: zipFileDetailsDtoProtobuf
        });
    }

    public getZipContentPreviewLink(
        slug: string,
        token: string | null,
        fileExternalId: string,
        zipEntry: ZipEntry
    ): Promise<GetFileDownloadLinkResponse> {
        const call = this._http.post<GetFileDownloadLinkResponse>(
            `/api/quick-shares/${slug}/files/${fileExternalId}/zip-content-preview-link`,
            { item: zipEntry, contentDisposition: 'inline' },
            { params: this.tokenParams(token) });

        return firstValueFrom(call);
    }

    public getZipContentDownloadLink(
        slug: string,
        token: string | null,
        fileExternalId: string,
        zipEntry: ZipEntry,
        contentDisposition: ContentDisposition
    ): Promise<GetFileDownloadLinkResponse> {
        const call = this._http.post<GetFileDownloadLinkResponse>(
            `/api/quick-shares/${slug}/files/${fileExternalId}/zip-content-download-link`,
            { item: zipEntry, contentDisposition: contentDisposition },
            { params: this.tokenParams(token) });

        return firstValueFrom(call);
    }

    public getZipBulkDownloadLink(
        slug: string,
        token: string | null,
        fileExternalId: string,
        request: GetZipBulkDownloadLinkRequest
    ): Promise<GetZipBulkDownloadLinkResponse> {
        const call = this._http.post<GetZipBulkDownloadLinkResponse>(
            `/api/quick-shares/${slug}/files/${fileExternalId}/zip-bulk-download-link`,
            request,
            { params: this.tokenParams(token) });

        return firstValueFrom(call);
    }

    private tokenParams(token: string | null): HttpParams {
        return token ? new HttpParams().set('token', token) : new HttpParams();
    }

    private withToken(route: string, token: string | null): string {
        return token ? `${route}?token=${encodeURIComponent(token)}` : route;
    }
}
