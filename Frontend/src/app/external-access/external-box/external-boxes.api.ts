import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { Observable, firstValueFrom } from "rxjs";
import { BoxMoveItemsToFolderRequest, BoxUpdateFolderNameRequest, BoxUpdateFileNameRequest, GetBoxDetailsAndFolderResponse, BoxCompleteFilePartUploadRequest, BoxInitiateFilePartUploadResponse, BoxCompleteFileUploadResponse, BoxGetUploadListResponse, GetBoxHtmlResponse, BoxGetFileUploadDetailsResponse } from "../contracts/external-access.contracts";
import { DataStore } from "../../services/data-store.service";
import { ZipPreviewDetails } from "../../files-explorer/file-inline-preview/file-inline-preview.component";
import { BulkCreateFolderRequest, BulkCreateFolderResponse, ContentDisposition, CountSelectedItemsRequest, CountSelectedItemsResponse, CreateFolderRequest, CreateFolderResponse, GetBulkDownloadLinkRequest, GetBulkDownloadLinkResponse, GetFileDownloadLinkResponse, GetFolderResponse, SearchFilesTreeRequest, SearchFilesTreeResponse } from "../../services/folders-and-files.api";
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
export class ExternalBoxesSetApi {
    constructor(
        private _http: HttpClient,
        private _protoHttp: ProtoHttp,
        private _dataStore: DataStore) {        
    }

    public async moveItems(boxExternalId: string, request: BoxMoveItemsToFolderRequest) {
        const call = this
            ._http
            .patch(
                `/api/boxes/${boxExternalId}/folders/move-items`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(boxExternalId))
        );
    }

    public async createFolder(boxExternalId: string, request: CreateFolderRequest): Promise<CreateFolderResponse> {
        const call = this
            ._http
            .post<CreateFolderResponse>(`/api/boxes/${boxExternalId}/folders`, request);

        const result = await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(boxExternalId))
        );

        return result;
    }

    public async bulkCreateFolders(boxExternalId: string, request: BulkCreateFolderRequest): Promise<BulkCreateFolderResponse> {        
        const result = await this._protoHttp.post<BulkCreateFolderRequest, BulkCreateFolderResponse>({
            route: `/api/boxes/${boxExternalId}/folders/bulk`,
            request: request,
            requestProtoType: bulkCreateFolderRequestDtoProtobuf,
            responseProtoType: bulkCreateFolderResponseDtoProtobuf
        });

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(boxExternalId))
        );

        return result;
    }

    public async bulkDelete(args: {
        boxExternalId: string, 
        fileExternalIds: string[],
        folderExternalIds: string[],
        fileUploadExternalIds: string[]
    }): Promise<void> {
        const call = this
            ._http
            .post(`/api/boxes/${args.boxExternalId}/bulk-delete`, {
                fileExternalIds: args.fileExternalIds,
                folderExternalIds: args.folderExternalIds,
                fileUploadExternalIds: args.fileUploadExternalIds
            });

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(args.boxExternalId))
        );
    }
    
    public async updateFolderName(boxExternalId: string, folderExternalId: string, request: BoxUpdateFolderNameRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(`/api/boxes/${boxExternalId}/folders/${folderExternalId}/name`, request);

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(boxExternalId))
        );
    }

    public async updateFileName(boxExternalId: string, fileExternalId: string, request: BoxUpdateFileNameRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(`/api/boxes/${boxExternalId}/files/${fileExternalId}/name`, request);

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(boxExternalId))
        );
    }

    public async completePartUpload(boxExternalId: string, externalId: string, partNumber: number, request: BoxCompleteFilePartUploadRequest): Promise<void> {
        const call = this
            ._http
            .post<void>(
                `/api/boxes/${boxExternalId}/uploads/${externalId}/parts/${partNumber}/complete`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        const result = await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(boxExternalId))
        );

        return result;
    }

    public async initiatePartUpload(boxExternalId: string, externalId: string, partNumber: number): Promise<BoxInitiateFilePartUploadResponse> {
        const call = this
            ._http
            .post<BoxInitiateFilePartUploadResponse>(
                `/api/boxes/${boxExternalId}/uploads/${externalId}/parts/${partNumber}/initiate`, null, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        const result = await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(boxExternalId))
        );

        return result;
    }

    public async completeUpload(boxExternalId: string, externalId: string): Promise<BoxCompleteFileUploadResponse> {
        const call = this
            ._http
            .post<BoxCompleteFileUploadResponse>(
                `/api/boxes/${boxExternalId}/uploads/${externalId}/complete`, null, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        const result = await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(boxExternalId))
        );

        return result;
    }

    public async initiateUpload(boxExternalId: string, request: InitiateFileUploadRequest): Promise<InitiateFileUploadResponse> {
        const call = this
            ._http
            .post<InitiateFileUploadResponse>(
                `/api/boxes/${boxExternalId}/uploads/initiate`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        const result = await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(boxExternalId))
        );

        return result;
    }

    public async bulkInitiateUpload(boxExternalId: string, request: BulkInitiateFileUploadRequest): Promise<BulkInitiateFileUploadResponse> {
        const response = await this._protoHttp.post<BulkInitiateFileUploadRequest, BulkInitiateFileUploadResponseRaw>({
            route: `/api/boxes/${boxExternalId}/uploads/initiate/bulk`,
            request: request,
            requestProtoType: bulkInitiateFileUploadRequestDtoProtobuf,
            responseProtoType: bulkInitiateFileUploadResponseDtoProtobuf
        });

        const result = deserializeBulkUploadResponse(request, response);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxKeysPrefix(boxExternalId))
        );

        return result;
    }

    public async getUploadDetails(boxExternalId: string, uploadExternalId: string): Promise<BoxGetFileUploadDetailsResponse> {
        const call = this
            ._http
            .get<BoxGetFileUploadDetailsResponse>(
                `/api/boxes/${boxExternalId}/uploads/${uploadExternalId}`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }
}

