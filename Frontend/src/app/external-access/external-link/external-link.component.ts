import { Component, computed, OnDestroy, OnInit, Signal, signal, WritableSignal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, Navigation, NavigationEnd, Router } from '@angular/router';
import { FilesExplorerApi, FilesExplorerComponent } from '../../files-explorer/files-explorer.component';
import { AccessCodesApi } from './access-codes.api';
import { FileUploadApi } from '../../services/file-upload-manager/file-upload-manager';
import { GetBoxDetails } from '../contracts/external-access.contracts';
import DOMPurify from 'dompurify';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { filter, Subscription } from 'rxjs';
import { DataStore } from '../../services/data-store.service';
import { AppFolderItem } from '../../shared/folder-item/folder-item.component';
import { AppFileItem } from '../../shared/file-item/file-item.component';
import { BulkCreateFolderRequest, CheckTextractJobsStatusRequest, ContentDisposition, CountSelectedItemsRequest, CreateFolderRequest, FilePreviewDetailsField, GetBulkDownloadLinkRequest, GetFolderResponse, SearchFilesTreeRequest, SendAiFileMessageRequest, StartTextractJobRequest, UpdateAiConversationNameRequest, UploadFileAttachmentRequest } from '../../services/folders-and-files.api';
import { BulkInitiateFileUploadRequest } from '../../services/uploads.api';
import { CookieUtils } from '../../shared/cookies';

@Component({
    selector: 'app-external-link',
    imports: [
        MatButtonModule,
        FilesExplorerComponent
    ],
    templateUrl: './external-link.component.html',
    styleUrl: './external-link.component.scss'
})
export class ExternalLinkComponent implements OnInit, OnDestroy {
    private  _accessCode: string | null = null;
    private get _accessCodeValue(): string {
        if (!this._accessCode) {
            throw new Error('Access code not set.');
        }

        return this._accessCode;
    }

    currentFolderExternalId: WritableSignal<string | null> = signal(null);
    currentFileExternalIdInPreview: WritableSignal<string | null> = signal(null);
    details: WritableSignal<GetBoxDetails | null> = signal(null);
    initialBoxContent: WritableSignal<GetFolderResponse | null> = signal(null);
    headerHtml: WritableSignal<SafeHtml | null> = signal(null);
    footerHtml: WritableSignal<SafeHtml | null> = signal(null);

    filesApi: Signal<FilesExplorerApi>;
    uploadsApi: Signal<FileUploadApi>;

    ownerEmail = computed(() => this.details()?.ownerEmail ?? null);
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
    private _subscription: Subscription | null = null;

    constructor(
        private _accessCodesApi: AccessCodesApi,
        private _activatedRoute: ActivatedRoute,
        private _router: Router,
        private _sanitizer: DomSanitizer,
        private _dataStore: DataStore,
    ) { 
        this.filesApi = signal(this.getFilesExplorerApi());
        this.uploadsApi = signal(this.getFileUploadApi());
    }

    async ngOnInit() {
        await this.handleNavigationChange(this._router.lastSuccessfulNavigation);

        this._subscription = this._router.events
            .pipe(filter(event => event instanceof NavigationEnd))
            .subscribe(() => {
                const navigation = this._router.getCurrentNavigation();
                this.handleNavigationChange(navigation)
            });
    }

    private async handleNavigationChange(navigation: Navigation | null) {
        const accessCode = this._activatedRoute.snapshot.params['accessCode'] || null;
        const folderExternalId = this._activatedRoute.snapshot.params['folderExternalId'] || null;
        const fileExternalId = this._activatedRoute.snapshot.queryParams['fileId'] || null;

        const oldAccessCode = this._accessCode;
        this._accessCode = accessCode;

        await this.loadBox(oldAccessCode, accessCode, folderExternalId, fileExternalId),
        await this.loadHtml(oldAccessCode, accessCode)     
    }

