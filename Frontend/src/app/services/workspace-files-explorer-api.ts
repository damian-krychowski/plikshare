import { FilesExplorerApi } from "../files-explorer/files-explorer.component";
import { AppFileItem } from "../shared/file-item/file-item.component";
import { DataStore } from "./data-store.service";
import { FileLockService } from "./file-lock.service";
import { BulkCreateFolderRequest, CheckTextractJobsStatusRequest, CheckTextractJobsStatusResponse, ContentDisposition, CountSelectedItemsRequest, CountSelectedItemsResponse, CreateFolderRequest, FilePreviewDetailsField, FoldersAndFilesGetApi, FoldersAndFilesSetApi, GetAiMessagesResponse, GetBulkDownloadLinkRequest, GetBulkDownloadLinkResponse, GetFilePreviewDetailsResponse, GetFilesTreeResponseDto, SearchFilesTreeRequest, SearchFilesTreeResponse, SendAiFileMessageRequest, StartTextractJobRequest, StartTextractJobResponse, UpdateAiConversationNameRequest, UploadFileAttachmentRequest } from "./folders-and-files.api";
import { ZipEntry } from "./zip";

export class WorkspaceFilesExplorerApi implements FilesExplorerApi {

    constructor(
        private _setApi: FoldersAndFilesSetApi,
        private _getApi: FoldersAndFilesGetApi,
        private _dataStore: DataStore,
        private _fileLockService: FileLockService,
        private _workspaceExternalId: string
    ) {

    }

    invalidatePrefetchedFolderDependentEntries(folderExternalId: string) {
        const keysPreifx = this._dataStore.boxesKey(this._workspaceExternalId);

        this._dataStore.invalidateEntries(
            key => key.startsWith(keysPreifx)
        );
    }

