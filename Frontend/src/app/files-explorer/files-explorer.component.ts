import { Component, InputSignal, OnChanges, OnDestroy, OnInit, Renderer2, Signal, SimpleChanges, ViewChild, WritableSignal, computed, input, output, signal } from '@angular/core';
import { FileToUpload, FileUploadApi, FileUploadManager, UploadsAbortedEvent, UploadCompletedEvent, UploadsInitiatedEvent } from '../services/file-upload-manager/file-upload-manager';
import { AppUploadItem, UploadItemComponent } from './upload-item/upload-item.component';
import { ConfirmOperationDirective } from '../shared/operation-confirm/confirm-operation.directive';
import { AppFolderItem, FolderItemComponent, FolderOperations } from '../shared/folder-item/folder-item.component';
import { AppFileItem, AppFileItems, FileItemComponent, FileOperations } from '../shared/file-item/file-item.component';
import { DropFilesDirective } from './directives/drop-files.directive';
import { DragOverStayDirective } from './directives/drag-over-stay.directive';
import { FolderPathComponent } from './folder-path/folder-path.component';
import { ItemButtonComponent } from '../shared/buttons/item-btn/item-btn.component';
import { ActionButtonComponent } from '../shared/buttons/action-btn/action-btn.component';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FileInlinePreviewComponent, FilePreviewOperations, ZipPreviewDetails } from './file-inline-preview/file-inline-preview.component';
import { StorageSizePipe } from '../shared/storage-size.pipe';
import { EditableTxtComponent } from '../shared/editable-txt/editable-txt.component';
import { BulkUploadPreviewComponent, BulkFileUpload, SingleBulkFileUpload, CreatedFolder } from './bulk-upload-preview/bulk-upload-preview.component';
import { BulkCreateFolderRequest, BulkCreateFolderResponse, BulkDeleteResponse, CheckTextractJobsStatusRequest, CheckTextractJobsStatusResponse, ContentDisposition, CountSelectedItemsRequest, CountSelectedItemsResponse, CreateFolderRequest, CreateFolderResponse, CurrentFolderDto, FileDto, FilePreviewDetailsField, GetAiMessagesResponse, GetBulkDownloadLinkRequest, GetBulkDownloadLinkResponse, GetFileDownloadLinkResponse, GetFilePreviewDetailsResponse, GetFilesTreeResponseDto, GetFolderResponse, mapFileDtosToItems, mapFolderDtosToItems, mapFolderDtoToItem, mapGetFolderResponseToItems, mapUploadDtosToItems, SearchFilesTreeRequest, SearchFilesTreeResponse, SendAiFileMessageRequest, StartTextractJobRequest, StartTextractJobResponse, SubfolderDto, UpdateAiConversationNameRequest, UploadDto, UploadFileAttachmentRequest } from '../services/folders-and-files.api';
import { ZipEntry } from '../services/zip';
import { FileSlicer } from '../services/file-upload-manager/file-slicer';
import { TextractJobStatusService } from '../services/textract-job-status.service';
import { FileTreeDeleteSelectionState, FileTreeSearchRequest, FileTreeSelectionState, FileTreeViewComponent, LoadFolderNodeRequest, SearchedFilesSelection } from '../shared/file-tree-view/file-tree-view.component';
import { getBase62Guid } from '../services/guid-base-62';
import { Debouncer } from '../services/debouncer';
import { ItemSearchComponent } from '../shared/item-search/item-search.component';
import { TreeViewMode } from '../shared/file-tree-view/tree-item';
import { FileInlinePreviewCommandsPipeline } from './file-inline-preview/file-inline-preview-commands-pipeline';
import { WorkspaceIntegrations } from '../services/workspaces.api';

export interface FilesExplorerApi {
    invalidatePrefetchedFolderDependentEntries: (folderExternalId: string) => void;
    invalidatePrefetchedEntries: () => void;

    prefetchTopFolders: () => void;
    getTopFolders: () => Promise<GetFolderResponse>;
    prefetchFolder: (folderExternalId: string) => void;
    getFolder: (folderExternalId: string) => Promise<GetFolderResponse>;
    createFolder: (request: CreateFolderRequest) => Promise<CreateFolderResponse>;
    bulkCreateFolders: (request: BulkCreateFolderRequest) => Promise<BulkCreateFolderResponse>;

    updateFolderName: (folderExternalId: string, request: { name: string }) => Promise<void>;

    moveItems: (request: {
        fileExternalIds: string[],
        folderExternalIds: string[],
        fileUploadExternalIds: string[],
        destinationFolderExternalId: string | null
    }) => Promise<void>;
    updateFileName: (fileExternalId: string, request: { name: string }) => Promise<void>;
    getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => Promise<GetFileDownloadLinkResponse>;
    getFilePreviewDetails: (fileExternalId: string, fields: FilePreviewDetailsField[] | null) => Promise<GetFilePreviewDetailsResponse>;
    bulkDelete: (fileExternalIds: string[], folderExternalIds: string[], fileUploadExternalIds: string[]) => Promise<BulkDeleteResponse>;
    getBulkDownloadLink: (request: GetBulkDownloadLinkRequest) => Promise<GetBulkDownloadLinkResponse>;    
    countSelectedItems: (request: CountSelectedItemsRequest) => Promise<CountSelectedItemsResponse>;
    searchFilesTree: (request: SearchFilesTreeRequest) => Promise<SearchFilesTreeResponse>;

    updateFileNote: (fileExternalId: string, noteContentJson: string) => Promise<void>;

    createFileComment: (fileExternalId: string, comment: {externalId: string, contentJson: string}) => Promise<void>;
    delefeFileComment: (fileExternalId: string, commentExternalId: string) => Promise<void>;
    updateFileComment: (fileExternalId: string, comment: {externalId: string, updatedContentJson: string}) => Promise<void>;

    updateFileContent: (fileExternalId: string, file: Blob) => Promise<void>;
    uploadFileAttachment: (fileExternalId: string, request: UploadFileAttachmentRequest) => Promise<void>;

    getZipPreviewDetails: (fileExternalId: string) => Promise<ZipPreviewDetails>;
    getZipContentDownloadLink: (fileExternalId: string, zipEntry: ZipEntry, contentDisposition: ContentDisposition) => Promise<GetFileDownloadLinkResponse>;

    startTextractJob(request: StartTextractJobRequest): Promise<StartTextractJobResponse>;
    checkTextractJobsStatus(request: CheckTextractJobsStatusRequest): Promise<CheckTextractJobsStatusResponse>;