    private async loadBox(oldAccessCode: string | null, accessCode: string, folderExternalId: string | null, fileExternalId: string | null) {
        let newFileExternalIdInPreview = this.currentFileExternalIdInPreview();

        if(this.currentFileExternalIdInPreview() != fileExternalId) {
            newFileExternalIdInPreview = fileExternalId;
        }

        if(oldAccessCode == accessCode && this.currentFolderExternalId() == folderExternalId) {
            this.currentFileExternalIdInPreview.set(newFileExternalIdInPreview);
            
            return;
        }
        
        try {
            const result = await this
                ._accessCodesApi
                .getDetailsAndContent(accessCode, folderExternalId);

            this.details.set(result.details);
            this.initialBoxContent.set(result);

            this.currentFolderExternalId.set(folderExternalId);
            this.currentFileExternalIdInPreview.set(newFileExternalIdInPreview);
        } catch (error) {
            console.error('Failed to load box', error);            
        } finally {
            this.isBoxLoaded.set(true);
        }
    }

    private async loadHtml(oldAccessCode: string | null, accessCode: string) {
        if(oldAccessCode == accessCode) {
            return;
        }

        try {
            const result = await this
                ._accessCodesApi
                .getHtml(accessCode);

            if(result.headerHtml) {
                const pureHeaderHtml = DOMPurify.sanitize(
                    result.headerHtml, { 
                        USE_PROFILES: { html: true } 
                    });

                this.headerHtml.set(this
                    ._sanitizer
                    .bypassSecurityTrustHtml(pureHeaderHtml));
            }

            if(result.footerHtml) {
                const pureFooterHtml = DOMPurify.sanitize(
                    result.footerHtml, { 
                        USE_PROFILES: { html: true } 
                    });

                this.footerHtml.set(this
                    ._sanitizer
                    .bypassSecurityTrustHtml(pureFooterHtml));
            }
        } catch (error) {
            console.error('Failed to load box html', error);            
        }
    }

    ngOnDestroy(): void {
        this._subscription?.unsubscribe();
    }

    public onFolderSelected(folder: AppFolderItem | null) {
        const folderExternalId = folder?.externalId ?? null;        

        if(folderExternalId == this.currentFolderExternalId()){
            return;
        }

        this.currentFolderExternalId.set(folderExternalId);
        this.setRoute(folderExternalId, undefined);
    }

    public onFilePreviewed(file: AppFileItem | null) {           
        if(file === null) {
            this.currentFileExternalIdInPreview.set(null);
            this.setRoute(this.currentFolderExternalId(), undefined);
        } else {
            if(file.externalId == this.currentFileExternalIdInPreview()) {
                return;
            }
        
            this.currentFileExternalIdInPreview.set(file.externalId);        
            this.setRoute(file.folderExternalId, { fileId: file.externalId });
        }
    }

    private setRoute(folderExternalId: string | null, queryParams: any | undefined) {
        if(folderExternalId == null) {
            this._router.navigate([`link/${this._accessCode}`], {
                replaceUrl: true,
                queryParams
            });
        } else {
            this._router.navigate([`link/${this._accessCode}/${folderExternalId}`], {
                replaceUrl: true,
                queryParams
            });
        }        
    }

    private getFileUploadApi(): FileUploadApi {
        return {
            bulkInitiateUpload: (request: BulkInitiateFileUploadRequest) => this._accessCodesApi.bulkInitiateUpload(
                this._accessCodeValue, 
                request),
                
            getUploadDetails: (uploadExternalId: string) => this._accessCodesApi.getUploadDetails(
                this._accessCodeValue, 
                uploadExternalId),

            initiatePartUpload: (uploadExternalId: string, partNumber: number) => this._accessCodesApi.initiatePartUpload(
                this._accessCodeValue, 
                uploadExternalId, 
                partNumber),

            completePartUpload: (uploadExternalId: string, partNumber: number, request: {eTag: string}) => this._accessCodesApi.completePartUpload(
                this._accessCodeValue, 
                uploadExternalId, 
                partNumber, 
                request),

            completeUpload: (uploadExternalId: string) => this._accessCodesApi.completeUpload(
                this._accessCodeValue, 
                uploadExternalId),

            abort: (uploadExternalId: string) => this._accessCodesApi.bulkDelete({
                accessCode: this._accessCodeValue,
                fileExternalIds: [],
                folderExternalIds: [],
                fileUploadExternalIds: [uploadExternalId]
            }),
            
            getXsrfToken: () => CookieUtils.GetXsrfBoxLinkToken()
        }
    }