    invalidatePrefetchedEntries() {
        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.topFolderKey(this._workspaceExternalId))
        );
    };

    prefetchTopFolders() {
        this._dataStore.prefetchWorkspaceTopFolders(
            this._workspaceExternalId);
    };

    getTopFolders(){
        return this._dataStore.getWorkspaceTopFolders(
            this._workspaceExternalId);
    }

    prefetchFolder(folderExternalId: string){
        this._dataStore.prefetchWorkspaceFolder(
            this._workspaceExternalId, 
            folderExternalId);
    }

    getFolder(folderExternalId: string){
        return this._dataStore.getWorkspaceFolder(
            this._workspaceExternalId, 
            folderExternalId);
    }

    createFolder(request: CreateFolderRequest){
        return this._setApi.createFolder(this._workspaceExternalId, request);
    }

    bulkCreateFolders(request: BulkCreateFolderRequest){
        return this._setApi.bulkCreateFolders(this._workspaceExternalId, request);
    }

    updateFolderName(folderExternalId: string, request: {name: string}){
        return this._setApi.updateFolderName(
            this._workspaceExternalId, 
            folderExternalId, 
            request);
    }

    moveItems(request: {fileExternalIds: string[], folderExternalIds: string[], fileUploadExternalIds: string[], destinationFolderExternalId: string | null}){
        return this._setApi.moveItems(
            this._workspaceExternalId, 
            request);
    }

    updateFileName(fileExternalId: string, request: {name: string}){
        return this._setApi.updateFileName(
            this._workspaceExternalId, 
            fileExternalId, 
            request);
    }

    getDownloadLink(fileExternalId: string, contentDisposition: ContentDisposition){
        return this._getApi.getDownloadLink(
            this._workspaceExternalId, 
            fileExternalId,
            contentDisposition);
    }
    
    getFilePreviewDetails(fileExternalId: string, fields: FilePreviewDetailsField[] | null): Promise<GetFilePreviewDetailsResponse> {
        return this._getApi.getFilePreviewDetails(
            this._workspaceExternalId, 
            fileExternalId,
            fields);
    }
    
    updateFileNote(fileExternalId: string, noteContentJson: string): Promise<void> {
        return this._setApi.updateFileNote(
            this._workspaceExternalId, 
            fileExternalId, {
                contentJson: noteContentJson   
            });
    }

    bulkDelete(fileExternalIds: string[], folderExternalIds: string[], fileUploadExternalIds: string[]) {
        return this._setApi.bulkDelete({
            workspaceExternalId: this._workspaceExternalId, 
            fileExternalIds: fileExternalIds,
            folderExternalIds: folderExternalIds,
            fileUploadExternalIds: fileUploadExternalIds
        });
    }
    
    getBulkDownloadLink(request: GetBulkDownloadLinkRequest): Promise<GetBulkDownloadLinkResponse>{
        return this._getApi.getBulkDownloadLink(
            this._workspaceExternalId, 
            request);
    }

    createFileComment(fileExternalId: string, comment: {externalId: string, contentJson: string}) {
        return this._setApi.createFileComment(
            this._workspaceExternalId,
            fileExternalId,
            comment
        );
    }

    delefeFileComment(fileExternalId: string, commentExternalId: string) {
        return this._setApi.deleteFileComment(
            this._workspaceExternalId,
            fileExternalId,
            commentExternalId
        );
    }

    updateFileComment(fileExternalId: string, comment: {externalId: string, updatedContentJson: string}) {
        return this._setApi.updateFileComment(
            this._workspaceExternalId,
            fileExternalId,
            comment.externalId, {
                contentJson: comment.updatedContentJson
            }
        );
    }

    getZipPreviewDetails(fileExternalId: string) {
        return this._getApi.getZipPreviewDetails(
            this._workspaceExternalId,
            fileExternalId);
    }

    getZipContentDownloadLink(fileExternalId: string, zipEntry: ZipEntry, contentDisposition: ContentDisposition){
        return this._getApi.getZipContentDownloadLink(
            this._workspaceExternalId,
            fileExternalId,
            zipEntry,
            contentDisposition
        );
    }

    startTextractJob(request: StartTextractJobRequest): Promise<StartTextractJobResponse> {
        return this._setApi.startTextractJob(
            this._workspaceExternalId,
            request);
    }

    checkTextractJobsStatus(request: CheckTextractJobsStatusRequest): Promise<CheckTextractJobsStatusResponse> {
        return this._getApi.checkTextractJobsStatus(
            this._workspaceExternalId,
            request
        );
    }

    countSelectedItems(request: CountSelectedItemsRequest): Promise<CountSelectedItemsResponse>{
        return this._getApi.countSelectedItems(
            this._workspaceExternalId,
            request);
    }

    searchFilesTree(request: SearchFilesTreeRequest): Promise<SearchFilesTreeResponse>{
        return this._getApi.searchFilesTree(
            this._workspaceExternalId,
            request);
    }

    updateFileContent(fileExternalId: string, file: Blob): Promise<void>{
        return this._setApi.updateFileContent(
            this._workspaceExternalId,
            fileExternalId,
            file
        );
    }

    uploadFileAttachment(fileExternalId: string, request: UploadFileAttachmentRequest): Promise<void> {
        return this._setApi.uploadFileAttachment(
            this._workspaceExternalId,
            fileExternalId,
            request
        );
    }

    sendAiFileMessage(fileExternalId: string, request: SendAiFileMessageRequest): Promise<void> {
        return this._setApi.sendAiFileMessage(
            this._workspaceExternalId,
            fileExternalId,
            request
        );
    }

    updateAiConversationName(fileExternalId: string, fileArtifactExternalId: string, request: UpdateAiConversationNameRequest): Promise<void> {
        return this._setApi.updateAiConversationName({
            workspaceExternalId: this._workspaceExternalId,
            fileExternalId: fileExternalId,
            fileArtifactExternalId: fileArtifactExternalId,
            request: request
        });
    }

    deleteAiConversation(fileExternalId: string, fileArtifactExternalId: string): Promise<void> {
        return this._setApi.deleteAiConversation({
            workspaceExternalId: this._workspaceExternalId,
            fileExternalId: fileExternalId,
            fileArtifactExternalId: fileArtifactExternalId
        });
    }

    getAiMessages(fileExternalId: string, fileArtifactExternalId: string, fromConversationCounter: number): Promise<GetAiMessagesResponse> {
        return this._getApi.getAiMessages({
            workspaceExternalId: this._workspaceExternalId,
            fileExternalId: fileExternalId,
            fileArtifactExternalId: fileArtifactExternalId,
            fromConversationCounter: fromConversationCounter
        });
    }

    getAllAiMessages(fileExternalId: string, fileArtifactExternalId: string): Promise<GetAiMessagesResponse> {
        return this._dataStore.getWorkspaceAiMessages(
            this._workspaceExternalId,
            fileExternalId,
            fileArtifactExternalId);
    }

    prefetchAiMessages(fileExternalId: string, fileArtifactExternalId: string): void {
        this._dataStore.prefetchWorkspaceAiMessages(
            this._workspaceExternalId,
            fileExternalId,
            fileArtifactExternalId);
    }

    subscribeToLockStatus(file: AppFileItem) {
        this._fileLockService.subscribeToLockStatus(file)
    }

    unsubscribeFromLockStatus (fileExternalId: string) {
        this._fileLockService.unsubscribe(fileExternalId);
    }
}