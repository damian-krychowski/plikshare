import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { BoxMoveItemsToFolderRequest, BoxUpdateFolderNameRequest, BoxUpdateFileNameRequest, GetBoxDetailsAndFolderResponse, BoxCompleteFilePartUploadRequest, BoxInitiateFilePartUploadResponse, BoxCompleteFileUploadResponse, BoxGetUploadListResponse, GetBoxHtmlResponse, BoxGetFileUploadDetailsResponse } from "../contracts/external-access.contracts";
import { ZipPreviewDetails } from "../../files-explorer/file-inline-preview/file-inline-preview.component";
import { BulkCreateFolderRequest, BulkCreateFolderResponse, BulkDeleteResponse, ContentDisposition, CountSelectedItemsRequest, CountSelectedItemsResponse, CreateFolderRequest, CreateFolderResponse, GetBulkDownloadLinkRequest, GetBulkDownloadLinkResponse, GetFileDownloadLinkResponse, GetFolderResponse, SearchFilesTreeRequest, SearchFilesTreeResponse } from "../../services/folders-and-files.api";
import { ZipEntry } from "../../services/zip";
import { BulkInitiateFileUploadRequest, BulkInitiateFileUploadResponse, BulkInitiateFileUploadResponseRaw, deserializeBulkUploadResponse, InitiateFileUploadRequest, InitiateFileUploadResponse } from "../../services/uploads.api";
import { getZipFileDetailsDtoProtobuf } from "../../protobuf/zip-file-details-dto.protobuf";
import { ProtoHttp } from "../../services/protobuf-http.service";
import { getFolderContentDtoProtobuf } from "../../protobuf/folder-content-dto.protobuf";
import { getBoxDetailsAndContentResponseDtoProtobuf } from "../../protobuf/box-details-and-folder-content-dto.protobuf";
import { getBulkInitiateFileUploadRequestDtoProtobuf } from "../../protobuf/bulk-initiate-file-upload-request-dto.protobuf";
import { getBulkInitiateFileUploadResponseDtoProtobuf } from "../../protobuf/bulk-initiate-file-upload-response-dto.protobuf";
import { getBulkCreateFolderRequestDtoProtobuf } from "../../protobuf/bulk-create-folder-request-dto.protobuf";
import { getBulkCreateFolderResponseDtoProtobuf } from "../../protobuf/bulk-create-folder-response-dto.protobuf";
import { getSearchFilesTreeResponseDtoProtobuf } from "../../protobuf/search-files-tree-response-dto.protobuf";
import { BOX_LINK_TOKEN_HEADER, BoxLinkTokenService } from "../../services/box-link-token.service";

const zipFileDetailsDtoProtobuf = getZipFileDetailsDtoProtobuf();
const folderContentDtoProtobuf = getFolderContentDtoProtobuf();
const boxDetailsAndFolderContentDtoProtobuf = getBoxDetailsAndContentResponseDtoProtobuf();
const bulkInitiateFileUploadRequestDtoProtobuf = getBulkInitiateFileUploadRequestDtoProtobuf();
const bulkInitiateFileUploadResponseDtoProtobuf = getBulkInitiateFileUploadResponseDtoProtobuf();
const bulkCreateFolderRequestDtoProtobuf = getBulkCreateFolderRequestDtoProtobuf();
const bulkCreateFolderResponseDtoProtobuf = getBulkCreateFolderResponseDtoProtobuf();
const searchFilesTreeResponseDtoProtobuf = getSearchFilesTreeResponseDtoProtobuf();

@Injectable({
    providedIn: 'root'
})
export class AccessCodesApi {
    constructor(
        private _boxLinkTokenService: BoxLinkTokenService,
        private _http: HttpClient,
        private _protoHttp: ProtoHttp) {
    }

    public async startSession(): Promise<string> {
        const call = this
            ._http
            .post(`/api/access-codes/start-session`, {}, {
                observe: 'response' 
            });

        const response = await firstValueFrom(call);
        const token = response.headers.get(BOX_LINK_TOKEN_HEADER);
        
        if (token) {
            this._boxLinkTokenService.set(token);
        }

        return token!;
    }

    private getHeaders(): HttpHeaders {
        let headers = new HttpHeaders({
            'Content-Type': 'application/json'
        });
        
        const token = this._boxLinkTokenService.get();

        if (token) {
            headers = headers.set(BOX_LINK_TOKEN_HEADER, token);
        }
        
        return headers;
    }