    sendAiFileMessage(fileExternalId: string, request: SendAiFileMessageRequest): Promise<void>;
    updateAiConversationName(fileExternalId: string, fileArtifactExternalId: string, request: UpdateAiConversationNameRequest): Promise<void>;
    deleteAiConversation(fileExternalId: string, fileArtifactExternalId: string): Promise<void>;
    getAiMessages(fileExternalId: string, fileArtifactExternalId: string, fromConversationCounter: number): Promise<GetAiMessagesResponse>;
    getAllAiMessages(fileExternalId: string, fileArtifactExternalId: string): Promise<GetAiMessagesResponse>;
    prefetchAiMessages(fileExternalId: string, fileArtifactExternalId: string): void;
}

export type InitialContent = {
    folder: CurrentFolderDto | null;
    subfolders: SubfolderDto[];
    files: FileDto[];
    uploads: UploadDto[];
}

type ExplorerItem = AppFolderItem | AppFileItem | AppUploadItem;

export type ItemToHighlight = {
    type: 'folder' | 'file';
    externalId: string;
}

type ViewMode = 'list-view' | 'tree-view';

@Component({
    selector: 'app-files-explorer',
    imports: [
        FormsModule,
        UploadItemComponent,
        ConfirmOperationDirective,
        FolderItemComponent,
        FileItemComponent,
        DropFilesDirective,
        DragOverStayDirective,
        ConfirmOperationDirective,
        FolderPathComponent,
        ItemButtonComponent,
        ActionButtonComponent,
        MatCheckboxModule,
        MatTooltipModule,
        FileInlinePreviewComponent,
        StorageSizePipe,
        EditableTxtComponent,
        BulkUploadPreviewComponent,
        FileTreeViewComponent,
        ItemSearchComponent
    ],
    templateUrl: './files-explorer.component.html',
    styleUrl: './files-explorer.component.scss'
})
export class FilesExplorerComponent implements OnChanges, OnInit, OnDestroy  {
    filesApi = input.required<FilesExplorerApi>();
    uploadsApi = input.required<FileUploadApi | null>();
    currentFolderExternalId = input.required<string | null>();
    currentFileExternalIdInPreview = input.required<string | null>();
    initialContent = input.required<InitialContent | null>();
    topFolderExternalId = input<string>();    
    constHeightMode = input<boolean>(false);

    private _wasInitialContentLoaded = false;

    allowList = input(false);
    allowCreateFolder = input(false);
    allowUpload = input(false);
    allowMoveItems = input(false);
    allowFolderShare = input(false);
    allowFolderRename = input(false);
    allowFolderDelete = input(false);
    allowFileRename = input(false);
    allowDownload = input(false);
    allowFileDelete = input(false);
    allowFileEdit = input(false);
    allowPreviewNotes = input(false);
    allowPreviewComment = input(false);
    
    hideContextBar = input(false);
    hideSelectAll = input(false);
    hideItemsActions = input(false);

    integrations = input<WorkspaceIntegrations>({textract: null, chatGpt:[]});

    canSelectAll = computed(() => this.hasAnyItem() && !this.hideSelectAll() && this.canSelectItems());
    canSelectItems = computed(() => this.allowMoveItems() || this.allowDownload() || this.allowFileDelete() || this.allowFileDelete());

    showEmptyFolderMessaage = input(false);

    itemToHighlight = input<ItemToHighlight | null>();

    folderSelected = output<AppFolderItem | null>();
    boxCreated = output<AppFolderItem>();
    filePreviewed = output<AppFileItem | null>();
    workspaceSizeUpdated = output<number>();

    isLoadingFolders = signal(false);
    isDeleting = signal(false);
    isMoving = signal(false);
    isLoading = computed(() => this.isLoadingFolders() || this.isDeleting() || this.isMoving());
    
    filesStats = computed(() => this.getFilesStats(this.files()))
    filesCount = computed(() => this.filesStats().count);
    selectedFilesCount = computed(() => this.filesStats().selectedCount);
    isAnyFileSelected = computed(() => this.selectedFilesCount() > 0);
    isAnyFileNotSelected = computed(() => this.selectedFilesCount() < this.filesCount());

    foldersStats = computed(() => this.getSelectionStats(this.folders()))
    foldersCount = computed(() => this.foldersStats().count);
    selectedFoldersCount = computed(() => this.foldersStats().selectedCount);
    isAnyFolderSelected = computed(() => this.selectedFoldersCount() > 0);
    isAnyFolderNotSelected = computed(() => this.selectedFoldersCount() < this.foldersCount());

    uploadsStats = computed(() => this.getSelectionStats(this.uploads()))
    uploadsCount = computed(() => this.uploadsStats().count);
    selectedUploadsCount = computed(() => this.uploadsStats().selectedCount);
    isAnyUploadSelected = computed(() => this.selectedUploadsCount() > 0);
    isAnyUploadNotSelected = computed(() => this.selectedUploadsCount() < this.uploadsCount());
    
    itemsCount = computed(() => 
        this.filesCount() 
        + this.foldersCount()
        + this.uploadsCount());

    selectedItemsCount = computed(() => 
        this.selectedFilesCount() 
        + this.selectedFoldersCount()
        + this.selectedUploadsCount());

    isAnyItemSelected = computed(() => this.selectedItemsCount() > 0);

    isAnyItemNotSelected = computed(() => this.selectedItemsCount() < this.itemsCount());

    canBulkDownload = computed(() =>
        !this.isAnyUploadSelected() 
        && (this.isAnyFolderSelected() || this.isAnyFileSelected())
        && this.allowDownload());

    canBulkDelete = computed(() => 
        (this.isAnyFolderSelected() && this.allowFolderDelete())
        || (this.isAnyFileSelected() && this.allowFileDelete())
        || this.isAnyUploadSelected()
        || (this.isAnyFileSelected() && this.filesStats().selectedCount == this.filesStats().selectedUploadedByUserCount));
    
    canBulkTreeDownload = computed(() => {
        if(!this.allowDownload())
            return false;

        const treeSelectionState = this.treeSelectionState();

        return treeSelectionState.selectedFileExternalIds.length > 0
            || treeSelectionState.selectedFolderExternalIds.length > 0;
    });

