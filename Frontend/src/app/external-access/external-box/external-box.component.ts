import { Component, computed, OnDestroy, OnInit, signal, WritableSignal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, Navigation, NavigationEnd, NavigationExtras, Router } from '@angular/router';
import { FilesExplorerApi, FilesExplorerComponent, ItemToHighlight } from '../../files-explorer/files-explorer.component';
import { ExternalBoxesGetApi, ExternalBoxesSetApi } from './external-boxes.api';
import { FileUploadApi } from '../../services/file-upload-manager/file-upload-manager';
import { GetBoxDetails } from '../contracts/external-access.contracts';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import DOMPurify from 'dompurify';
import { Subscription, filter } from 'rxjs';
import { DataStore } from '../../services/data-store.service';
import { PrefetchDirective } from '../../shared/prefetch.directive';
import { SearchInputComponent } from '../../shared/search-input/search-input.component';
import { SearchComponent, SearchSlideAnimation } from '../../shared/search/search.component';
import { InAppSharing } from '../../services/in-app-sharing.service';
import { SettingsMenuBtnComponent } from '../../shared/setting-menu-btn/settings-menu-btn.component';
import { AppFolderItem } from '../../shared/folder-item/folder-item.component';
import { SignOutService } from '../../services/sign-out.service';
import { AppFileItem } from '../../shared/file-item/file-item.component';
import { BulkCreateFolderRequest, CheckTextractJobsStatusRequest, ContentDisposition, CountSelectedItemsRequest, CreateFolderRequest, FilePreviewDetailsField, GetBulkDownloadLinkRequest, GetFolderResponse, SearchFilesTreeRequest, SendAiFileMessageRequest, StartTextractJobRequest, UpdateAiConversationNameRequest, UploadFileAttachmentRequest } from '../../services/folders-and-files.api';
import { BulkInitiateFileUploadRequest } from '../../services/uploads.api';
import { CookieUtils } from '../../shared/cookies';

@Component({
    selector: 'app-external-box',
    imports: [
        MatButtonModule,
        FilesExplorerComponent,
        PrefetchDirective,
        SearchInputComponent,
        SearchComponent,
        SettingsMenuBtnComponent
    ],
    templateUrl: './external-box.component.html',
    styleUrl: './external-box.component.scss',
    animations: [SearchSlideAnimation]
})
export class ExternalBoxComponent implements OnInit, OnDestroy  {
    private _boxExternalId: string | null = null;

    private get _boxExternalIdValue(): string {
        if (!this._boxExternalId) {
            throw new Error('BoxExternalId not set');
        }

        return this._boxExternalId;
    }

    currentFolderExternalId: WritableSignal<string | null> = signal(null);
    currentFileExternalIdInPreview: WritableSignal<string | null> = signal(null);
    details: WritableSignal<GetBoxDetails | null> = signal(null);
    headerHtml: WritableSignal<SafeHtml | null> = signal(null);
    footerHtml: WritableSignal<SafeHtml | null> = signal(null);
    itemToHighlight: WritableSignal<ItemToHighlight | null> = signal(null);
    initialBoxContent: WritableSignal<GetFolderResponse | null> = signal(null);

    filesApi: WritableSignal<FilesExplorerApi | null> = signal(null);
    uploadsApi: WritableSignal<FileUploadApi | null> = signal(null);

    isBoxLoaded = signal(false);

    name = computed(() => this.details()?.name);
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

    private _routerSubscription: Subscription | null = null;
    
    constructor(
        private _externalBoxesGetApi: ExternalBoxesGetApi,
        private _externalBoxesSetApi: ExternalBoxesSetApi,
        private _activatedRoute: ActivatedRoute,
        private _router: Router,
        private _sanitizer: DomSanitizer,
        private _signOutService: SignOutService,
        private _inAppSharing: InAppSharing,
        public dataStore: DataStore
    ) { 

    }

    async ngOnInit() {        
        await this.handleNavigationChange(this._router.lastSuccessfulNavigation);

        this._routerSubscription = this._router.events
            .pipe(filter(event => event instanceof NavigationEnd))
            .subscribe(() => {
                const navigation = this._router.getCurrentNavigation();
                this.handleNavigationChange(navigation)
            });
    }

    ngOnDestroy(): void {
        this._routerSubscription?.unsubscribe();
    }