    public async moveItems(accessCode: string, request: BoxMoveItemsToFolderRequest) {
        const call = this
            ._http
            .patch(
                `/api/access-codes/${accessCode}/folders/move-items`, request, {
                    headers: this.getHeaders()
                });

        await firstValueFrom(call);
    }

    public createFolder(accessCode: string, request: CreateFolderRequest): Promise<CreateFolderResponse> {
        const call = this
            ._http
            .post<CreateFolderResponse>(`/api/access-codes/${accessCode}/folders`, request, {
                headers: this.getHeaders()
            });

        return firstValueFrom(call);
    }

    public bulkCreateFolders(accessCode: string, request: BulkCreateFolderRequest): Promise<BulkCreateFolderResponse> {
        return this._protoHttp.post<BulkCreateFolderRequest,BulkCreateFolderResponse>({
            route: `/api/access-codes/${accessCode}/folders/bulk`,
            request: request,
            requestProtoType: bulkCreateFolderRequestDtoProtobuf,
            responseProtoType: bulkCreateFolderResponseDtoProtobuf,
            boxLinkToken: this._boxLinkTokenService.get()
        });
    }

    public async bulkDelete(req: {
        accessCode: string, 
        fileExternalIds: string[], 
        folderExternalIds: string[],
        fileUploadExternalIds: string[]
    }): Promise<BulkDeleteResponse> {
        const call = this
            ._http
            .post<BulkDeleteResponse>(`/api/access-codes/${req.accessCode}/bulk-delete`, {
                fileExternalIds: req.fileExternalIds,
                folderExternalIds: req.folderExternalIds,
                fileUploadExternalIds: req.fileUploadExternalIds
            }, {
                headers: this.getHeaders()
            });

        return await firstValueFrom(call);
    }

    public async updateFolderName(accessCode: string, folderExternalId: string, request: BoxUpdateFolderNameRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(`/api/access-codes/${accessCode}/folders/${folderExternalId}/name`, request, {
                headers: this.getHeaders()
            });

        await firstValueFrom(call);
    }

    public async updateFileName(accessCode: string, fileExternalId: string, request: BoxUpdateFileNameRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(`/api/access-codes/${accessCode}/files/${fileExternalId}/name`, request, {
                headers: this.getHeaders()
            });

        await firstValueFrom(call);
    }

    public getDownloadLink(accessCode: string, fileExternalId: string, contentDisposition: ContentDisposition): Promise<GetFileDownloadLinkResponse> {
        const call = this
            ._http
            .get<GetFileDownloadLinkResponse>(`/api/access-codes/${accessCode}/files/${fileExternalId}/download-link`, {
                params: {
                    contentDisposition: contentDisposition
                },
                headers: this.getHeaders()
            });

        return firstValueFrom(call);
    }

    public getBulkDownloadLink(accessCode: string, request: GetBulkDownloadLinkRequest): Promise<GetBulkDownloadLinkResponse> {
        const call = this
            ._http
            .post<GetBulkDownloadLinkResponse>(`/api/access-codes/${accessCode}/files/bulk-download-link`, request, {
                headers: this.getHeaders()
            });

        return firstValueFrom(call);
    }

    public getDetailsAndContent(accessCode: string, folderExternalId: string | null): Promise<GetBoxDetailsAndFolderResponse> {
        return this._protoHttp.get<GetBoxDetailsAndFolderResponse>({
            route: `/api/access-codes/${accessCode}/${folderExternalId ?? ''}`,
            responseProtoType: boxDetailsAndFolderContentDtoProtobuf,
            boxLinkToken: this._boxLinkTokenService.get()
        });
    }

    public getHtml(accessCode: string): Promise<GetBoxHtmlResponse> {
        const call = this
            ._http
            .get<GetBoxHtmlResponse>(
                `/api/access-codes/${accessCode}/html`, {
                    headers: this.getHeaders()
                });

        return firstValueFrom(call);
    }

    public getContent(accessCode: string, folderExternalId: string | null): Promise<GetFolderResponse> {
        return this._protoHttp.get<GetFolderResponse>({
            route: `/api/access-codes/${accessCode}/content/${folderExternalId ?? ''}`,
            responseProtoType: folderContentDtoProtobuf,
            boxLinkToken: this._boxLinkTokenService.get()
        });
    }

    public completePartUpload(accessCode: string, externalId: string, partNumber: number, request: BoxCompleteFilePartUploadRequest): Promise<void> {
        const call = this
            ._http
            .post<void>(
                `/api/access-codes/${accessCode}/uploads/${externalId}/parts/${partNumber}/complete`, request, {
                    headers: this.getHeaders()
                });

        return firstValueFrom(call);
    }