    canBulkTreeDelete = computed(() => {
        const allowFileDelete = this.allowFileDelete();
        const allowFolderDelete = this.allowFolderDelete();

        if(!allowFileDelete && !allowFolderDelete)
            return false;
        
        const treeSelectionState = this.treeSelectionState();

        //exclusion is not allowed for bulk delete
        if(treeSelectionState.excludedFileExternalIds.length > 0)
            return false;
        
        if(treeSelectionState.excludedFolderExternalIds.length > 0)
            return false;

        //needs appropriate permissions
        if(treeSelectionState.selectedFileExternalIds.length > 0 && !allowFileDelete)
            return false;

        if(treeSelectionState.selectedFolderExternalIds.length > 0 && !allowFolderDelete)
            return false;

        return treeSelectionState.selectedFileExternalIds.length > 0
            || treeSelectionState.selectedFolderExternalIds.length > 0;
    });

    isAnyNameEditPending = computed(() => this.foldersStats().isAnyNameEditing || this.filesStats().isAnyNameEditing);
    isAnyItemCut = computed(() => this.cutItems().length > 0);

    canUpload = computed(() => this.allowUpload() && this.uploadsApi() != null);
    canCutItems = computed(() => this.isAnyItemSelected() && this.allowMoveItems());
    canPasteItems = computed(() => this.isAnyItemCut() && this.allowMoveItems());

    hasFiles = computed(() => this.filesCount() > 0);
    hasUploads = computed(() => this.uploadsCount() > 0);
    hasFolders = computed(() => this.foldersCount() > 0);
    hasAnyItem = computed(() => this.itemsCount() > 0);

    isEmptyMessageVisible = computed(() => this.showEmptyFolderMessaage() && this.itemsCount() == 0);

    dragCounter = 0;
    isDragging = signal(false);

    selectedFolder = signal<AppFolderItem | null>(null);

    folders: WritableSignal<AppFolderItem[]> = signal([]);
    files: WritableSignal<AppFileItem[]> = signal([]);
    uploads: WritableSignal<AppUploadItem[]> = signal([]);
    cutItems: WritableSignal<ExplorerItem[]> = signal([]);

    explorerTreeItems = computed(() => [...this.folders(), ...this.files()]);
    
    treeSelectionState = signal<FileTreeSelectionState>({
        excludedFileExternalIds: [],
        excludedFolderExternalIds: [],
        selectedFileExternalIds: [],
        selectedFolderExternalIds: []
    });

    isTreeSelectionSummaryLoading = signal(false);

    treeSelectionSummary = signal<CountSelectedItemsResponse>({
        selectedFilesCount: 0,
        selectedFoldersCount: 0,
        totalSizeInBytes: 0
    });

    treeSearchPhrase = signal<string>('');
    treeSearchedFilesSelection = signal<SearchedFilesSelection | null>(null);

    fileInPreview: WritableSignal<AppFileItem | null> = signal(null);
    
    fileInPreviewCanEdit = computed(() => {
        const file = this.fileInPreview();
        
        if(!file)
            return false;

        return AppFileItems.canEdit(file, this.allowFileEdit());
    });

    fileInPreviewIsEditMode = signal(false);

    pendingBulkUpload: WritableSignal<BulkFileUpload | null> = signal(null);
    totalBulkUploadSize = computed(() => this.pendingBulkUpload()?.archive()?.fileSize ?? 0);

    previousForPreview = computed(() => this.getPreviousFileForPreview(this.fileInPreview()));
    isPreviousForPreviewAvailable = computed(() => this.previousForPreview() != null);
    isMouseOverPreviewPreviousBtn = signal(false);

    nextForPreview = computed(() => this.getNextFileForPreview(this.fileInPreview()));
    isNextForPreviewAvailable = computed(() => this.nextForPreview() != null);
    isMouseOverPreviewNextBtn = signal(false);

    operations: FolderOperations & FileOperations & FilePreviewOperations = {
        saveFolderNameFunc: async (folderExternalId: string | null, newName: string) => {
            if (!folderExternalId)
                return;

            await this.filesApi().updateFolderName(folderExternalId, {
                name: newName
            });
        },

        prefetchFolderFunc: (folderExternalId: string | null) => {
            this.prefetchFolder(folderExternalId);
        },

        openFolderFunc: (folderExternalId: string | null) => {
            this.openFolderByExternalId(folderExternalId);
        },

        deleteFolderFunc: async (folderExternalId: string | null) => {
            if (!folderExternalId)
                return;

            const result = await this.filesApi().bulkDelete([], [folderExternalId], []);

            if(result.newWorkspaceSizeInBytes != null) {
                this.workspaceSizeUpdated.emit(result.newWorkspaceSizeInBytes);
            }
        },

        saveFileNameFunc: (fileExternalId: string, newName: string) => 
            this.filesApi().updateFileName(fileExternalId, {name: newName}),

        deleteFileFunc: async (fileExternalId: string) => {            
            const result = await this.filesApi().bulkDelete([fileExternalId], [], []);
            
            if(result.newWorkspaceSizeInBytes != null) {
                this.workspaceSizeUpdated.emit(result.newWorkspaceSizeInBytes);
            }
        },

        getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => 
            this.filesApi().getDownloadLink(fileExternalId, contentDisposition),

        getFilePreviewDetails: (fileExternalId: string, fields: FilePreviewDetailsField[] | null) => 
            this.filesApi().getFilePreviewDetails(fileExternalId, fields),

        updateFileNote: (fileExternalId: string, noteContentJson: string) => 
            this.filesApi().updateFileNote(fileExternalId, noteContentJson),

        createFileComment: (fileExternalId: string, comment: {externalId: string, contentJson: string}) => 
            this.filesApi().createFileComment(fileExternalId, comment),

        delefeFileComment: (fileExternalId: string, commentExternalId: string) =>
            this.filesApi().delefeFileComment(fileExternalId, commentExternalId),

        updateFileComment: (fileExternalId: string, comment: {externalId: string, updatedContentJson: string}) => 
            this.filesApi().updateFileComment(fileExternalId, comment),

        getZipPreviewDetails: (fileExternalId: string) =>
            this.filesApi().getZipPreviewDetails(fileExternalId),
        
        getZipContentDownloadLink: (fileExternalId: string, zipEntry: ZipEntry, contentDisposition: ContentDisposition) =>
            this.filesApi().getZipContentDownloadLink(fileExternalId, zipEntry, contentDisposition),

        startTextractJob: (request: StartTextractJobRequest) =>
            this.filesApi().startTextractJob(request),

        updateFileContent: (fileExternalId: string, file:Blob) =>
            this.filesApi().updateFileContent(fileExternalId, file),

        uploadFileAttachment: (fileExternalId: string, request: UploadFileAttachmentRequest) =>
            this.filesApi().uploadFileAttachment(fileExternalId, request),

        sendAiFileMessage: (fileExternalId: string, request: SendAiFileMessageRequest) =>
            this.filesApi().sendAiFileMessage(fileExternalId, request),

        updateAiConversationName: (fileExternalId: string, fileArtifactExternalId: string, requetst: UpdateAiConversationNameRequest) =>
            this.filesApi().updateAiConversationName(fileExternalId, fileArtifactExternalId, requetst),

        deleteAiConversation: (fileExternalId: string, fileArtifactExternalId: string) =>
            this.filesApi().deleteAiConversation(fileExternalId, fileArtifactExternalId),

        getAiMessages: (fileExternalId: string, fileArtifactExternalId: string, fromConversationCounter: number) =>
            this.filesApi().getAiMessages(fileExternalId, fileArtifactExternalId, fromConversationCounter),
        
        getAllAiMessages: (fileExternalId: string, fileArtifactExternalId: string) =>
            this.filesApi().getAllAiMessages(fileExternalId, fileArtifactExternalId),

        prefetchAiMessages: (fileExternalId: string, fileArtifactExternalId: string) =>
            this.filesApi().prefetchAiMessages(fileExternalId, fileArtifactExternalId)
    }