    private async handleNavigationChange(navigation: Navigation | null) {
        const boxExternalId = this._activatedRoute.snapshot.params['boxExternalId'] || null;
        const folderExternalId = this._activatedRoute.snapshot.params['folderExternalId'] || null;
        const fileExternalId = this._activatedRoute.snapshot.queryParams['fileId'] || null;

        const oldBoxExternalId = this._boxExternalId;
        this._boxExternalId = boxExternalId;

        await Promise.all([
            this.load(oldBoxExternalId, boxExternalId, folderExternalId, fileExternalId),
            this.loadHtml(oldBoxExternalId, boxExternalId)
        ]);

        this.tryConsumeNavigationState(navigation);
    }

    private async load(oldBoxExternalId: string | null, boxExternalId: string, folderExternalId: string | null, fileExternalId: string | null) {
        let newFileExternalIdInPreview = this.currentFileExternalIdInPreview();

        if(this.currentFileExternalIdInPreview() != fileExternalId) {
            newFileExternalIdInPreview = fileExternalId;
        }

        if(oldBoxExternalId == boxExternalId && this.currentFolderExternalId() == folderExternalId) {
            this.currentFileExternalIdInPreview.set(newFileExternalIdInPreview);
            
            return;
        }

        try {
            const result = await this
                .dataStore
                .getExternalBoxDetailsAndContent(boxExternalId, folderExternalId);

            this.details.set(result.details);
            this.initialBoxContent.set(result);

            this.currentFolderExternalId.set(folderExternalId);
            this.currentFileExternalIdInPreview.set(newFileExternalIdInPreview);

            this.filesApi.set(this.getFilesExplorerApi());
            this.uploadsApi.set(this.getFileUploadApi());
        } catch (error) {
            console.error('Failed to load box', error);            
        } finally {
            this.isBoxLoaded.set(true);
        }
    }

    private async loadHtml(oldBoxExternalId: string | null, boxExternalId: string) {
        if(oldBoxExternalId == boxExternalId) {
            return;
        }

        try {
            const result = await this
                .dataStore
                .getExternalBoxHtml(boxExternalId);

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
            console.error('Failed to load box', error);            
        }
    }


    private tryConsumeNavigationState(navigation: Navigation | null) {
        if(!navigation || !navigation.extras)
            return;

        this.tryHighlighFolder(navigation.extras);
        this.tryHighlightFile(navigation.extras); 
    }

    private tryHighlighFolder(extras: NavigationExtras) {
        if(!extras.state || !extras.state['folderToHighlight'])
            return;

        const folderToHighlightKey = extras
            .state['folderToHighlight'] as string;

        const folderExternalId = this
            ._inAppSharing
            .pop(folderToHighlightKey) as string;

        if(folderExternalId) {
            this.itemToHighlight.set({
                type: 'folder',
                externalId: folderExternalId
            });
        }
    }
    