    public initiatePartUpload(accessCode: string, externalId: string, partNumber: number): Promise<BoxInitiateFilePartUploadResponse> {
        const call = this
            ._http
            .post<BoxInitiateFilePartUploadResponse>(
                `/api/access-codes/${accessCode}/uploads/${externalId}/parts/${partNumber}/initiate`, null, {
                    headers: this.getHeaders()
                });

        return firstValueFrom(call);
    }

    public completeUpload(accessCode: string, externalId: string): Promise<BoxCompleteFileUploadResponse> {
        const call = this
            ._http
            .post<BoxCompleteFileUploadResponse>(
                `/api/access-codes/${accessCode}/uploads/${externalId}/complete`, null, {
                    headers: this.getHeaders()
                });

        return firstValueFrom(call);
    }

    public initiateUpload(accessCode: string, request: InitiateFileUploadRequest): Promise<InitiateFileUploadResponse> {
        const call = this
            ._http
            .post<InitiateFileUploadResponse>(
                `/api/access-codes/${accessCode}/uploads/initiate`, request, {
                    headers: this.getHeaders()
                });

        return firstValueFrom(call);
    }

    public async bulkInitiateUpload(accessCode: string, request: BulkInitiateFileUploadRequest): Promise<BulkInitiateFileUploadResponse> {
        const response = await this._protoHttp.post<BulkInitiateFileUploadRequest, BulkInitiateFileUploadResponseRaw>({
                route: `/api/access-codes/${accessCode}/uploads/initiate/bulk`,
                request: request,
                requestProtoType: bulkInitiateFileUploadRequestDtoProtobuf,
                responseProtoType: bulkInitiateFileUploadResponseDtoProtobuf,
                boxLinkToken: this._boxLinkTokenService.get()
            });
    
        return deserializeBulkUploadResponse(request, response);       
    }

    public getUploadDetails(accessCode: string, uploadExternalId: string): Promise<BoxGetFileUploadDetailsResponse> {
        const call = this
            ._http
            .get<BoxGetFileUploadDetailsResponse>(
                `/api/access-codes/${accessCode}/uploads/${uploadExternalId}`, {
                headers: this.getHeaders()
            });

        return firstValueFrom(call);
    }

    public getUploadList(accessCode: string): Promise<BoxGetUploadListResponse> {
        const call = this
            ._http
            .get<BoxGetUploadListResponse>(
                `/api/access-codes/${accessCode}/uploads`, {
                headers: this.getHeaders()
            });

        return firstValueFrom(call);
    }   

    public getZipPreviewDetails(accessCode: string, fileExternalId: string) {
        return this._protoHttp.get<ZipPreviewDetails>({
            route: `/api/access-codes/${accessCode}/files/${fileExternalId}/preview/zip`,
            responseProtoType: zipFileDetailsDtoProtobuf,
            boxLinkToken: this._boxLinkTokenService.get()
        });
    }

    public getZipContentDownloadLink(accessCode: string, fileExternalId: string, zipEntry: ZipEntry, contentDisposition: ContentDisposition): Promise<GetFileDownloadLinkResponse> {
        const call = this
            ._http
            .post<GetFileDownloadLinkResponse>(
                `/api/access-codes/${accessCode}/files/${fileExternalId}/preview/zip/download-link`, {
                    item: zipEntry,
                    contentDisposition: contentDisposition
            }, {
                headers: this.getHeaders()
            });

        return firstValueFrom(call);
    }
        
    public async countSelectedItems(accessCode: string, request: CountSelectedItemsRequest): Promise<CountSelectedItemsResponse> {
        const call = this
            ._http
            .post<CountSelectedItemsResponse>(
                `/api/access-codes/${accessCode}/count-selected-items`, request, {
                    headers: this.getHeaders()
                });

        return await firstValueFrom(call);
    }
    
    public async searchFilesTree(accessCode: string, request: SearchFilesTreeRequest): Promise<SearchFilesTreeResponse> {
        const result = await this._protoHttp.postJsonToProto<SearchFilesTreeRequest, SearchFilesTreeResponse>({
            route: `/api/access-codes/${accessCode}/search-files-tree`,
            request: request,
            responseProtoType: searchFilesTreeResponseDtoProtobuf,
            boxLinkToken: this._boxLinkTokenService.get()
        });
        
        return result;
    }
}