    textractJobStatusService = new TextractJobStatusService(
        this.filesApi);

    private wasTopFolderLoaded = false;

    private _uploadsCompletedSubscription: Subscription | null = null;
    private _uploadsInitiatedSubscription: Subscription | null = null;
    private _uploadsAbortedSubscription: Subscription | null = null;
    private _workspaceSizeUpdatedSubscription: Subscription | null = null;

    viewMode = signal<ViewMode>('list-view');
    treeViewMode = signal<TreeViewMode>('show-all');

    fileInlinePreviewCommandsPipeline = new FileInlinePreviewCommandsPipeline()
    isFileInPreviewBeingSaved = signal(false);

    @ViewChild(BulkUploadPreviewComponent) bulkUploadPreview!: BulkUploadPreviewComponent;
    @ViewChild(FileTreeViewComponent) fileTreeView!: FileTreeViewComponent;

    constructor(
        public fileUploadManager: FileUploadManager,
        private _renderer: Renderer2) {
    }

    ngOnInit(): void {
        this._renderer.listen('window', 'dragenter', this.onDragEnter.bind(this));
        this._renderer.listen('window', 'dragleave', this.onDragLeave.bind(this));
        this._renderer.listen('window', 'dragover', this.onDragOver.bind(this));
        this._renderer.listen('window', 'drop', this.onDrop.bind(this));
        this._renderer.listen('window', 'keydown', this.handleKeyDown.bind(this));

        this._uploadsCompletedSubscription = this.fileUploadManager.uploadCompleted.subscribe({
            next: (uploadCompletedEvent) => this.onUploadCompleted(uploadCompletedEvent)
        });

        this._uploadsInitiatedSubscription = this.fileUploadManager.uploadsInitiated.subscribe({
            next: (uploadsInitiatedEvent) => this.onUploadsInitiated(uploadsInitiatedEvent)
        });

        this._uploadsAbortedSubscription = this.fileUploadManager.uploadsAborted.subscribe({
            next: (uploadsAbortedEvent) => this.onUploadsAborted(uploadsAbortedEvent)
        });

        this._workspaceSizeUpdatedSubscription = this.fileUploadManager.workspaceSizeUpdated.subscribe({
            next: (workspaceSizeUpdatedEvent) => this.workspaceSizeUpdated.emit(workspaceSizeUpdatedEvent.newWorkpsaceSizeInBytes)
        });
    }

    ngOnDestroy(): void {
        this._uploadsCompletedSubscription?.unsubscribe();
        this._uploadsInitiatedSubscription?.unsubscribe();
        this._uploadsAbortedSubscription?.unsubscribe();
        this._workspaceSizeUpdatedSubscription?.unsubscribe();
    }

    private onDragEnter(event: DragEvent): void {
        event.preventDefault();
        this.dragCounter++;

        this.isDragging.set(true);
    }

    private onDragLeave(event: DragEvent): void {
        event.preventDefault();

        if (this.dragCounter > 0)
            this.dragCounter--;

        if (this.dragCounter === 0)
            this.isDragging.set(false);
    }

    private onDragOver(event: DragEvent): void {
        event.preventDefault();
        this.isDragging.set(true);
    }

    private onDrop(event: DragEvent): void {
        event.preventDefault();
        this.dragCounter = 0;
        this.isDragging.set(false);
    }

    private _loadingPromise: Promise<void> | null = null;
    async ngOnChanges(changes: SimpleChanges) {
        const currentFolderChange = changes['currentFolderExternalId'];
        const currentFileInPreviewChange = changes['currentFileExternalIdInPreview'];
        const apisChange = changes['apis'];
        const itemToHighlightChange = changes['itemToHighlight'];

        if (apisChange) {
            this.wasTopFolderLoaded = false;
        }

        if (this._loadingPromise) {
            await this._loadingPromise;
            this._loadingPromise = null;
        }

        if (currentFolderChange || apisChange) {
            this._loadingPromise = this.openFolderByExternalId(this.currentFolderExternalId());
            await this._loadingPromise;
            this._loadingPromise = null;
        }

        if(currentFileInPreviewChange) {
            const file = this
                .files()
                .find(f => f.externalId == this.currentFileExternalIdInPreview()) ?? null;

            this.setFileInPreview(file);
        }

        const itemToHighlight = this.itemToHighlight();

        if (itemToHighlightChange && itemToHighlight) {
            if (itemToHighlight.type == 'folder') {
                const folder = this.folders().find(
                    (f) => f.externalId === itemToHighlight.externalId);

                if (folder) {
                    folder.isHighlighted.set(true);
                }
            }

            if (itemToHighlight.type == 'file') {
                const file = this.files().find(
                    (f) => f.externalId === itemToHighlight.externalId);

                if (file) {
                    file.isHighlighted.set(true);
                }
            }
        }
    }

    private async loadTopFoldersAndFiles() {
        try {
            this.isLoadingFolders.set(true);

            await this.loadTopFolders();

            this.folderSelected.emit(null);
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoadingFolders.set(false);
        }
    }

