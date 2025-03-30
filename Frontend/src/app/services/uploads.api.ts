import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { UploadAlgorithm } from "./file-upload-manager/file-upload-manager";
import { getBulkInitiateFileUploadRequestDtoProtobuf } from "../protobuf/bulk-initiate-file-upload-request-dto.protobuf";
import { ProtoHttp } from "./protobuf-http.service";
import { getBulkInitiateFileUploadResponseDtoProtobuf } from "../protobuf/bulk-initiate-file-upload-response-dto.protobuf";

export interface GetUploadListResponse {
    items: Upload[];
}

export interface Upload {
    externalId: string;
    fileName: string;
    fileExtension: string;
    fileContentType: string;
    fileSizeInBytes: number;
    
    folderName: string;
    folderExternalId: string;
    folderPath: string[];

    alreadyUploadedPartNumbers: number[];
}

export interface InitiateFileUploadRequest {
    fileUploadExternalId: string;
    folderExternalId: string | null;
    fileNameWithExtension: string;
    fileSizeInBytes: number;
    fileContentType: string;
}

export interface InitiateFileUploadResponse {
    uploadExternalId: string;
    expectedPartsCount: number;
    algorithm: UploadAlgorithm;
    preSignedUploadLink: string | null;
}   

export interface BulkInitiateFileUploadRequest {
    items: InitiateFileUploadRequest[];
}

export interface BulkInitiateFileUploadResponse {
    items: InitiateFileUploadResponse[];
    preSignedMultiFileDirectUploadLink: string | null;
    newWorkspaceSizeInBytes: number | null;
}   

export interface BulkInitiateFileUploadResponseRaw {
    directUploads: {
        count: number,
        preSignedMultiFileDirectUploadLink: string;
    } | null;

    singleChunkUploads: {
        fileUploadExternalId: string;
        preSignedUploadLink: string;
    }[];

    multiStepChunkUploads: {        
        fileUploadExternalId: string;
        expectedPartsCount: number;
    }[];

    newWorkspaceSizeInBytes: number | null;
}

export function deserializeBulkUploadResponse(
    request: BulkInitiateFileUploadRequest, 
    response: BulkInitiateFileUploadResponseRaw): BulkInitiateFileUploadResponse {
 
    const results: InitiateFileUploadResponse[] = [];

    for (const singleChunkUpload of response.singleChunkUploads) {
        results.push({
            uploadExternalId: singleChunkUpload.fileUploadExternalId,
            preSignedUploadLink: singleChunkUpload.preSignedUploadLink,
            algorithm: 'single-chunk-upload',
            expectedPartsCount: 1
        });
    }

    for (const multiStepChunkUpload of response.multiStepChunkUploads) {
        results.push({
            uploadExternalId: multiStepChunkUpload.fileUploadExternalId,
            expectedPartsCount: multiStepChunkUpload.expectedPartsCount,
            preSignedUploadLink: null,
            algorithm: 'multi-step-chunk-upload'
        });
    }

    const directUploadsCount = response.directUploads?.count ?? 0;
    const actualResponseItemsCount = results.length + directUploadsCount;

    if(actualResponseItemsCount != request.items.length) {
        throw new Error(
            `Wrong count of result items from BulkInitiateFileUpload request. Expected number is ${request.items.length} but found ${actualResponseItemsCount}`);
    }

    if(directUploadsCount > 0) {
        const resultIdsSet = new Set<string>(results.map(x => x.uploadExternalId));
 
        const directUploadIds = request
            .items
            .map(x => x.fileUploadExternalId)
            .filter(id => !resultIdsSet.has(id));
        
        for (const directUploadId of directUploadIds) {
            results.push({
                uploadExternalId: directUploadId,
                algorithm: 'direct-upload',
                expectedPartsCount: 1,
                preSignedUploadLink: null
            });
        }
    }    
 
    return {
        items: results,
        preSignedMultiFileDirectUploadLink: response.directUploads?.preSignedMultiFileDirectUploadLink ?? null,
        newWorkspaceSizeInBytes: response.newWorkspaceSizeInBytes === -1
            ? null
            : response.newWorkspaceSizeInBytes
    };
 }

export interface GetUploadDetailsResponse {
    alreadyUploadedPartNumbers: number[];
    expectedPartsCount: number;
}

export interface InitiateFilePartUploadResponse {
    uploadPreSignedUrl: string;
    startsAtByte: number;
    endsAtByte: number;
    isCompleteFilePartUploadCallbackRequired: boolean;
}

export interface CompleteFilePartUploadRequest {
    eTag: string;
}

export interface CompleteFileUploadResponse {
    fileExternalId: string;
}

export interface GetUploadsCountResponse {
    count: number;
}

const bulkInitiateFileUploadRequestDtoProtobuf = getBulkInitiateFileUploadRequestDtoProtobuf();
const bulkInitiateFileUploadResponseDtoProtobuf = getBulkInitiateFileUploadResponseDtoProtobuf();

@Injectable({
    providedIn: 'root'
})
export class UploadsApi {
    constructor(
        private _http: HttpClient,
        private _protoHttp: ProtoHttp) {        
    }

    public async completePartUpload(workspaceExternalId: string, externalId: string, partNumber: number, request: CompleteFilePartUploadRequest): Promise<void> {
        const call = this
            ._http
            .post<void>(
                `/api/workspaces/${workspaceExternalId}/uploads/${externalId}/parts/${partNumber}/complete`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async initiatePartUpload(workspaceExternalId: string, externalId: string, partNumber: number): Promise<InitiateFilePartUploadResponse> {
        const call = this
            ._http
            .post<InitiateFilePartUploadResponse>(
                `/api/workspaces/${workspaceExternalId}/uploads/${externalId}/parts/${partNumber}/initiate`, null, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async completeUpload(workspaceExternalId: string, externalId: string): Promise<CompleteFileUploadResponse> {
        const call = this
            ._http
            .post<CompleteFileUploadResponse>(
                `/api/workspaces/${workspaceExternalId}/uploads/${externalId}/complete`, null, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async bulkInitiateUpload(workspaceExternalId: string, request: BulkInitiateFileUploadRequest): Promise<BulkInitiateFileUploadResponse> {        
        const response = await this._protoHttp.post<BulkInitiateFileUploadRequest, BulkInitiateFileUploadResponseRaw>({
            route: `/api/workspaces/${workspaceExternalId}/uploads/initiate/bulk`,
            request: request,
            requestProtoType: bulkInitiateFileUploadRequestDtoProtobuf,
            responseProtoType: bulkInitiateFileUploadResponseDtoProtobuf
        });

        return deserializeBulkUploadResponse(request, response);
    }

    public async getUploadDetails(workspaceExternalId: string, uploadExternalId: string): Promise<GetUploadDetailsResponse> {
        const call = this
            ._http
            .get<GetUploadDetailsResponse>(
                `/api/workspaces/${workspaceExternalId}/uploads/${uploadExternalId}`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async getUploadList(workspaceExternalId: string): Promise<GetUploadListResponse> {
        const call = this
            ._http
            .get<GetUploadListResponse>(
                `/api/workspaces/${workspaceExternalId}/uploads`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async getUploadsCount(workspaceExternalId: string): Promise<GetUploadsCountResponse> {
        const call = this
            ._http
            .get<GetUploadsCountResponse>(
                `/api/workspaces/${workspaceExternalId}/uploads/count`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }
}