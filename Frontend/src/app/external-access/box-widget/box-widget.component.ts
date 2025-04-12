import { Component, computed, input, OnDestroy, OnInit, Signal, signal, WritableSignal } from '@angular/core';
import { FilesExplorerApi, FilesExplorerComponent } from '../../files-explorer/files-explorer.component';
import { BoxWidgetApi } from './box-widget.api';
import { FileUploadApi } from '../../services/file-upload-manager/file-upload-manager';
import { GetBoxDetails } from '../contracts/external-access.contracts';
import { DataStore } from '../../services/data-store.service';
import { AppFolderItem } from '../../shared/folder-item/folder-item.component';
import { AppFileItem } from '../../shared/file-item/file-item.component';
import { BulkCreateFolderRequest, CheckTextractJobsStatusRequest, ContentDisposition, CountSelectedItemsRequest, CreateFolderRequest, FilePreviewDetailsField, GetBulkDownloadLinkRequest, GetFolderResponse, SearchFilesTreeRequest, SendAiFileMessageRequest, StartTextractJobRequest, UpdateAiConversationNameRequest, UploadFileAttachmentRequest } from '../../services/folders-and-files.api';
import { BulkInitiateFileUploadRequest } from '../../services/uploads.api';
import { interval, Subscription } from 'rxjs';
import { CheckFileLocksRequest, CheckFileLocksResponse } from '../../services/lock-status.api';

@Component({
    selector: 'app-box-widget',
    imports: [
        FilesExplorerComponent
    ],
    templateUrl: './box-widget.component.html',
    styleUrl: './box-widget.component.scss'
})
export class BoxWidgetComponent implements OnInit, OnDestroy {
    url = input.required<string>();

    currentFolderExternalId: WritableSignal<string | null> = signal(null);
    currentFileExternalIdInPreview: WritableSignal<string | null> = signal(null);
    details: WritableSignal<GetBoxDetails | null> = signal(null);
    initialBoxContent: WritableSignal<GetFolderResponse | null> = signal(null);

    filesApi: Signal<FilesExplorerApi>;
    uploadsApi: Signal<FileUploadApi>;

    isTurnedOn = computed(() => this.details()?.isTurnedOn ?? false);
    
    allowList = computed(() => this.details()?.allowList ?? false);
    allowCreateFolder = computed(() => this.details()?.allowCreateFolder ?? false);
    allowUpload = computed(() => this.details()?.allowUpload ?? false);
    allowMoveItems = computed(() => this.details()?.allowMoveItems ?? false);
    allowRenameFolder = computed(() => this.details()?.allowRenameFolder ?? false);
    allowDeleteFolder =  computed(() => this.details()?.allowDeleteFolder ?? false);
    allowRenameFile = computed(() => this.details()?.allowRenameFile ?? false);
    allowDeleteFile = computed(() => this.details()?.allowDeleteFile ?? false);
    allowDownload = computed(() => this.details()?.allowDownload ?? false);

    public isBoxLoaded = signal(false);

    private _fileLockService: BoxWidgetFileLockService;

    constructor(
        private _boxWidgetApi: BoxWidgetApi,
        private _dataStore: DataStore,
    ) { 
        this.filesApi = signal(this.getFilesExplorerApi());
        this.uploadsApi = signal(this.getFileUploadApi());

        this._fileLockService = new BoxWidgetFileLockService(
            this.url, 
            this._boxWidgetApi);
    }

    async ngOnInit() {        
        this._fileLockService.startPolling()
        await this.loadBox(this.url(), null);
    }
    
    ngOnDestroy(): void {
        this._fileLockService.stopPolling();
    }

    private async loadBox(url: string, folderExternalId: string | null) {
       
        try {
            const result = await this
                ._boxWidgetApi
                .getDetailsAndContent(url, folderExternalId);

            this.details.set(result.details);
            this.initialBoxContent.set(result);
        } catch (error) {
            console.error('Failed to load box', error);            
        } finally {
            this.isBoxLoaded.set(true);
        }
    }

    public onFolderSelected(folder: AppFolderItem | null) {
        const folderExternalId = folder?.externalId ?? null;        

        if(folderExternalId == this.currentFolderExternalId()){
            return;
        }

        this.currentFolderExternalId.set(folderExternalId);
        this.loadBox(this.url(), this.currentFolderExternalId());
    }