    private async loadFolderAndFiles(folderExternalId: string) {
        try {
            this.isLoadingFolders.set(true);
            await this.loadFolder(folderExternalId);
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoadingFolders.set(false);
        }
    }

    private async getTopFolder() {
        const initialContent = this.initialContent();

        if (initialContent
            && !this._wasInitialContentLoaded
            && initialContent.folder?.externalId == this.topFolderExternalId()) {
            this._wasInitialContentLoaded = true;
            return initialContent;
        }

        const folderResponse = await this
            .filesApi()
            .getTopFolders();

        return folderResponse;
    }

    private async loadTopFolders() {
        const folderResponse = await this.getTopFolder();

        if (!folderResponse)
            return;

        //czy to wyciagnaie parentaexternalid ma tutaj jakikolwiek sens? przedebugować
        const selectedFolder = folderResponse.folder
            ? mapFolderDtoToItem(
                folderResponse.folder,
                folderResponse.folder.ancestors)
            : null;

        this.selectedFolder.set(selectedFolder);

        const ancestors = folderResponse.folder
                ? [...(folderResponse.folder.ancestors ?? []), {
                    externalId: folderResponse.folder.externalId,
                    name: folderResponse.folder.name
                }]
                : [];

        this.folders.set(mapFolderDtosToItems(
            folderResponse.subfolders,
            ancestors));

        this.files.set(mapFileDtosToItems(
            folderResponse.files,
            folderResponse.folder?.externalId ?? null));

        this.uploads.set(mapUploadDtosToItems(
            folderResponse.uploads,
            folderResponse.folder?.externalId ?? null));

        this.wasTopFolderLoaded = true;
    }

    private async getFolder(folderExternalId: string): Promise<GetFolderResponse> {
        const initialContent = this.initialContent();
        
        if (initialContent
            && !this._wasInitialContentLoaded
            && initialContent.folder
            && initialContent.folder.externalId === folderExternalId) {
            this._wasInitialContentLoaded = true;

            return initialContent as GetFolderResponse;
        }

        const folderResponse = await this
            .filesApi()
            .getFolder(folderExternalId);

        return folderResponse;
    }

    private async loadFolder(folderExternalId: string) {
        const folderResponse = await this.getFolder(
            folderExternalId);

        const { selectedFolder, subfolders, files, uploads } = mapGetFolderResponseToItems(
            this.topFolderExternalId() ?? null,
            folderResponse);
       
        this.selectedFolder.set(
            selectedFolder);

        this.folders.set(subfolders);

        this.folderSelected.emit(
            this.selectedFolder());

        this.files.set(files);

        this.uploads.set(uploads);

        this.wasTopFolderLoaded = false;
    }

    async createNewFolder() {
        try {
            const selectedFolder = this.selectedFolder();

            const ancestors = selectedFolder 
                ? [...(selectedFolder.ancestors ?? []), {
                    externalId: selectedFolder.externalId,
                    name: selectedFolder.name()
                }]
                : [];

            const newFolder: AppFolderItem = {
                type: 'folder',
                externalId: `fo_${getBase62Guid()}`,
                name: signal('untitled folder'),
                ancestors: ancestors,
                isSelected: signal(false),
                isNameEditing: signal(true),
                isCut: signal(false),
                isHighlighted: signal(false),
                wasCreatedByUser: true,
                createdAt: new Date()
            };

            this.folders.update(values => [...values, newFolder]);

            const result = await this.filesApi().createFolder({
                externalId: newFolder.externalId,
                parentExternalId: selectedFolder?.externalId ?? null,
                name: newFolder.name(),
                ensureUniqueName: false
            });
        } catch (error) {
            console.error(error);
        }
    }

    async openFolderByExternalId(folderExternalId: string | null) {
        if (this.isAnyNameEditPending())
            return;

        const selectedFolder = this.selectedFolder();

        if (selectedFolder != null && selectedFolder.externalId === folderExternalId){
            return;
        }

        if (folderExternalId === null && this.wasTopFolderLoaded){            
            return;
        }

        if (!folderExternalId) {
            await this.loadTopFoldersAndFiles();
            this.closeFilePreview();
            this.closePendingBulkUpload();
        } else {
            await this.loadFolderAndFiles(folderExternalId);
            this.closeFilePreview();
            this.closePendingBulkUpload();
        }
    }

    onFolderDeleted(folder: AppFolderItem) {
        this.folders.update(values => values.filter(f => f.externalId !== folder.externalId));
    }

    onFileDeleted(file: AppFileItem) {
        this.files.update(values => values.filter(f => f.externalId !== file.externalId))
    }

    onFilePreviewed(file: AppFileItem) {
        this.setFileInPreview(file);
    }

    getNextFileForPreview(current: AppFileItem | null) {        
        if(!current) 
            return null;

        const files = this.files();
        const index = files.indexOf(current);

        for(let i = index + 1; i < files.length; i++) {
            const file = files[i];

            if(AppFileItems.canPreview(file, this.allowDownload())) {
                return file;
            }
        }

        return null;    
    }

    showNextInPreview() {
        if(this.isNextForPreviewAvailable()){
            this.setFileInPreview(this.nextForPreview());
        }
    }

    getPreviousFileForPreview(current: AppFileItem | null) {        
        if(!current) 
            return null;

        const files = this.files();

        const index = files.indexOf(current);

        for(let i = index - 1; i >= 0; i--) {
            const file = files[i];

            if(AppFileItems.canPreview(file, this.allowDownload())) {
                return file;
            }
        }

        return null;    
    }

    showPreviousInPreview() {
        if(this.isPreviousForPreviewAvailable()) {
            this.setFileInPreview(this.previousForPreview());
        }
    }

    closeFilePreview() {
        this.setFileInPreview(null);
    }

    closePendingBulkUpload() {
        this.pendingBulkUpload.set(null);
    }

    setFileInPreview(file: AppFileItem | null) {
        const currentFileInPreview = this.fileInPreview();

        if(this.areFilesTheSame(file, currentFileInPreview))
            return;

        this.fileInPreviewIsEditMode.set(false);
        this.fileInPreview.set(file);
        this.filePreviewed.emit(file);
    }

    private areFilesTheSame(file1: AppFileItem | null, file2: AppFileItem | null) {
        if(!file1 && !file2)
            return true;
        
        if(!file1)
            return false;

        if(!file2)
            return false;

        return file1.externalId == file2.externalId;
    }