    private getFilesExplorerApi(): FilesExplorerApi {
        return {
            invalidatePrefetchedFolderDependentEntries(folderExternalId) {
                return;
            },
            
            invalidatePrefetchedEntries: () => this._dataStore.invalidateEntries(
                key => key.startsWith(`external-link/${this._accessCodeValue}`)
            ),

            prefetchTopFolders: () => this._dataStore.prefetch(
                `external-link/${this._accessCodeValue}/folders`,
                () => this._accessCodesApi.getContent(this._accessCodeValue, null)),

            getTopFolders: () => this._dataStore.get(
                `external-link/${this._accessCodeValue}/folders`,
                () => this._accessCodesApi.getContent(this._accessCodeValue, null)),

            prefetchFolder: (folderExternalId: string) => this._dataStore.prefetch(
                `external-link/${this._accessCodeValue}/folders/${folderExternalId}`,
                () => this._accessCodesApi.getContent(this._accessCodeValue, folderExternalId)),

            getFolder: (folderExternalId: string) => this._dataStore.get(
                `external-link/${this._accessCodeValue}/folders/${folderExternalId}`,
                () => this._accessCodesApi.getContent(this._accessCodeValue, folderExternalId)),

            createFolder: (request: CreateFolderRequest) => this._accessCodesApi.createFolder(
                this._accessCodeValue, request),
                
            bulkCreateFolders: (request: BulkCreateFolderRequest) => this._accessCodesApi.bulkCreateFolders(
                this._accessCodeValue, request),
            
            updateFolderName: (folderExternalId: string, request: {name: string}) => this._accessCodesApi.updateFolderName(
                this._accessCodeValue, 
                folderExternalId, 
                request),

            moveItems: (request: {fileExternalIds: string[], folderExternalIds: string[], fileUploadExternalIds: string[], destinationFolderExternalId: string | null}) => this._accessCodesApi.moveItems(
                this._accessCodeValue, 
                request),

            updateFileName: (fileExternalId: string, request: {name: string}) => this._accessCodesApi.updateFileName(
                this._accessCodeValue, 
                fileExternalId, 
                request),

            getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => this._accessCodesApi.getDownloadLink(
                this._accessCodeValue, 
                fileExternalId,
                contentDisposition),

            bulkDelete: (fileExternalIds: string[], folderExternalIds: string[], fileUploadExternalIds: string[]) => this._accessCodesApi.bulkDelete({
                accessCode: this._accessCodeValue,
                fileExternalIds: fileExternalIds,
                folderExternalIds: folderExternalIds,
                fileUploadExternalIds: fileUploadExternalIds
            }),

            getBulkDownloadLink: (request: GetBulkDownloadLinkRequest) => this._accessCodesApi.getBulkDownloadLink(
                this._accessCodeValue, 
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

            getZipPreviewDetails: async (fileExternalId: string) =>  this._accessCodesApi.getZipPreviewDetails(
                this._accessCodeValue, 
                fileExternalId),

            getZipContentDownloadLink: async (fileExternalId, zipEntry, contentDisposition) =>  this._accessCodesApi.getZipContentDownloadLink(
                this._accessCodeValue, 
                fileExternalId,
                zipEntry,
                contentDisposition),

            startTextractJob: async (request: StartTextractJobRequest) => {
                throw new Error("not implemented");
            },

            checkTextractJobsStatus: async (request: CheckTextractJobsStatusRequest) => {
                throw new Error("not implemented")
            },

            countSelectedItems: async (request: CountSelectedItemsRequest) => this._accessCodesApi.countSelectedItems(
                this._accessCodeValue, 
                request),

                
            searchFilesTree: async (request: SearchFilesTreeRequest) => this._accessCodesApi.searchFilesTree(
                this._accessCodeValue,
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
        };
    }
}