    public onFilePreviewed(file: AppFileItem | null) {           
        // if(file === null) {
        //     this.currentFileExternalIdInPreview.set(null);
        // } else {        
        //     this.currentFileExternalIdInPreview.set(file.externalId);        
        // }
    }

    private getFileUploadApi(): FileUploadApi {
        return {
            bulkInitiateUpload: (request: BulkInitiateFileUploadRequest) => this._boxWidgetApi.bulkInitiateUpload(
                this.url(), 
                request),
                
            getUploadDetails: (uploadExternalId: string) => this._boxWidgetApi.getUploadDetails(
                this.url(), 
                uploadExternalId),

            initiatePartUpload: (uploadExternalId: string, partNumber: number) => this._boxWidgetApi.initiatePartUpload(
                this.url(), 
                uploadExternalId, 
                partNumber),

            completePartUpload: (uploadExternalId: string, partNumber: number, request: {eTag: string}) => this._boxWidgetApi.completePartUpload(
                this.url(), 
                uploadExternalId, 
                partNumber, 
                request),

            completeUpload: (uploadExternalId: string) => this._boxWidgetApi.completeUpload(
                this.url(), 
                uploadExternalId),

            abort: async (uploadExternalId: string) => {
                await this._boxWidgetApi.bulkDelete(this.url(), {
                    fileExternalIds: [],
                    folderExternalIds: [],
                    fileUploadExternalIds: [uploadExternalId]
                });
            }
        }
    }


    private getFilesExplorerApi(): FilesExplorerApi {
        return {
            invalidatePrefetchedFolderDependentEntries(folderExternalId) {
                return;
            },
            
            invalidatePrefetchedEntries: () => this._dataStore.invalidateEntries(
                key => key.startsWith(`external-link/${this.url()}`)
            ),

            prefetchTopFolders: () => this._dataStore.prefetch(
                `external-link/${this.url()}/folders`,
                () => this._boxWidgetApi.getContent(this.url(), null)),

            getTopFolders: () => this._dataStore.get(
                `external-link/${this.url()}/folders`,
                () => this._boxWidgetApi.getContent(this.url(), null)),

            prefetchFolder: (folderExternalId: string) => this._dataStore.prefetch(
                `external-link/${this.url()}/folders/${folderExternalId}`,
                () => this._boxWidgetApi.getContent(this.url(), folderExternalId)),

            getFolder: (folderExternalId: string) => this._dataStore.get(
                `external-link/${this.url()}/folders/${folderExternalId}`,
                () => this._boxWidgetApi.getContent(this.url(), folderExternalId)),

            createFolder: (request: CreateFolderRequest) => this._boxWidgetApi.createFolder(
                this.url(), request),
                
            bulkCreateFolders: (request: BulkCreateFolderRequest) => this._boxWidgetApi.bulkCreateFolders(
                this.url(), request),
            
            updateFolderName: (folderExternalId: string, request: {name: string}) => this._boxWidgetApi.updateFolderName(
                this.url(), 
                folderExternalId, 
                request),

            moveItems: (request: {fileExternalIds: string[], folderExternalIds: string[], fileUploadExternalIds: string[], destinationFolderExternalId: string | null}) => this._boxWidgetApi.moveItems(
                this.url(), 
                request),

            updateFileName: (fileExternalId: string, request: {name: string}) => this._boxWidgetApi.updateFileName(
                this.url(), 
                fileExternalId, 
                request),

            getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => this._boxWidgetApi.getDownloadLink(
                this.url(), 
                fileExternalId,
                contentDisposition),

            bulkDelete: (fileExternalIds: string[], folderExternalIds: string[], fileUploadExternalIds: string[]) => this._boxWidgetApi.bulkDelete(this.url(), {
                fileExternalIds: fileExternalIds,
                folderExternalIds: folderExternalIds,
                fileUploadExternalIds: fileUploadExternalIds
            }),

            getBulkDownloadLink: (request: GetBulkDownloadLinkRequest) => this._boxWidgetApi.getBulkDownloadLink(
                this.url(), 
                request),

            getFilePreviewDetails: async (fileExternalId: string, fields: FilePreviewDetailsField[] | null) => {
                return {
                    note: null,
                    comments: [],
                    pendingTextractJobs: [],
                    textractResultFiles: [],
                    aiConversations: [],
                    attachments: []
                };
            },

            updateFileNote: async (fileExternalId: string, noteContentJson: string) => {
                return;
            },

            createFileComment: async (fileExternalId: string, comment: {externalId: string, contentJson: string}) => {
                return;
            },

            delefeFileComment: async (fileExternalId: string, commentExternalId: string) => {
                return;
            },

            updateFileComment: async (fileExternalId: string, comment: {externalId: string, updatedContentJson: string}) => {
                return;
            },

            uploadFileAttachment: async (fileExternalId: string, request: UploadFileAttachmentRequest) => {
                throw new Error("not implemented");
            },

            getZipPreviewDetails: async (fileExternalId: string) =>  this._boxWidgetApi.getZipPreviewDetails(
                this.url(), 
                fileExternalId),

            getZipContentDownloadLink: async (fileExternalId, zipEntry, contentDisposition) =>  this._boxWidgetApi.getZipContentDownloadLink(
                this.url(), 
                fileExternalId,
                zipEntry,
                contentDisposition),

            startTextractJob: async (request: StartTextractJobRequest) => {
                throw new Error("not implemented");
            },

            checkTextractJobsStatus: async (request: CheckTextractJobsStatusRequest) => {
                throw new Error("not implemented")
            },

            countSelectedItems: async (request: CountSelectedItemsRequest) => this._boxWidgetApi.countSelectedItems(
                this.url(), 
                request),

                
            searchFilesTree: async (request: SearchFilesTreeRequest) => this._boxWidgetApi.searchFilesTree(
                this.url(),
                request
            ),

            updateFileContent: async (fileExternalId: string, file: Blob) => {
                throw new Error("not implemented")
            },

            sendAiFileMessage: async (fileExternalId: string, request: SendAiFileMessageRequest) => {
                throw new Error("not implemented")
            },

            updateAiConversationName: async (fileExternalId: string, fileArtifactExternalId: string, request: UpdateAiConversationNameRequest) => {
                throw new Error("not implemented")
            },

            deleteAiConversation: async (fileExternalId: string, fileArtifactExternalId: string) => {
                throw new Error("not implemented")
            },
            
            getAiMessages: async (fileExternalId: string, fileArtifactExternalId: string) => {
                throw new Error("not implemented")
            },

            getAllAiMessages: async (fileExternalId: string, fileArtifactExternalId: string) => {
                throw new Error("not implemented")
            },
            
            prefetchAiMessages: async (fileExternalId: string, fileArtifactExternalId: string) => {
                throw new Error("not implemented")
            },

            subscribeToLockStatus: (file: AppFileItem) => this._fileLockService.subscribeToLockStatus(file),
            unsubscribeFromLockStatus: (fileExternalId: string) => this._fileLockService.unsubscribe(fileExternalId)
        };
    }
}