@Injectable({
    providedIn: 'root'
})
export class ExternalBoxesGetApi {
    constructor(
        private _http: HttpClient,
        private _protoHttp: ProtoHttp) {        
    }

    public getDownloadLink(boxExternalId: string, fileExternalId: string, contentDisposition: ContentDisposition): Promise<GetFileDownloadLinkResponse> {
        const call = this
            ._http
            .get<GetFileDownloadLinkResponse>(`/api/boxes/${boxExternalId}/files/${fileExternalId}/download-link`, {
                params: {
                    contentDisposition: contentDisposition
                }
            });

        return firstValueFrom(call);
    }

    public getBulkDownloadLink(boxExternalId: string, request: GetBulkDownloadLinkRequest): Promise<GetBulkDownloadLinkResponse> {
        const call = this
            ._http
            .post<GetBulkDownloadLinkResponse>(`/api/boxes/${boxExternalId}/files/bulk-download-link`, request);

        return firstValueFrom(call);
    }

    public getDetailsAndContent(boxExternalId: string, folderExternalId: string | null): Promise<GetBoxDetailsAndFolderResponse> {
        return this._protoHttp.get<GetBoxDetailsAndFolderResponse>({
            route: `/api/boxes/${boxExternalId}/${folderExternalId ?? ''}`,
            responseProtoType: boxDetailsAndFolderContentDtoProtobuf
        });
    }

    public getHtml(boxExternalId: string): Promise<GetBoxHtmlResponse> {
        const call = this
            ._http
            .get<GetBoxHtmlResponse>(
                `/api/boxes/${boxExternalId}/html`);

        return firstValueFrom(call);
    }

    public getContent(boxExternalId: string, folderExternalId: string | null): Promise<GetFolderResponse> {
        return this._protoHttp.get<GetFolderResponse>({
            route: `/api/boxes/${boxExternalId}/content/${folderExternalId ?? ''}`,
            responseProtoType: folderContentDtoProtobuf
        });
    }

    public getUploadList(boxExternalId: string): Promise<BoxGetUploadListResponse> {
        const call = this
            ._http
            .get<BoxGetUploadListResponse>(
                `/api/boxes/${boxExternalId}/uploads`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return firstValueFrom(call);
    }

    public getZipPreviewDetails(boxExternalId: string, fileExternalId: string) {
        return this._protoHttp.get<ZipPreviewDetails>({
            route: `/api/boxes/${boxExternalId}/files/${fileExternalId}/preview/zip`,
            responseProtoType: zipFileDetailsDtoProtobuf
        });
    }

    public getZipContentDownloadLink(boxExternalId: string, fileExternalId: string, zipEntry: ZipEntry, contentDisposition: ContentDisposition): Promise<GetFileDownloadLinkResponse> {
        const call = this
            ._http
            .post<GetFileDownloadLinkResponse>(
                `/api/boxes/${boxExternalId}/files/${fileExternalId}/preview/zip/download-link`, {
                    item: zipEntry,
                    contentDisposition: contentDisposition                    
            });

        return firstValueFrom(call);
    }   
    
    public async countSelectedItems(boxExternalId: string, request: CountSelectedItemsRequest): Promise<CountSelectedItemsResponse> {
        const call = this
            ._http
            .post<CountSelectedItemsResponse>(
                `/api/boxes/${boxExternalId}/count-selected-items`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }    
    
    public async searchFilesTree(boxExternalId: string, request: SearchFilesTreeRequest): Promise<SearchFilesTreeResponse> {
        const result = await this._protoHttp.postJsonToProto<SearchFilesTreeRequest, SearchFilesTreeResponse>({
            route: `/api/boxes/${boxExternalId}/search-files-tree`,
            request: request,
            responseProtoType: searchFilesTreeResponseDtoProtobuf
        });
        
        return result;
    }
}