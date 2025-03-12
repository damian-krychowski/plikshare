import { HttpClient, HttpHeaders, HttpParams } from "@angular/common/http";
import { Injectable, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { DataStore } from "./data-store.service";
import { ZipPreviewDetails } from "../files-explorer/file-inline-preview/file-inline-preview.component";
import { ZipEntry } from "./zip";
import { getTopFolderContetDtoProtobuf } from "../protobuf/top-folder-content-dto.protobuf";
import { getFolderContentDtoProtobuf } from "../protobuf/folder-content-dto.protobuf";
import { getZipFileDetailsDtoProtobuf } from "../protobuf/zip-file-details-dto.protobuf";
import { ProtoHttp } from "./protobuf-http.service";
import { getBulkCreateFolderResponseDtoProtobuf } from "../protobuf/bulk-create-folder-response-dto.protobuf";
import { getBulkCreateFolderRequestDtoProtobuf } from "../protobuf/bulk-create-folder-request-dto.protobuf";
import { AppFileItem } from "../shared/file-item/file-item.component";
import { AppUploadItem } from "../files-explorer/upload-item/upload-item.component";
import { AppFolderItem } from "../shared/folder-item/folder-item.component";
import { getSearchFilesTreeResponseDtoProtobuf } from "../protobuf/search-files-tree-response-dto.protobuf";

export interface UploadFileAttachmentRequest {
    externalId: string;
    name: string;
    extension: string;
    file: Blob
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
}

export interface FileDto {
    externalId: string;
    name: string;
    extension: string;
    sizeInBytes: number;
    wasUploadedByUser: boolean;
    isLocked: boolean;
}

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

export interface GetFilePreviewDetailsResponse {
    note: FilePreviewNote | null;
    comments: FilePreviewComment[];
    textractResultFiles: FilePreviewTextractResultFile[] | null;
    pendingTextractJobs: FilePreviewPendingTextractJob[] | null;
    aiConversations: FilePreviewAiConversation[] | null;
    attachments: FilePreviewAttachmentFile[] | null;
}

export type FilePreviewDetailsField = 'note' | 'comments' | 'textract-result-files' | 'pending-textract-jobs' | "attachments";

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
}

export interface SearchFilesTreeFileItem {
    externalId: string;
    name: string;
    extension: string;
    sizeInBytes: number;
    isLocked: boolean;
    wasUploadedByUser: boolean;
    folderIdIndex: number;
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
    }): Promise<void> {
        const call = this
            ._http
            .post<void>(`/api/workspaces/${args.workspaceExternalId}/bulk-delete`, {
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
        const call = this
            ._http
            .post<GetBulkDownloadLinkResponse>(`/api/workspaces/${workspaceExternalId}/files/bulk-download-link`, request);

        return firstValueFrom(call);
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
        createdAt?: string | null
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
            : new Date(folder.createdAt)
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