class BoxWidgetFileLockService {
    private lockedFiles = signal<Set<string>>(new Set());
    private subscriptions = new Map<string, AppFileItem>();
    private pollingSubscription: Subscription | null = null;

    constructor(
        private _url: Signal<string>,
        private _boxWidgetApi: BoxWidgetApi
    ) {
    }

    subscribeToLockStatus(file: AppFileItem) {
        if(!file.isLocked())
            return;

        const fileId = file.externalId;

        if (!this.subscriptions.has(fileId)) {
            this.subscriptions.set(fileId, file);

            this.lockedFiles.update(files => {
                files.add(fileId);
                return files;
            });
        }
    }

    unsubscribe(fileId: string) {
        this.subscriptions.delete(fileId);
        this.lockedFiles.update(files => {
            files.delete(fileId);
            return files;
        });
    }

    private async checkLockStatus() {
        if (this.subscriptions.size === 0) return;

        try {
            const url = this._url();

            if(!url)
                return;

            const fileIds = Array.from(this.subscriptions.keys());

            const response = await this._boxWidgetApi.checkFileLocks(url, {
                externalIds: fileIds
            });

            this.lockedFiles.set(new Set(response.lockedExternalIds));

            // Update individual file items and unsubscribe unlocked files
            this.subscriptions.forEach((file, id) => {
                const isLocked = response.lockedExternalIds.includes(id);
                file.isLocked.set(isLocked);
                
                // If file is no longer locked, remove it from subscriptions
                if (!isLocked) {
                    this.unsubscribe(id);
                }
            });
        } catch (error) {
            console.error('Failed to check lock status:', error);
        }
    }

    startPolling(intervalMs: number = 1000) {
        this.pollingSubscription = interval(intervalMs)
            .subscribe(() => this.checkLockStatus());
    }

    stopPolling() {
        this.pollingSubscription?.unsubscribe();
    }
}