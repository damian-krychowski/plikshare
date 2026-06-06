import { HttpClient, HttpHeaders, HttpParams } from "@angular/common/http";
import { Injectable, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { DataStore } from "./data-store.service";
import { ZipPreviewDetails } from "../files-explorer/file-inline-preview/file-inline-preview.component";
import { ZipEntry } from "./zip";
import { getTopFolderContetDtoProtobuf } from "../protobuf/top-folder-content-dto.protobuf";
import { getFolderContentDtoProtobuf } from "../protobuf/folder-content-dto.protobuf";
import { getZipFileDetailsDtoProtobuf } from "../protobuf/zip-file-details-dto.protobuf";
import { getGenerateFileThumbnailsBulkRequestDtoProtobuf } from "../protobuf/generate-file-thumbnails-bulk-request-dto.protobuf";
import { getGenerateFileThumbnailsBulkResponseDtoProtobuf } from "../protobuf/generate-file-thumbnails-bulk-response-dto.protobuf";
import { getCountThumbnailableFilesRequestDtoProtobuf, getCountThumbnailableFilesResponseDtoProtobuf } from "../protobuf/count-thumbnailable-files-request-dto.protobuf";
import { ProtoHttp } from "./protobuf-http.service";
import { getBulkCreateFolderResponseDtoProtobuf } from "../protobuf/bulk-create-folder-response-dto.protobuf";
import { getBulkCreateFolderRequestDtoProtobuf } from "../protobuf/bulk-create-folder-request-dto.protobuf";
import { AppFileItem } from "../shared/file-item/file-item.component";
import { AppUploadItem } from "../files-explorer/upload-item/upload-item.component";
import { AppFolderItem } from "../shared/folder-item/folder-item.component";
import { getSearchFilesTreeResponseDtoProtobuf } from "../protobuf/search-files-tree-response-dto.protobuf";
import { getBulkDownloadLinkRequestDtoProtobuf } from "../protobuf/get-bulk-download-link-request-dto.protobuf";
import { getBulkDownloadLinkResponseDtoProtobuf } from "../protobuf/get-bulk-download-link-response-dto.protobuf";
import { getZipBulkDownloadLinkRequestDtoProtobuf } from "../protobuf/get-zip-bulk-download-link-request-dto.protobuf";
import { getZipBulkDownloadLinkResponseDtoProtobuf } from "../protobuf/get-zip-bulk-download-link-response-dto.protobuf";

export interface UploadFileAttachmentRequest {
    externalId: string;
    name: string;
    extension: string;
    file: Blob
}

export type ThumbnailVariant = 'Mini' | 'Small' | 'Large';

export type DownloadImageFormat = 'jpeg' | 'png' | 'webp';

export interface UploadFileThumbnailRequest {
    externalId: string;
    name: string;
    extension: string;
    file: Blob;
    variant: ThumbnailVariant;
}

export interface BulkCreateFolderRequest {
    parentExternalId: string | null;
    ensureUniqueNames: boolean;
    folderTrees: BulkCreateFolderTree[];
}

export interface BulkCreateFolderTree {
    temporaryId: number;
    name: string;
    subfolders: BulkCreateFolderTree[];
}

export interface BulkCreateFolderResponse {
    items: {
        temporaryId: number;
        externalId: string;
    }[];
}

export interface CreateFolderRequest {
    externalId: string;
    parentExternalId: string | null;
    name: string;
    ensureUniqueName: boolean;
}

export interface CreateFolderResponse {
    externalId: string;
}

export interface UpdateFolderName {
    name: string;
}

export interface GetFolderResponse {
    folder: CurrentFolderDto;
    subfolders: SubfolderDto[];
    files: FileDto[];
    uploads: UploadDto[];
}

export interface CurrentFolderDto {
    externalId: string;
    name: string;
    ancestors: {
        name: string;
        externalId: string;
    }[];
}

export interface SubfolderDto {
    externalId: string;
    name: string;
    wasCreatedByUser: boolean;
    createdAt: string | null;
    position: number;
}

export interface FileDto {
    externalId: string;
    name: string;
    extension: string;
    sizeInBytes: number;
    wasUploadedByUser: boolean;
    isLocked: boolean;
    createdAt: string | null;
    position: number;
    miniThumbnailEtag: string | null;
}

export interface UpdatePositionsRequest {
    parentFolderExternalId: string | null;
    folders: UpdatePositionItem[];
    files: UpdatePositionItem[];
}

export interface UpdatePositionItem {
    externalId: string;
    position: number;
}

export type SortMode = 'custom' | 'name' | 'date';
export type SortDirection = 'asc' | 'desc';

export const ITEM_POSITION_STEP = 1024;

export interface UploadDto {
    externalId: string;
    fileName: string;
    fileExtension: string;
    fileContentType: string;
    fileSizeInBytes: number;

    alreadyUploadedPartNumbers: number[];
}

export interface MoveItemsToFolderRequest {
    fileExternalIds: string[];
    folderExternalIds: string[];
    fileUploadExternalIds: string[];
    destinationFolderExternalId: string | null;
    destinationPosition?: number | null;
}

export interface UpdateFileName {
    name: string;
}

export interface UpdateFileNoteRequest {
    contentJson: string;
}

export interface CreateFileCommentRequest {
    externalId: string;
    contentJson: string;
}

export interface UpdateFileCommentRequest {
    contentJson: string;
}

export interface DeleteFileRequest {
    fileExternalIds: string[];
}

export type ContentDisposition = "attachment" | "inline";

export interface GetFileDownloadLinkResponse {
    downloadPreSignedUrl: string;
}

export interface GetBulkDownloadLinkRequest {
    selectedFolders: string[];
    selectedFiles: string[];
    excludedFolders: string[];
    excludedFiles: string[];
}

export interface GetBulkDownloadLinkResponse {
    preSignedUrl: string;
}

export interface GetZipBulkDownloadLinkRequest {
    selectedFolderIds: number[];
    selectedEntryIndices: number[];
    excludedFolderIds: number[];
    excludedEntryIndices: number[];
}

export interface GetZipBulkDownloadLinkResponse {
    downloadPreSignedUrl: string;
}

export interface GetFilePreviewDetailsResponse {
    note: FilePreviewNote | null;
    comments: FilePreviewComment[];
    textractResultFiles: FilePreviewTextractResultFile[] | null;
    pendingTextractJobs: FilePreviewPendingTextractJob[] | null;
    aiConversations: FilePreviewAiConversation[] | null;
    attachments: FilePreviewAttachmentFile[] | null;
    thumbnails: FilePreviewThumbnail[] | null;
}

export type FilePreviewDetailsField = 'note' | 'comments' | 'textract-result-files' | 'pending-textract-jobs' | "attachments" | "thumbnails";

export type FilePreviewThumbnail = {
    externalId: string;
    variant: ThumbnailVariant;
    sizeInBytes: number;
}

export type GenerateFileThumbnailsResponse = {
    batchId: string;
}

export type GenerateThumbnailsBulkRequest = {
    selectedFolders: string[];
    selectedFiles: string[];
    excludedFolders: string[];
    excludedFiles: string[];
    variants: ThumbnailVariant[];
}

// Wire shape sent over protobuf. The fields/types match the C# DTO exactly (no enums, no ExtId
// wrappers) — variants travel as their string names ("Mini" / "Small" / "Large"); both sides
// Enum.Parse to the real enum.
export type GenerateFileThumbnailsBulkRequestDto = {
    selectedFolders: string[];
    selectedFiles: string[];
    excludedFolders: string[];
    excludedFiles: string[];
    variants: string[];
}

export type GenerateFileThumbnailsBulkResponseDto = {
    batchId: string;
    totalFiles: number;
}

export type GenerateThumbnailsBulkResponse = {
    batchId: string;
    totalFiles: number;
}

export type CountThumbnailableFilesRequest = {
    selectedFolders: string[];
    selectedFiles: string[];
    excludedFolders: string[];
    excludedFiles: string[];
}

export type CountThumbnailableFilesResponse = {
    fileCount: number;
    totalSizeInBytes: number;
}

export type CancelThumbnailBatchResponse = {
    cancelledCount: number;
}

export type FailedThumbnailVariant = {
    variant: ThumbnailVariant;
    error: string;
}

export type ThumbnailGenerationStatus = {
    failedVariants: FailedThumbnailVariant[];
    total: number;
    completed: number;
    failed: number;
    pending: number;
    readyThumbnails: ReadyThumbnail[];
    processingFileExternalIds: string[];
}

export type ReadyThumbnail = {
    fileExternalId: string;
    variants: ReadyThumbnailVariant[];
}

export type ReadyThumbnailVariant = {
    variant: ThumbnailVariant;
    etag: string;
}

export type FilePreviewComment = {
    externalId: string;
    contentJson: string;
    createdAt: string;
    createdBy: string;
    wasEdited: boolean;
}

export type FilePreviewNote = {
    contentJson: string;
    changedAt: string;
    changedBy: string;
}

export type FilePreviewTextractResultFile = {
    externalId: string;
    name: string;
    extension: string;
    sizeInBytes: number;
    features: TextractFeature[];
    wasUploadedByUser: boolean;
}

export type FilePreviewAttachmentFile = {
    externalId: string;
    name: string;
    extension: string;
    sizeInBytes: number;
    wasUploadedByUser: boolean;
}

export type FilePreviewPendingTextractJob = {
    externalId: string;
    status: TextractJobStatus;
    features: TextractFeature[];
}

export type FilePreviewAiConversation = {
    fileArtifactExternalId: string;
    aiConversationExternalId: string;
    aiIntegrationExternalId: string;
    isWaitingForAiResponse: boolean;
    createdAt: string;
    createdBy: string;
    conversationCounter: number;
    name: string | null;
}

export interface StartTextractJobRequest {
    fileExternalId: string;
    features: TextractFeature[];
}

export interface StartTextractJobResponse {
    externalId: string;
}

export type TextractFeature = 'tables' | 'forms' | 'layout';

export interface CheckTextractJobsStatusRequest {
    externalIds: string[]
}

export interface CheckTextractJobsStatusResponse {
    items: TextractJobStatusItem[];
}

export interface TextractJobStatusItem {
    externalId: string;
    status: TextractJobStatus;
}

export type TextractJobStatus = 'waits-for-file' | 'pending' | 'processing' | 'downloading-results' | 'completed' | 'partially-completed' | 'failed';

export interface GetFilesTreeResponseDto {
    files: TreeFileDto[];
    folders: TreeFolderDto[];
}

export interface TreeFileDto {
    externalId: string;
    folderId: number | null;
    sizeInBytes: number;
    fullName: string;
}

export interface TreeFolderDto {
    id: number;
    externalId: string;
    parentFolderId: number | null;
    name: string;
}

export interface CountSelectedItemsRequest {
    selectedFolders: string[];
    selectedFiles: string[];
    excludedFolders: string[];
    excludedFiles: string[];
}

export interface CountSelectedItemsResponse {
    selectedFoldersCount: number;
    selectedFilesCount: number;
    totalSizeInBytes: number;
}

export interface SearchFilesTreeRequest {
    folderExternalId: string | null;
    phrase: string;
}

export interface SearchFilesTreeResponse {
    folderExternalIds: string[];
    folders: SearchFilesTreeFolderItem[];
    files: SearchFilesTreeFileItem[];
    tooManyResultsCounter: number;
}

export interface SearchFilesTreeFolderItem {
    name: string;
    idIndex: number;
    parentIdIndex: number;
    wasCreatedByUser: boolean;
    createdAt: string | null;
    position: number;
}

export interface SearchFilesTreeFileItem {
    externalId: string;
    name: string;
    extension: string;
    sizeInBytes: number;
    isLocked: boolean;
    wasUploadedByUser: boolean;
    folderIdIndex: number;
    createdAt: string | null;
    position: number;
    miniThumbnailEtag: string | null;
}

export interface SendAiFileMessageRequest {
    fileArtifactExternalId: string;
    conversationExternalId: string;
    messageExternalId: string;
    conversationCounter: number;
    message: string;
    includes: AiInclude[];
    aiIntegrationExternalId: string;
    aiModel: string;
}

export type AiInclude = AiFileInclude | AiNotesInclude | AiCommentsInclude;

export type AiFileInclude = {
    $type: 'file';
    externalId: string;
}

export type AiNotesInclude = {
    $type: 'notes';
    externalId: string;
}

export type AiCommentsInclude = {
    $type: 'comments';
    externalId: string;
}

export interface UpdateAiConversationNameRequest {
    name: string;
}

export interface GetAiMessagesResponse {
    conversationExternalId: string;
    conversationName: string | null;
    messages: AiMessageDto[];
}

export interface AiMessageDto {
    externalId: string;
    conversationCounter: number;
    message: string;
    includes: AiInclude[];
    createdAt: string;
    createdBy: string;
    authorType: AiMessageAuthorType;
    aiModel: string;
}

export type AiMessageAuthorType = 'human' | 'ai';

export interface BulkDeleteResponse {
    newWorkspaceSizeInBytes: number | null;
}

const zipFileDetailsDtoProtobuf = getZipFileDetailsDtoProtobuf();
const folderContentDtoProtobuf = getFolderContentDtoProtobuf();
const topFolderContentDtoProtobuf = getTopFolderContetDtoProtobuf();

const bulkCreateFolderRequestDtoProtobuf = getBulkCreateFolderRequestDtoProtobuf();
const bulkCreateFolderResponseDtoProtobuf = getBulkCreateFolderResponseDtoProtobuf();

const searchFilesTreeResponseDtoProtobuf = getSearchFilesTreeResponseDtoProtobuf();

@Injectable({
    providedIn: 'root'
})
export class FoldersAndFilesSetApi {
    constructor(
        private _http: HttpClient,
        private _protoHttp: ProtoHttp,
        private _dataStore: DataStore) {
    }

    public async deleteAiConversation(args: {
        workspaceExternalId: string,
        fileExternalId: string;
        fileArtifactExternalId: string
    }): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/ai/workspaces/${args.workspaceExternalId}/files/${args.fileExternalId}/conversations/${args.fileArtifactExternalId}`,
                {
                    headers: new HttpHeaders({
                        'Content-Type': 'application/json'
                    })
                }
            );

        await firstValueFrom(call);
    }

    public async updateAiConversationName(args: {
        workspaceExternalId: string,
        fileExternalId: string;
        fileArtifactExternalId: string,
        request: UpdateAiConversationNameRequest
    }): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/ai/workspaces/${args.workspaceExternalId}/files/${args.fileExternalId}/conversations/${args.fileArtifactExternalId}/name`,
                args.request,
                {
                    headers: new HttpHeaders({
                        'Content-Type': 'application/json'
                    })
                }
            );

        await firstValueFrom(call);
    }

    public async sendAiFileMessage(workspaceExternalId: string, fileExternalId: string, request: SendAiFileMessageRequest): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/ai/workspaces/${workspaceExternalId}/files/${fileExternalId}/messages`,
                request,
                {
                    headers: new HttpHeaders({
                        'Content-Type': 'application/json'
                    })
                }
            );

        await firstValueFrom(call);
    }

    public async updateFileContent(workspaceExternalId: string, fileExternalId: string, file: Blob) {
        const call = this
            ._http
            .put(
                `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/content`,
                file,
                {
                    headers: new HttpHeaders({
                        'Content-Type': file.type
                    })
                }
            );

        await firstValueFrom(call);
    }

    public async moveItems(workspaceExternalId: string, request: MoveItemsToFolderRequest) {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${workspaceExternalId}/folders/move-items`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.topFolderKey(workspaceExternalId))
        );
    }

    public async createFolder(workspaceExternalId: string, request: CreateFolderRequest): Promise<CreateFolderResponse> {
        const call = this
            ._http
            .post<CreateFolderResponse>(
                `/api/workspaces/${workspaceExternalId}/folders`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        const result = await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.topFolderKey(workspaceExternalId))
        );

        return result;
    }

    public async bulkCreateFolders(workspaceExternalId: string, request: BulkCreateFolderRequest): Promise<BulkCreateFolderResponse> {
        const result = await this._protoHttp.post<BulkCreateFolderRequest, BulkCreateFolderResponse>({
            route: `/api/workspaces/${workspaceExternalId}/folders/bulk`,
            request: request,
            requestProtoType: bulkCreateFolderRequestDtoProtobuf,
            responseProtoType: bulkCreateFolderResponseDtoProtobuf
        });

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.topFolderKey(workspaceExternalId))
        );

        return result;
    }

    public async updatePositions(workspaceExternalId: string, request: UpdatePositionsRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${workspaceExternalId}/folders/update-positions`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.topFolderKey(workspaceExternalId))
        );
    }

    public async updateFolderName(workspaceExternalId: string, externalId: string, request: UpdateFolderName): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${workspaceExternalId}/folders/${externalId}/name`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.topFolderKey(workspaceExternalId))
        );
    }

    public async bulkDelete(args: {
        workspaceExternalId: string,
        fileExternalIds: string[],
        folderExternalIds: string[],
        fileUploadExternalIds: string[]
    }): Promise<BulkDeleteResponse> {
        const call = this
            ._http
            .post<BulkDeleteResponse>(`/api/workspaces/${args.workspaceExternalId}/bulk-delete`, {
                fileExternalIds: args.fileExternalIds,
                folderExternalIds: args.folderExternalIds,
                fileUploadExternalIds: args.fileUploadExternalIds
            });

        const result = await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.topFolderKey(args.workspaceExternalId))
        );

        return result;
    }

    public async updateFileName(workspaceExternalId: string, fileExternalId: string, request: UpdateFileName): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/name`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.topFolderKey(workspaceExternalId))
        );
    }

    public async updateFileNote(workspaceExternalId: string, fileExternalId: string, request: UpdateFileNoteRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/note`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async createFileComment(workspaceExternalId: string, fileExternalId: string, request: CreateFileCommentRequest): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/comments`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async deleteFileComment(workspaceExternalId: string, fileExternalId: string, commentExternalId: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/comments/${commentExternalId}`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateFileComment(workspaceExternalId: string, fileExternalId: string, commentExternalId: string, request: UpdateFileCommentRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/comments/${commentExternalId}`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async startTextractJob(workspaceExternalId: string, request: StartTextractJobRequest): Promise<StartTextractJobResponse> {
        const call = this
            ._http
            .post<StartTextractJobResponse>(
                `/api/workspaces/${workspaceExternalId}/aws-textract/jobs`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async uploadFileAttachment(workspaceExternalId: string, fileExternalId: string, request: UploadFileAttachmentRequest): Promise<void> {
        const formData = new FormData();

        const fullFileName = `${request.name}${request.extension}`;
        const file = new File([request.file], fullFileName, { type: request.file.type });

        formData.append('file', file);
        formData.append('fileExternalId', request.externalId);

        const call = this._http.post<{ attachmentId: string }>(
            `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/attachments`,
            formData
        );

        await firstValueFrom(call);
    }

    public async uploadFileThumbnail(
        workspaceExternalId: string,
        fileExternalId: string,
        request: UploadFileThumbnailRequest): Promise<void> {
        const formData = new FormData();

        const fullFileName = `${request.name}${request.extension}`;
        const file = new File([request.file], fullFileName, { type: request.file.type });

        formData.append('file', file);
        formData.append('fileExternalId', request.externalId);
        formData.append('variant', request.variant);

        const call = this._http.post<void>(
            `/api/workspaces/${workspaceExternalId}/media/thumbnails/${fileExternalId}`,
            formData
        );

        await firstValueFrom(call);
    }

    public async deleteFileThumbnail(
        workspaceExternalId: string,
        fileExternalId: string,
        variant: ThumbnailVariant): Promise<void> {
        const call = this._http.delete<void>(
            `/api/workspaces/${workspaceExternalId}/media/thumbnails/${fileExternalId}/${variant}`
        );

        await firstValueFrom(call);
    }

    public async generateFileThumbnails(
        workspaceExternalId: string,
        fileExternalId: string,
        variants: ThumbnailVariant[]): Promise<GenerateFileThumbnailsResponse> {
        const call = this._http.post<GenerateFileThumbnailsResponse>(
            `/api/workspaces/${workspaceExternalId}/media/thumbnails/${fileExternalId}/generate`,
            { variants }
        );

        return await firstValueFrom(call);
    }

    public async generateBulkThumbnails(
        workspaceExternalId: string,
        request: GenerateThumbnailsBulkRequest): Promise<GenerateThumbnailsBulkResponse> {
        // Both directions protobuf — request body is dominated by the file id list (cheap on the
        // wire), and staying on proto for the response keeps us on the same well-trodden path as
        // bulkCreateFolders / bulkInitiateFileUpload.
        const wireRequest: GenerateFileThumbnailsBulkRequestDto = {
            selectedFolders: request.selectedFolders,
            selectedFiles: request.selectedFiles,
            excludedFolders: request.excludedFolders,
            excludedFiles: request.excludedFiles,
            // ThumbnailVariant is a string union — its names match the C# enum, so we send the
            // strings as-is and the server Enum.Parse-es them.
            variants: request.variants,
        };

        const wireResponse = await this._protoHttp.post<GenerateFileThumbnailsBulkRequestDto, GenerateFileThumbnailsBulkResponseDto>({
            route: `/api/workspaces/${workspaceExternalId}/media/thumbnails/generate-bulk`,
            request: wireRequest,
            requestProtoType: getGenerateFileThumbnailsBulkRequestDtoProtobuf(),
            responseProtoType: getGenerateFileThumbnailsBulkResponseDtoProtobuf(),
        });

        return {
            batchId: wireResponse.batchId,
            totalFiles: wireResponse.totalFiles,
        };
    }

    public async getThumbnailGenerationStatus(
        workspaceExternalId: string,
        batchId: string): Promise<ThumbnailGenerationStatus> {
        const call = this._http.get<ThumbnailGenerationStatus>(
            `/api/workspaces/${workspaceExternalId}/media/thumbnails/batches/${batchId}/status`
        );

        return await firstValueFrom(call);
    }

    // Cancels a thumbnail batch: deletes its not-yet-started jobs. Files already being processed
    // finish, and completed thumbnails stay. Returns how many jobs were cancelled.
    public async cancelThumbnailBatch(
        workspaceExternalId: string,
        batchId: string): Promise<CancelThumbnailBatchResponse> {
        const call = this._http.post<CancelThumbnailBatchResponse>(
            `/api/workspaces/${workspaceExternalId}/media/thumbnails/batches/${batchId}/cancel`,
            {}
        );

        return await firstValueFrom(call);
    }

    // Server-pushed batch status over SSE (replaces client polling). The server sends an initial
    // snapshot, then a fresh status on every change, and closes once nothing is still generating.
    // Returns an unsubscribe that closes the connection. Cookie auth flows automatically via
    // withCredentials (same-origin EventSource).
    public subscribeThumbnailBatch(
        workspaceExternalId: string,
        batchId: string,
        onStatus: (status: ThumbnailGenerationStatus) => void): () => void {
        const eventSource = new EventSource(
            `/api/workspaces/${workspaceExternalId}/media/thumbnails/batches/${batchId}/events`,
            { withCredentials: true }
        );

        eventSource.onmessage = (event) => {
            try {
                onStatus(JSON.parse(event.data) as ThumbnailGenerationStatus);
            } catch (err) {
                console.error('Failed to parse thumbnail batch event:', err);
            }
        };

        eventSource.onerror = () => {
            // EventSource reconnects on its own; the caller closes us once the batch is terminal.
        };

        return () => eventSource.close();
    }

    public async downloadFileConverted(
        workspaceExternalId: string,
        fileExternalId: string,
        format: DownloadImageFormat,
        downloadFileName: string): Promise<void> {
        // Fetch as blob with HttpClient (sends auth cookies / antiforgery automatically),
        // then synthesize a click on a hidden <a download="..."> so the browser shows its
        // native Save dialog. Going straight to the URL via window.open would bypass
        // HttpClient interceptors and lose the requested filename on cross-origin headers.
        const blob = await firstValueFrom(
            this._http.get(
                `/api/workspaces/${workspaceExternalId}/media/${fileExternalId}/convert`,
                {
                    params: { format },
                    responseType: 'blob'
                }
            )
        );

        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = downloadFileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
}


@Injectable({
    providedIn: 'root'
})
export class FoldersAndFilesGetApi {
    constructor(
        private _http: HttpClient,
        private _protoHttp: ProtoHttp) {
    }    

    public async getAiMessages(args: {
        workspaceExternalId: string,
        fileExternalId: string;
        fileArtifactExternalId: string;
        fromConversationCounter?: number
    }): Promise<GetAiMessagesResponse> {
        let params = new HttpParams();
        
        if (args.fromConversationCounter !== undefined) {
            params = params.append('fromConversationCounter', args.fromConversationCounter.toString());
        }
        
        const call = this
            ._http
            .get<GetAiMessagesResponse>(
                `/api/ai/workspaces/${args.workspaceExternalId}/files/${args.fileExternalId}/conversations/${args.fileArtifactExternalId}/messages`,
                {
                    headers: new HttpHeaders({
                        'Content-Type': 'application/json'
                    }),
                    params: params
                }
            );
    
        return await firstValueFrom(call);
    }

    public getFolder(workspaceExternalId: string, externalId: string): Promise<GetFolderResponse> {
        return this._protoHttp.get<GetFolderResponse>({
            route: `/api/workspaces/${workspaceExternalId}/folders/${externalId}`,
            responseProtoType: folderContentDtoProtobuf
        });
    }

    public getTopFolders(workspaceExternalId: string): Promise<GetFolderResponse> {
        return this._protoHttp.get<GetFolderResponse>({
            route: `/api/workspaces/${workspaceExternalId}/folders`,
            responseProtoType: topFolderContentDtoProtobuf
        });
    }

    public getDownloadLink(workspaceExternalId: string, fileExternalId: string, contentDisposition: ContentDisposition): Promise<GetFileDownloadLinkResponse> {
        const call = this
            ._http
            .get<GetFileDownloadLinkResponse>(
                `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/download-link`, {
                params: {
                    contentDisposition: contentDisposition
                }
            });

        return firstValueFrom(call);
    }

    public getFilePreviewDetails(workspaceExternalId: string, fileExternalId: string, fields: FilePreviewDetailsField[] | null): Promise<GetFilePreviewDetailsResponse> {
        // Create params object only if fields are specified
        let params = new HttpParams();

        if (fields && fields.length > 0) {
            // Add each field as a separate query parameter
            fields.forEach(field => {
                params = params.append('fields', field);
            });
        }

        const call = this
            ._http
            .get<GetFilePreviewDetailsResponse>(
                `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/preview/details`, { params });

        return firstValueFrom(call);
    }

    public getBulkDownloadLink(workspaceExternalId: string, request: GetBulkDownloadLinkRequest): Promise<GetBulkDownloadLinkResponse> {
        return this._protoHttp.post<GetBulkDownloadLinkRequest, GetBulkDownloadLinkResponse>({
            route: `/api/workspaces/${workspaceExternalId}/files/bulk-download-link`,
            request: request,
            requestProtoType: getBulkDownloadLinkRequestDtoProtobuf(),
            responseProtoType: getBulkDownloadLinkResponseDtoProtobuf()
        });
    }

    public async getZipPreviewDetails(workspaceExternalId: string, fileExternalId: string) {
        return this._protoHttp.get<ZipPreviewDetails>({
            route: `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/preview/zip`,
            responseProtoType: zipFileDetailsDtoProtobuf
        });
    }

    public getZipContentDownloadLink(workspaceExternalId: string, fileExternalId: string, zipEntry: ZipEntry, contentDisposition: ContentDisposition): Promise<GetFileDownloadLinkResponse> {
        const call = this
            ._http
            .post<GetFileDownloadLinkResponse>(
                `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/preview/zip/download-link`, {
                item: zipEntry,
                contentDisposition: contentDisposition
            });

        return firstValueFrom(call);
    }

    public getZipBulkDownloadLink(
        workspaceExternalId: string,
        fileExternalId: string,
        request: GetZipBulkDownloadLinkRequest
    ): Promise<GetZipBulkDownloadLinkResponse> {
        return this._protoHttp.post<GetZipBulkDownloadLinkRequest, GetZipBulkDownloadLinkResponse>({
            route: `/api/workspaces/${workspaceExternalId}/files/${fileExternalId}/preview/zip/bulk-download-link`,
            request: request,
            requestProtoType: getZipBulkDownloadLinkRequestDtoProtobuf(),
            responseProtoType: getZipBulkDownloadLinkResponseDtoProtobuf()
        });
    }

    public async checkTextractJobsStatus(workspaceExternalId: string, request: CheckTextractJobsStatusRequest): Promise<CheckTextractJobsStatusResponse> {
        const call = this
            ._http
            .post<CheckTextractJobsStatusResponse>(
                `/api/workspaces/${workspaceExternalId}/aws-textract/jobs/status`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async countSelectedItems(workspaceExternalId: string, request: CountSelectedItemsRequest): Promise<CountSelectedItemsResponse> {
        const call = this
            ._http
            .post<CountSelectedItemsResponse>(
                `/api/workspaces/${workspaceExternalId}/count-selected-items`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async countThumbnailableFiles(workspaceExternalId: string, request: CountThumbnailableFilesRequest): Promise<CountThumbnailableFilesResponse> {
        const wireResponse = await this._protoHttp.post<CountThumbnailableFilesRequest, CountThumbnailableFilesResponse>({
            route: `/api/workspaces/${workspaceExternalId}/media/thumbnails/generate-bulk/count`,
            request: {
                selectedFolders: request.selectedFolders,
                selectedFiles: request.selectedFiles,
                excludedFolders: request.excludedFolders,
                excludedFiles: request.excludedFiles,
            },
            requestProtoType: getCountThumbnailableFilesRequestDtoProtobuf(),
            responseProtoType: getCountThumbnailableFilesResponseDtoProtobuf(),
        });

        return {
            fileCount: wireResponse.fileCount,
            // int64 decodes to a protobufjs Long — Number() collapses it (and is a no-op for a number).
            totalSizeInBytes: Number(wireResponse.totalSizeInBytes),
        };
    }

    public async searchFilesTree(workspaceExternalId: string, request: SearchFilesTreeRequest): Promise<SearchFilesTreeResponse> {
        const result = await this._protoHttp.postJsonToProto<SearchFilesTreeRequest, SearchFilesTreeResponse>({
            route: `/api/workspaces/${workspaceExternalId}/search-files-tree`,
            request: request,
            responseProtoType: searchFilesTreeResponseDtoProtobuf
        });
        
        return result;
    }
}

export function mapFileDtosToItems(files: FileDto[], folderExternalId: string | null): AppFileItem[] {
    return files?.map((f) => {
        const file: AppFileItem = {
            type: 'file',
            externalId: f.externalId,
            folderExternalId: folderExternalId,
            name: signal(f.name),
            extension: f.extension,
            sizeInBytes: f.sizeInBytes,
            wasUploadedByUser: f.wasUploadedByUser ?? false,
            folderPath: null,
            isLocked: signal(f.isLocked),
            createdAt: f.createdAt == null ? null : new Date(f.createdAt),
            position: signal(f.position),
            miniThumbnailEtag: signal(f.miniThumbnailEtag ?? null),

            isSelected: signal(false),
            isNameEditing: signal(false),
            isCut: signal(false),
            isHighlighted: signal(false)
        };

        return file;
    }) ?? [];
}

export function mapUploadDtosToItems(uploads: UploadDto[], folderExternalId: string | null): AppUploadItem[] {
    return uploads?.map((u) => {
        const upload: AppUploadItem = {
            type: 'upload',
            externalId: u.externalId,
            fileName: signal(u.fileName),
            folderExternalId: folderExternalId,
            fileExtension: u.fileExtension,
            fileContentType: u.fileContentType,
            fileSizeInBytes: u.fileSizeInBytes,
            alreadyUploadedPartNumbers: u.alreadyUploadedPartNumbers,
            fileUpload: signal(undefined),
            isSelected: signal(false),
            isCut: signal(false)
        };

        return upload;
    }) ?? [];
}

export function mapFolderDtosToItems(folders: SubfolderDto[], ancestors: { externalId: string, name: string }[]): AppFolderItem[] {
    return folders?.map((folder) => mapFolderDtoToItem(
        folder,
        ancestors)) ?? [];
}

export function mapFolderDtoToItem(
    folder: {
        externalId: string,
        name: string,
        wasCreatedByUser?: boolean,
        createdAt?: string | null,
        position?: number
    }, ancestors: {
        externalId: string,
        name: string
    }[]): AppFolderItem {

    return {
        type: 'folder',
        externalId: folder.externalId,
        name: signal(folder.name),
        ancestors: ancestors,
        isSelected: signal(false),
        isNameEditing: signal(false),
        isCut: signal(false),
        isHighlighted: signal(false),
        wasCreatedByUser: folder.wasCreatedByUser ?? false,
        createdAt: folder.createdAt == null
            ? null
            : new Date(folder.createdAt),
        position: signal(folder.position ?? 0)
    };
}

export function mapGetFolderResponseToItems(topFolderExternalId: string | null, folderResponse: GetFolderResponse): {
    selectedFolder: AppFolderItem;
    subfolders: AppFolderItem[];
    files: AppFileItem[];
    uploads: AppUploadItem[];
} {
    const path = preparePath(
        topFolderExternalId,
        folderResponse.folder?.ancestors ?? []);

    const selectedFolder = mapFolderDtoToItem(
        folderResponse.folder,
        path);

    const subfolders = mapFolderDtosToItems(
        folderResponse.subfolders,
        [...path, {
            externalId: folderResponse.folder.externalId,
            name: folderResponse.folder.name
        }]);

    const files = mapFileDtosToItems(
        folderResponse.files,
        folderResponse.folder.externalId);

    const uploads = mapUploadDtosToItems(
        folderResponse.uploads,
        folderResponse.folder.externalId);

    return {
        selectedFolder,
        subfolders,
        files,
        uploads
    };
}

export function preparePath(topFolderExternalId: string | null, segments: { externalId: string; name: string; }[]): { externalId: string, name: string }[] {
    const result: { externalId: string, name: string }[] = [];
    let isBelowTopFolder = topFolderExternalId == null;

    for (let i = 0; i < segments.length; i++) {
        const element = segments[i];

        if (!isBelowTopFolder && element.externalId === topFolderExternalId) {
            isBelowTopFolder = true;
        }

        if (isBelowTopFolder) {
            result.push({
                externalId: element.externalId,
                name: element.name
            });
        }
    }

    return result;
}