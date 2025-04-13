import { HttpClient, HttpHeaders, HttpErrorResponse } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { BoxMoveItemsToFolderRequest, BoxUpdateFolderNameRequest, BoxUpdateFileNameRequest, GetBoxDetailsAndFolderResponse, BoxCompleteFilePartUploadRequest, BoxInitiateFilePartUploadResponse, BoxCompleteFileUploadResponse, BoxGetUploadListResponse, BoxGetFileUploadDetailsResponse } from "../contracts/external-access.contracts";
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
import { CheckFileLocksRequest, CheckFileLocksResponse } from "../../services/lock-status.api";
import { BOX_LINK_TOKEN_HEADER, BoxLinkTokenService } from "../../services/box-link-token.service";

const zipFileDetailsDtoProtobuf = getZipFileDetailsDtoProtobuf();
const folderContentDtoProtobuf = getFolderContentDtoProtobuf();
const boxDetailsAndFolderContentDtoProtobuf = getBoxDetailsAndContentResponseDtoProtobuf();
const bulkInitiateFileUploadRequestDtoProtobuf = getBulkInitiateFileUploadRequestDtoProtobuf();
const bulkInitiateFileUploadResponseDtoProtobuf = getBulkInitiateFileUploadResponseDtoProtobuf();
const bulkCreateFolderRequestDtoProtobuf = getBulkCreateFolderRequestDtoProtobuf();
const bulkCreateFolderResponseDtoProtobuf = getBulkCreateFolderResponseDtoProtobuf();
const searchFilesTreeResponseDtoProtobuf = getSearchFilesTreeResponseDtoProtobuf();

export interface UrlComponents {
    baseUrl: string;
    accessCode: string;
}

export interface ErrorHandlingCallbacks {
    onUnauthorized?: () => Promise<void>;
    onNotFound?: () => void;
    onOtherError?: (error: any) => void;
}

@Injectable({
    providedIn: 'root'
})
export class BoxWidgetApi {
    private errorHandlers: ErrorHandlingCallbacks = {};

    constructor(
        private _boxLinkTokenService: BoxLinkTokenService,
        private _http: HttpClient,
        private _protoHttp: ProtoHttp) {
    }

    /**
     * Set custom error handling callbacks
     */
    public setErrorHandlers(handlers: ErrorHandlingCallbacks): void {
        this.errorHandlers = { ...this.errorHandlers, ...handlers };
    }

    /**
     * Extracts base URL and access code from the full URL
     * @param url The full URL (e.g., https://localhost:8080/link/4T05Lb3RoJLXaAhzsh6Vjd)
     * @returns The extracted URL components
     */
    public extractUrlComponents(url: string): UrlComponents {
        try {
            const parsedUrl = new URL(url);
            
            // Extract access code from the path
            const pathParts = parsedUrl.pathname.split('/');
            const accessCode = pathParts[pathParts.length - 1];
            
            // Extract base URL
            const baseUrl = `${parsedUrl.protocol}//${parsedUrl.host}`;
            
            return { baseUrl, accessCode };
        } catch (error) {
            console.error('Invalid URL format:', error);
            throw new Error('Invalid URL format. Expected format: https://domain.com/link/accessCode');
        }
    }

    /**
     * Handle HTTP errors with appropriate actions
     */
    private async handleError(err: any, url: string) {
        if (err.status === 401) {
            if (this.errorHandlers.onUnauthorized) {
                await this.errorHandlers.onUnauthorized();
            } else {
                // Default behavior: try to start a new session
                await this.startSession(url);
                
            }
        } else if (err.status === 404) {
            if (this.errorHandlers.onNotFound) {
                this.errorHandlers.onNotFound();
            }
        } else {
            if (this.errorHandlers.onOtherError) {
                this.errorHandlers.onOtherError(err);
            } else {
                console.error("API error:", err);
            }
        }
    }

    /**
     * Execute a request with error handling
     */
    private async executeRequest<T>(
        url: string, 
        requestFn: () => Promise<T>, 
        maxRetries: number = 1
    ): Promise<T> {
        try {
            return await requestFn();
        } catch (error: any) {            
            await this.handleError(error, url);
                
            // If we should retry (only for 401 errors)
            if (error.status === 401 && maxRetries > 0) {
                return await this.executeRequest(url, requestFn, maxRetries - 1);
            }

            throw error;
        }
    }    