    onFilesDropped(files: File[]) {
        const uploadsApi = this.uploadsApi();

        if(!uploadsApi)
            return;

        const filesToUpload: FileToUpload[] = [];
        
        for (const file of files) {
            filesToUpload.push({
                folderExternalId: this.selectedFolder()?.externalId ?? null,
                contentType: file.type,
                name: file.name,
                size: file.size,
                slicer: new FileSlicer(file)
            });
        }   

        this.fileUploadManager.addFiles(filesToUpload, uploadsApi);
    }

    onFilesSelected(event: any, fileUpload: HTMLInputElement) {        
        try {
            const uploadsApi = this.uploadsApi();

            if(!uploadsApi)
                return;
            
            const files: File[] = event.target.files;        
            const filesToUpload: FileToUpload[] = [];
                    
            for (const file of files) {
                filesToUpload.push({
                    folderExternalId: this.selectedFolder()?.externalId ?? null,
                    contentType: file.type,
                    name: file.name,
                    size: file.size,
                    slicer: new FileSlicer(file)
                });
            }        
            
            this.fileUploadManager.addFiles(filesToUpload, uploadsApi);
        } finally {            
            fileUpload.value = "";
        }
    }

    cutSelectedItems() {
        this.cutItems.set([
            ...this.folders().filter((f) => f.isSelected()),
            ...this.files().filter((f) => f.isSelected()),
            ...this.uploads().filter((f) => f.isSelected())
        ])

        this.cutItems().forEach((item) => {
            item.isSelected.set(false);
            item.isCut.set(true)
        });
    }

    async pasteCutItems() {
        const cutItems = this.cutItems();

        if (this.areCutItemsOnThisPage(cutItems)) {
            this.cutItems().forEach((item) => item.isCut.set(false));
            this.cutItems.set([]);

            return;
        }

        try {
            this.isMoving.set(true);

            const files = cutItems.filter((item) => item.type === 'file') as AppFileItem[];
            const folders = cutItems.filter((item) => item.type === 'folder') as AppFolderItem[];
            const uploads = cutItems.filter((item) => item.type === 'upload') as AppUploadItem[];

            const promise = this.filesApi().moveItems({
                fileExternalIds: files.map((f) => f.externalId).filter((f) => f !== null) as string[],
                folderExternalIds: folders.map((f) => f.externalId).filter((f) => f !== null) as string[],
                fileUploadExternalIds: uploads.map((f) => f.externalId).filter((f) => f !== null) as string[],
                destinationFolderExternalId: this.selectedFolder()?.externalId ?? null,
            });

            this.folders.update(values => [...values, ...folders]);
            this.files.update(values => [...values, ...files]);
            this.uploads.update(values => [...values, ...uploads]);

            cutItems.forEach((item) => item.isCut.set(false));
            this.cutItems.set([]);

            this.filesApi().invalidatePrefetchedEntries();

            await promise;

            this.isMoving.set(false);
        } catch (error) {
            console.error(error);
        } finally {
            this.isMoving.set(false);
        }
    }

    areCutItemsOnThisPage(cutItems: ExplorerItem[]) {
        return cutItems.some((item) =>
            (item.type == 'file' && this.files().includes(item))
            || (item.type == 'folder' && this.folders().includes(item))
            || (item.type == 'upload' && this.uploads().includes(item)));
    }

    public createBoxFromFolder(folder: AppFolderItem) {
        this.boxCreated.emit(folder);
    }

    public onUploadAborted(upload: AppUploadItem) {
        this.uploads.update(values => values.filter(u => u.externalId !== upload.externalId));
    }

    public onUploadsInitiated(event: UploadsInitiatedEvent) {
        const currentFolderExternalId = this.selectedFolder()?.externalId ?? null;
        
        const uploadsForCurrentFolder = event
            .uploads
            .filter(u => u.folderExternalId === currentFolderExternalId);
        
        this.uploads.update(values => [...values, ...uploadsForCurrentFolder]);
    }

    public onUploadCompleted(event: UploadCompletedEvent) {
        const upload = this
            .uploads()
            .find(u => u.externalId === event.uploadExternalId);

        if(!upload)
            return;

        this.uploads.update(values => values.filter(u => u.externalId !== event.uploadExternalId));

        const newFile: AppFileItem = {
            type: 'file',
            externalId: event.fileExternalId,
            folderExternalId: this.selectedFolder()?.externalId ?? null,
            name: upload.fileName,
            extension: upload.fileExtension,
            sizeInBytes: upload.fileSizeInBytes,
            wasUploadedByUser: true,
            folderPath: null,
            isLocked: signal(true),

            isSelected: signal(false),
            isNameEditing: signal(false),
            isCut: signal(false),
            isHighlighted: signal(false)
        };

        this.files.update(values => [...values, newFile]);
    }

    public onUploadsAborted(event: UploadsAbortedEvent) {
        this.uploads.update(values => values.filter(u => !event.uploadExternalIds.includes(u.externalId)));
    }

    public prefetchTopFolders() {
        this.filesApi().prefetchTopFolders();
    }

    public prefetchFolder(folderExternalId: string | null) {
        if (folderExternalId) {
            this.filesApi().prefetchFolder(folderExternalId);
        } else {
            this.filesApi().prefetchTopFolders();
        }
    }

    public startUpload(fileUpload: HTMLInputElement) {
        fileUpload.click();
    }

    public runBulkUpload() {
        if(!this.bulkUploadPreview)
            return;

        this.bulkUploadPreview.runBulkUpload();
    }

    public startBulkUpload() {
        const archive = signal<SingleBulkFileUpload | null>(null);

        this.pendingBulkUpload.set({
            archive: archive,
            isStarted: signal(false),
            isCompleted: signal(false),
            isUploadEnabled: computed(() => {
                const archivesVal = archive();

                if(!archivesVal)
                    return false;
                
                return !archivesVal.isBroken() && archivesVal.archive() != null;
            })
        });
    }