    private tryHighlightFile(extras: NavigationExtras) {
        if(!extras.state || !extras.state['fileToHighlight'])
            return;

        const fileToHighlightKey = extras
            .state['fileToHighlight'] as string;

        const fileExternalId = this
            ._inAppSharing
            .pop(fileToHighlightKey) as string;

        if(fileExternalId) {
            this.itemToHighlight.set({
                type: 'file',
                externalId: fileExternalId
            });
        }
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
            this._router.navigate([`box/${this._boxExternalId}/`], {
                replaceUrl: true,
                queryParams
            });
        } else {
            this._router.navigate([`box/${this._boxExternalId}/${folderExternalId}`], {
                replaceUrl: true,
                queryParams
            });
        } 
    }

    private getFileUploadApi(): FileUploadApi {
        return {                
            bulkInitiateUpload: (request: BulkInitiateFileUploadRequest) => this._externalBoxesSetApi.bulkInitiateUpload(
                this._boxExternalIdValue, 
                request),

            getUploadDetails: (uploadExternalId: string) => this._externalBoxesSetApi.getUploadDetails(
                this._boxExternalIdValue, 
                uploadExternalId),

            initiatePartUpload: (uploadExternalId: string, partNumber: number) => this._externalBoxesSetApi.initiatePartUpload(
                this._boxExternalIdValue, 
                uploadExternalId, 
                partNumber),

            completePartUpload: (uploadExternalId: string, partNumber: number, request: {eTag: string}) => this._externalBoxesSetApi.completePartUpload(
                this._boxExternalIdValue, 
                uploadExternalId, 
                partNumber, 
                request),

            completeUpload: (uploadExternalId: string) => this._externalBoxesSetApi.completeUpload(
                this._boxExternalIdValue, 
                uploadExternalId),

            abort: (uploadExternalId: string) => this._externalBoxesSetApi.bulkDelete({
                boxExternalId: this._boxExternalIdValue,
                fileExternalIds: [],
                folderExternalIds: [],
                fileUploadExternalIds: [uploadExternalId]
            })
        }
    }

    private getFilesExplorerApi(): FilesExplorerApi {
        return {
            invalidatePrefetchedFolderDependentEntries(folderExternalId) {
                return;
            },

            invalidatePrefetchedEntries: () => this.dataStore.invalidateEntries(
                key => key.startsWith(this.dataStore.externalBoxKeysPrefix(this._boxExternalIdValue))
            ),

            prefetchTopFolders: () => this.dataStore.prefetchExternalBoxFolders(this._boxExternalIdValue),

            getTopFolders: () => this.dataStore.getExternalBoxFolders(this._boxExternalIdValue),

            prefetchFolder: (folderExternalId: string) => this.dataStore.prefetchExternalBoxFolder(
                this._boxExternalIdValue,
                folderExternalId),

            getFolder: (folderExternalId: string) => this.dataStore.getExternalBoxFolder(
                this._boxExternalIdValue,
                folderExternalId),

            createFolder: (request: CreateFolderRequest) => this._externalBoxesSetApi.createFolder(
                this._boxExternalIdValue, request),

            bulkCreateFolders: (request: BulkCreateFolderRequest) => this._externalBoxesSetApi.bulkCreateFolders(
                this._boxExternalIdValue, request),
            
            updateFolderName: (folderExternalId: string, request: {name: string}) => this._externalBoxesSetApi.updateFolderName(
                this._boxExternalIdValue, 
                folderExternalId,
                request),

            moveItems: (request: {fileExternalIds: string[], folderExternalIds: string[], fileUploadExternalIds: string[], destinationFolderExternalId: string | null}) => this._externalBoxesSetApi.moveItems(
                this._boxExternalIdValue, 
                request),

            updateFileName: (fileExternalId: string, request: {name: string}) => this._externalBoxesSetApi.updateFileName(
                this._boxExternalIdValue, 
                fileExternalId, 
                request),

            getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => this._externalBoxesGetApi.getDownloadLink(
                this._boxExternalIdValue, 
                fileExternalId,
                contentDisposition),

            bulkDelete: (fileExternalIds: string[], folderExternalIds: string[], fileUploadExternalIds: string[]) => this._externalBoxesSetApi.bulkDelete({
                boxExternalId: this._boxExternalIdValue,
                fileExternalIds: fileExternalIds,
                folderExternalIds: folderExternalIds,
                fileUploadExternalIds: fileUploadExternalIds
            }),

            getBulkDownloadLink: (request: GetBulkDownloadLinkRequest) => this._externalBoxesGetApi.getBulkDownloadLink(
                this._boxExternalIdValue, 
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
            }, //todo not yet implemented

            updateFileNote: async (fileExternalId: string, noteContentJson: string) => {
                return; //todo not yet implemented
            },

            createFileComment: async (fileExternalId: string, comment: {externalId: string, contentJson: string}) => {
                return; //todo not yet implemented
            },

            delefeFileComment: async (fileExternalId: string, commentExternalId: string) => {
                return; //todo not yet implemented
            },

            updateFileComment: async (fileExternalId: string, comment: {externalId: string, updatedContentJson: string}) => {
                return; //todo not yet implemented
            },

            
            uploadFileAttachment: async (fileExternalId: string, request: UploadFileAttachmentRequest) => {
                throw new Error("not implemented");
            },

            getZipPreviewDetails: async (fileExternalId: string) =>  this._externalBoxesGetApi.getZipPreviewDetails(
                this._boxExternalIdValue, 
                fileExternalId),

            getZipContentDownloadLink: async (fileExternalId, zipEntry, contentDisposition) =>  this._externalBoxesGetApi.getZipContentDownloadLink(
                this._boxExternalIdValue, 
                fileExternalId,
                zipEntry,
                contentDisposition),
                
            startTextractJob: async (request: StartTextractJobRequest) => {
                throw new Error("not implemented");
            },
            
            checkTextractJobsStatus: async (request: CheckTextractJobsStatusRequest) => {
                throw new Error("not implemented")
            },
            
            countSelectedItems: async (request: CountSelectedItemsRequest) => this._externalBoxesGetApi.countSelectedItems(
                this._boxExternalIdValue, 
                request),
                                                
            searchFilesTree: async (request: SearchFilesTreeRequest) => this._externalBoxesGetApi.searchFilesTree(
                this._boxExternalIdValue,
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

    goToDashboard() {
        const details = this.details();

        if(details?.workspaceExternalId) {
            this._router.navigate([`workspaces/${details.workspaceExternalId}/boxes`]);
        } else {
            this._router.navigate(['workspaces']);
        }
    }
    
    async signOut() {
        await this._signOutService.signOut();
    }
}