    public async startSession(url: string): Promise<void> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
                
        try {
            const call = this
                ._http
                .post(`${baseUrl}/api/access-codes/${accessCode}/start-session`, {}, {
                    observe: 'response' 
                });
                
            const response = await firstValueFrom(call);
            const token = response.headers.get(BOX_LINK_TOKEN_HEADER);
            
            if (token) {
                this._boxLinkTokenService.set(token);
            }
        } catch (error) {
            if (error instanceof HttpErrorResponse && error.status !== 401) {
                // We don't want to handle 401 errors when starting a session (would cause infinite loop)
                await this.handleError(error, url);
            }
            throw error;
        }
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

    public async moveItems(url: string, request: BoxMoveItemsToFolderRequest): Promise<void> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.patch(
                `${baseUrl}/api/access-codes/${accessCode}/folders/move-items`, 
                request, {
                    headers: this.getHeaders()
                }
            );
            
            await firstValueFrom(call);
        });
    }

    public async createFolder(url: string, request: CreateFolderRequest): Promise<CreateFolderResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.post<CreateFolderResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/folders`, 
                request, {
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }

    public async bulkCreateFolders(url: string, request: BulkCreateFolderRequest): Promise<BulkCreateFolderResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            return await this._protoHttp.post<BulkCreateFolderRequest, BulkCreateFolderResponse>({
                route: `${baseUrl}/api/access-codes/${accessCode}/folders/bulk`,
                request: request,
                requestProtoType: bulkCreateFolderRequestDtoProtobuf,
                responseProtoType: bulkCreateFolderResponseDtoProtobuf,
                xsrfToken: undefined,
                boxLinkToken: this._boxLinkTokenService.get()
            });
        });
    }

    public async bulkDelete(url: string, req: {
        fileExternalIds: string[], 
        folderExternalIds: string[],
        fileUploadExternalIds: string[]
    }): Promise<BulkDeleteResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.post<BulkDeleteResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/bulk-delete`, 
                {
                    fileExternalIds: req.fileExternalIds,
                    folderExternalIds: req.folderExternalIds,
                    fileUploadExternalIds: req.fileUploadExternalIds
                }, {
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }

    public async updateFolderName(url: string, folderExternalId: string, request: BoxUpdateFolderNameRequest): Promise<void> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.patch<void>(
                `${baseUrl}/api/access-codes/${accessCode}/folders/${folderExternalId}/name`, 
                request, {
                    headers: this.getHeaders()
                }
            );
            
            await firstValueFrom(call);
        });
    }

    public async updateFileName(url: string, fileExternalId: string, request: BoxUpdateFileNameRequest): Promise<void> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.patch<void>(
                `${baseUrl}/api/access-codes/${accessCode}/files/${fileExternalId}/name`, 
                request, {
                    headers: this.getHeaders()
                }
            );
            
            await firstValueFrom(call);
        });
    }

    public async getDownloadLink(url: string, fileExternalId: string, contentDisposition: ContentDisposition): Promise<GetFileDownloadLinkResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.get<GetFileDownloadLinkResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/files/${fileExternalId}/download-link`, 
                {
                    params: {
                        contentDisposition: contentDisposition
                    },
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }

    public async getBulkDownloadLink(url: string, request: GetBulkDownloadLinkRequest): Promise<GetBulkDownloadLinkResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.post<GetBulkDownloadLinkResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/files/bulk-download-link`, 
                request, {
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }

    public async getDetailsAndContent(url: string, folderExternalId: string | null): Promise<GetBoxDetailsAndFolderResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            return await this._protoHttp.get<GetBoxDetailsAndFolderResponse>({
                route: `${baseUrl}/api/access-codes/${accessCode}/${folderExternalId ?? ''}`,
                responseProtoType: boxDetailsAndFolderContentDtoProtobuf,
                boxLinkToken: this._boxLinkTokenService.get()
            });
        });
    }

    public async getContent(url: string, folderExternalId: string | null): Promise<GetFolderResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            return await this._protoHttp.get<GetFolderResponse>({
                route: `${baseUrl}/api/access-codes/${accessCode}/content/${folderExternalId ?? ''}`,
                responseProtoType: folderContentDtoProtobuf,
                boxLinkToken: this._boxLinkTokenService.get()
            });
        });
    }

    public async completePartUpload(url: string, externalId: string, partNumber: number, request: BoxCompleteFilePartUploadRequest): Promise<void> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.post<void>(
                `${baseUrl}/api/access-codes/${accessCode}/uploads/${externalId}/parts/${partNumber}/complete`, 
                request, {
                    headers: this.getHeaders()
                }
            );
            
            await firstValueFrom(call);
        });
    }

    public async initiatePartUpload(url: string, externalId: string, partNumber: number): Promise<BoxInitiateFilePartUploadResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.post<BoxInitiateFilePartUploadResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/uploads/${externalId}/parts/${partNumber}/initiate`, 
                null, {
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }

    public async completeUpload(url: string, externalId: string): Promise<BoxCompleteFileUploadResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.post<BoxCompleteFileUploadResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/uploads/${externalId}/complete`, 
                null, {
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }

    public async initiateUpload(url: string, request: InitiateFileUploadRequest): Promise<InitiateFileUploadResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.post<InitiateFileUploadResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/uploads/initiate`, 
                request, {
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }

    public async bulkInitiateUpload(url: string, request: BulkInitiateFileUploadRequest): Promise<BulkInitiateFileUploadResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const response = await this._protoHttp.post<BulkInitiateFileUploadRequest, BulkInitiateFileUploadResponseRaw>({
                route: `${baseUrl}/api/access-codes/${accessCode}/uploads/initiate/bulk`,
                request: request,
                requestProtoType: bulkInitiateFileUploadRequestDtoProtobuf,
                responseProtoType: bulkInitiateFileUploadResponseDtoProtobuf,
                boxLinkToken: this._boxLinkTokenService.get()
            });
            
            return deserializeBulkUploadResponse(request, response);
        });
    }

    public async getUploadDetails(url: string, uploadExternalId: string): Promise<BoxGetFileUploadDetailsResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.get<BoxGetFileUploadDetailsResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/uploads/${uploadExternalId}`, {
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }

    public async getUploadList(url: string): Promise<BoxGetUploadListResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.get<BoxGetUploadListResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/uploads`, {
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }   

    public async getZipPreviewDetails(url: string, fileExternalId: string) {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            return await this._protoHttp.get<ZipPreviewDetails>({
                route: `${baseUrl}/api/access-codes/${accessCode}/files/${fileExternalId}/preview/zip`,
                responseProtoType: zipFileDetailsDtoProtobuf,
                boxLinkToken: this._boxLinkTokenService.get()
            });
        });
    }

    public async getZipContentDownloadLink(url: string, fileExternalId: string, zipEntry: ZipEntry, contentDisposition: ContentDisposition): Promise<GetFileDownloadLinkResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.post<GetFileDownloadLinkResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/files/${fileExternalId}/preview/zip/download-link`, 
                {
                    item: zipEntry,
                    contentDisposition: contentDisposition
                }, {
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }
        
    public async countSelectedItems(url: string, request: CountSelectedItemsRequest): Promise<CountSelectedItemsResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            const call = this._http.post<CountSelectedItemsResponse>(
                `${baseUrl}/api/access-codes/${accessCode}/count-selected-items`, 
                request, {
                    headers: this.getHeaders()
                }
            );
            
            return await firstValueFrom(call);
        });
    }
    
    public async searchFilesTree(url: string, request: SearchFilesTreeRequest): Promise<SearchFilesTreeResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);
        
        return this.executeRequest(url, async () => {
            return await this._protoHttp.postJsonToProto<SearchFilesTreeRequest, SearchFilesTreeResponse>({
                route: `${baseUrl}/api/access-codes/${accessCode}/search-files-tree`,
                request: request,
                responseProtoType: searchFilesTreeResponseDtoProtobuf,
                boxLinkToken: this._boxLinkTokenService.get()
            });
        });
    }

    public async checkFileLocks(url: string, request: CheckFileLocksRequest): Promise<CheckFileLocksResponse> {
        const { baseUrl, accessCode } = this.extractUrlComponents(url);

        return this.executeRequest(url, async () => {
            const call = this
                ._http
                .post<CheckFileLocksResponse>(`${baseUrl}/api/access-codes/${accessCode}/lock-status/files`, request, {
                    headers: this.getHeaders()
                });

            return await firstValueFrom(call);
        });        
    }
}