    async deleteSelectedItems() {
        const filesToDelete = this
            .files()
            .filter((f) => f.isSelected())
            .map(f => f.externalId);

        const foldersToDelete = this
            .folders()
            .filter(f => f.isSelected())
            .map(f => f.externalId);

        const uploadsToDelete = this
            .uploads()
            .filter(f => f.isSelected())
            .map(f => f.externalId);

        this.files.update(values => values.filter(f => filesToDelete.indexOf(f.externalId) == -1))
        
        this.folders.update(values => values.filter((f) => foldersToDelete.indexOf(f.externalId) == -1));
        this.uploads.update(values => values.filter((f) => uploadsToDelete.indexOf(f.externalId) == -1));


        if (uploadsToDelete.length > 0) {
            await this.fileUploadManager.abortUploads(
                uploadsToDelete);
        }

        const result = await this.filesApi().bulkDelete(
            filesToDelete,
            foldersToDelete,
            uploadsToDelete
        );

        if(result.newWorkspaceSizeInBytes != null) {
            this.workspaceSizeUpdated.emit(result.newWorkspaceSizeInBytes);
        }
    }

    async deleteFile(file: AppFileItem) {
        this.fileInPreviewIsEditMode.set(false);
        this.fileInPreview.set(null);

        this.files.update(values => values.filter(f => f.externalId !== file.externalId))

        const result = await this.filesApi().bulkDelete(
            [file.externalId], [], []
        );
        
        if(result.newWorkspaceSizeInBytes != null) {
            this.workspaceSizeUpdated.emit(result.newWorkspaceSizeInBytes);
        }
    }

    async downloadSelectedItems() {        
        const selectedFiles = this
            .files()
            .filter(f => f.isSelected());

        const selectedFolders = this
            .folders()
            .filter(f => f.isSelected());

        const preSignedUrl = await this.getPreSignedUrl(
            selectedFiles,
            selectedFolders);

        const link = document.createElement('a');
        link.href = preSignedUrl.url;
        link.download = preSignedUrl.downloadName;
        link.click();
        link.remove();
    }

    async saveFileName(file: AppFileItem, newName: string) {
        file.name.set(newName);

        await this.operations.saveFileNameFunc(
            file.externalId,
            newName);
    }

    async downloadFile(file: AppFileItem) {
        const preSignedUrl = await this.getPreSignedUrl(
            [file], []);

        const link = document.createElement('a');
        link.href = preSignedUrl.url;
        link.download = preSignedUrl.downloadName;
        link.click();
        link.remove();
    }

    private async getPreSignedUrl(files: AppFileItem[], folders: AppFolderItem[]): Promise<{url: string, downloadName: string}> {
        if(files.length == 1 && folders.length == 0) {
            //bulk download call is not needed, so we simply do a normal file download
            //which is better because its faster, and also for S3 storages it is using presigned links
            //which saves egress/ingress of server

            const file = files[0];

            const response = await this.filesApi().getDownloadLink(
                file.externalId,
                "attachment");

            return {
                url: response.downloadPreSignedUrl,
                downloadName: file.name() + file.extension
            };
        }

        const fileExternalIds = files
            .map(f => f.externalId);

        const folderExternalIds = folders
            .map(f => f.externalId);

        const response = await this.filesApi().getBulkDownloadLink({
            selectedFiles: fileExternalIds,
            selectedFolders: folderExternalIds,
            excludedFiles: [],
            excludedFolders: []
        });
        
        const timestamp = new Date().toISOString().split('T')[0];
      
        return {
            url: response.preSignedUrl,
            downloadName: `bulk_download_${timestamp}.zip`
        };
    }

    private handleKeyDown(event: KeyboardEvent): void {
        if (event.ctrlKey && event.key.toLowerCase() === 'a') {
            event.preventDefault();
            
            if (!this.isAnyNameEditPending()) {
                this.selectAllItems();
            }
        } else if (event.key === 'Escape' && this.isAnyItemSelected()) {
            this.clearSelection();
        }
    }

    public toggleAllFolders() {
        if(this.isAnyFolderNotSelected()) {
            this.selectAllFolders();
        } else {
            this.clearFoldersSelection();
        }
    }

    public selectAllFolders() {
        this.folders().forEach(fo => fo.isSelected.set(true));
    }

    public clearFoldersSelection() {
        this.folders().forEach(fo => fo.isSelected.set(false));
    }

    public toggleAllFiles(){
        if(this.isAnyFileNotSelected()) {
            this.selectAllFiles();
        } else {
            this.clearFilesSelection();
        }
    }

    public selectAllFiles() {
        this.files().forEach(fi => fi.isSelected.set(true));
    }

    public clearFilesSelection() {
        this.files().forEach(fi => fi.isSelected.set(false));
    }

    public toggleAllUploads() {
        if(this.isAnyUploadNotSelected()) {
            this.selectAllUploads();
        } else {
            this.clearUploadsSelection();
        }
    }

    public selectAllUploads() {        
        this.uploads().forEach(u => u.isSelected.set(true));
    }

    public clearUploadsSelection() {
        this.uploads().forEach(u => u.isSelected.set(false));
    }

    private selectAllItems(): void {
        this.selectAllFolders();
        this.selectAllFiles();
        this.selectAllUploads();
    }

    private clearSelection(): void {
        this.clearFoldersSelection();
        this.clearFilesSelection();
        this.clearUploadsSelection();
    }

    public toggleAllItemsSelection() {
        if(this.isAnyItemNotSelected()) {            
            this.selectAllItems();
        } else {
            this.clearSelection();
        }
    }

    private getSelectionStats(items: {isSelected: WritableSignal<boolean>; isNameEditing?: WritableSignal<boolean>;}[]): {
        count: number, 
        selectedCount: number,
        isAnyNameEditing: boolean,
    } {
        let selectedCount = 0;
        let isAnyNameEditing = false;
        let ownedByUserCount = 0;

        for (const item of items) {
            if(item.isSelected())
                selectedCount ++;

            if(!isAnyNameEditing && item.isNameEditing && item.isNameEditing())
                isAnyNameEditing = true;
        }

        return  {
            count: items.length,
            selectedCount: selectedCount,
            isAnyNameEditing: isAnyNameEditing
        };
    }

    private getFilesStats(files: AppFileItem[]): {
        count: number, 
        selectedCount: number,
        isAnyNameEditing: boolean,
        uploadedByUserCount: number,
        selectedUploadedByUserCount: number
    } {
        let selectedCount = 0;
        let isAnyNameEditing = false;
        let uploadedByUserCount = 0;
        let selectedUploadedByUserCount = 0;
    
        for (const file of files) {
            if(file.isSelected())
                selectedCount ++;
    
            if(file.wasUploadedByUser)
                uploadedByUserCount ++;
    
            if(file.wasUploadedByUser && file.isSelected())
                selectedUploadedByUserCount ++;
    
            if(file.isNameEditing())
                isAnyNameEditing = true;
        }
        
        return  {
            count: files.length,
            selectedCount: selectedCount,
            isAnyNameEditing: isAnyNameEditing,
            uploadedByUserCount: uploadedByUserCount,
            selectedUploadedByUserCount: selectedUploadedByUserCount
        };
    }

    addFoldersCreatedInBulkUpload(folders: CreatedFolder[]) {
        const selectedFolder = this.selectedFolder();

        const newFolderItems: AppFolderItem[] = [];

        const ancestors = selectedFolder
            ? [...selectedFolder.ancestors, {
                externalId: selectedFolder.externalId, 
                name: selectedFolder.name()
            }]
            : [];

        for (const folder of folders) {
            newFolderItems.push({
                type: 'folder',
                externalId: folder.externalId,
                name: signal(folder.name),
                ancestors: ancestors,
                createdAt: new Date(),
                isCut: signal(false),
                isHighlighted: signal(false),
                isNameEditing: signal(false),
                isSelected: signal(false),
                wasCreatedByUser: true
            });
        }

        this.folders.update(values => [...values, ...newFolderItems]);
    }

    async setViewMode(viewMode: ViewMode) {
        this.viewMode.set(viewMode);
    }


    onFolderTreePrefetchRequested(folder: AppFolderItem) {
        this.filesApi().prefetchFolder(folder.externalId);
    }

    async onFolderTreeLoadRequested(request: LoadFolderNodeRequest) {

        const folderResponse = await this.filesApi().getFolder(
            request.folder.externalId);

        const { selectedFolder, subfolders, files, uploads } = mapGetFolderResponseToItems(
            this.topFolderExternalId() ?? null,
            folderResponse);
        
        request.folderLoadedCallback(
            [...subfolders, ...files]);
    }

    onFolderTreeSetToRoot(folder: AppFolderItem) {
        this.operations.openFolderFunc(
            folder.externalId, 
            null);
    }

    async onDownloadFile(file: AppFileItem) {
        if(file.isLocked())
            return;

        const response = await this.operations.getDownloadLink(
            file.externalId,
            "attachment");

        const link = document.createElement('a');
        link.href = response.downloadPreSignedUrl;
        link.download = `${file.name()}${file.extension}`;
        link.click();
        link.remove();
    }

    private _fileTreeSelectionStateDebouncer = new Debouncer(100);
    async onFileTreeSelectionStateChanged(state: FileTreeSelectionState) {
        this.treeSelectionState.set(state);

        this._fileTreeSelectionStateDebouncer.debounceAsync(async () => {
            this.isTreeSelectionSummaryLoading.set(true);

            try {
                const api = this.filesApi();       
    
                const result = await api.countSelectedItems({
                    selectedFiles: state.selectedFileExternalIds,
                    selectedFolders: state.selectedFolderExternalIds,
                    excludedFiles: state.excludedFileExternalIds,
                    excludedFolders: state.excludedFolderExternalIds
                });
        
                this.treeSelectionSummary.set(result);                
            } catch (error) {
                console.error(error);
            } finally {
                this.isTreeSelectionSummaryLoading.set(false);
            }
        });
    }

    async downloadSelectedTreeItems() {                
        const treeSelectionState = this.treeSelectionState();

        if(treeSelectionState.selectedFileExternalIds.length == 0 && treeSelectionState.selectedFolderExternalIds.length == 0)
            return;

        const response = await this.filesApi().getBulkDownloadLink({
            selectedFiles: treeSelectionState.selectedFileExternalIds,
            selectedFolders: treeSelectionState.selectedFolderExternalIds,
            excludedFiles: treeSelectionState.excludedFileExternalIds,
            excludedFolders: treeSelectionState.excludedFolderExternalIds
        });
        
        const timestamp = new Date().toISOString().split('T')[0];    

        const link = document.createElement('a');
        link.href = response.preSignedUrl;
        link.download = `bulk_download_${timestamp}.zip`;
        link.click();
        link.remove();
    }

    triggerSelectedTreeItemsDelete() {
        if(!this.fileTreeView)
            return;

        this.fileTreeView.deleteSelectedItems();
    }

    async onFileTreeSelectedItemsDelete(state: FileTreeDeleteSelectionState) {
        this.files.update(values => values.filter(f => state.selectedFileExternalIds.indexOf(f.externalId) == -1))        
        this.folders.update(values => values.filter((f) => state.selectedFolderExternalIds.indexOf(f.externalId) == -1));

        const result = await this.filesApi().bulkDelete(
            state.selectedFileExternalIds,
            state.selectedFolderExternalIds,
            []
        );       

        if(result.newWorkspaceSizeInBytes != null) {
            this.workspaceSizeUpdated.emit(result.newWorkspaceSizeInBytes);
        }
    }

    async onTreeSearchRequested(request: FileTreeSearchRequest) {
        const api = this.filesApi();

        const result = await api.searchFilesTree({
            folderExternalId: this.selectedFolder()?.externalId ?? null,
            phrase: request.phrase
        });

        request.callback(result);
    }

    onTreeSearchedFilesSelectionChanged(event: SearchedFilesSelection | null) {
        this.treeSearchedFilesSelection.set(event);

        if(event == null) {
            this.treeSearchPhrase.set('');
        }
    }

    toggleTreeSearchedFilesSelection() {
        if(!this.fileTreeView)
            return;

        this.fileTreeView.toggleSearchedFilesSelection();
    }

    cancelTreeSelection() {
        if(!this.fileTreeView)
            return;

        this.fileTreeView.cancelSelection();
    }

    fileInPreviewCancelContentChanges() {
        this.fileInPreviewIsEditMode.set(false);
        this.fileInlinePreviewCommandsPipeline.emit({
            type: 'cancel-content-change'
        });
    }

    fileInPreviewSaveContentChanges() {
        const fileInPreview = this.fileInPreview();

        if(!fileInPreview)
            return;

        this.fileInlinePreviewCommandsPipeline.emit({
            type: 'save-content-change',
            callback: async (content: string, contentType: string) => {
                try {
                    this.isFileInPreviewBeingSaved.set(true);

                    const api = this.filesApi();
                    const file: Blob = new Blob([content], { type: contentType});
    
                    await api.updateFileContent(
                        fileInPreview.externalId, 
                        file);

                    this.fileInPreviewIsEditMode.set(false);
                } finally {
                    this.isFileInPreviewBeingSaved.set(false);                    
                }
            }
        });
    }
}