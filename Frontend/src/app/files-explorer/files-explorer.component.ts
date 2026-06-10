import { AfterViewInit, Component, ElementRef, HostListener, OnChanges, OnDestroy, OnInit, Renderer2, SimpleChanges, ViewChild, WritableSignal, computed, effect, input, output, signal, untracked } from '@angular/core';
import { FileToUpload, FileUploadApi, FileUploadManager, UploadsAbortedEvent, UploadCompletedEvent, UploadsInitiatedEvent } from '../services/file-upload-manager/file-upload-manager';
import { AppUploadItem, UploadItemComponent } from './upload-item/upload-item.component';
import { ConfirmOperationDirective } from '../shared/operation-confirm/confirm-operation.directive';
import { AppFolderItem, AppFolderPermissions, FolderOperations } from '../shared/folder-item/folder-item.component';
import { FoldersListComponent } from './folders-list/folders-list.component';
import { AppFileItem, AppFileItems, AppFilePermissions, FileOperations } from '../shared/file-item/file-item.component';
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
import { StorageSizePipe, StorageSizeUtils } from '../shared/storage-size.pipe';
import { EditableTxtComponent } from '../shared/editable-txt/editable-txt.component';
import { BulkUploadPreviewComponent, BulkFileUpload, SingleBulkFileUpload, CreatedFolder } from './bulk-upload-preview/bulk-upload-preview.component';
import { BulkCreateFolderRequest, BulkCreateFolderResponse, BulkDeleteResponse, CheckTextractJobsStatusRequest, CheckTextractJobsStatusResponse, ContentDisposition, CountSelectedItemsRequest, CountSelectedItemsResponse, CountThumbnailableFilesRequest, CountThumbnailableFilesResponse, CreateFolderRequest, CreateFolderResponse, CurrentFolderDto, DownloadImageFormat, FileDto, FilePreviewDetailsField, GetAiMessagesResponse, GetBulkDownloadLinkRequest, GetBulkDownloadLinkResponse, GetFileDownloadLinkResponse, GenerateFileThumbnailsResponse, GenerateThumbnailsBulkRequest, GenerateThumbnailsBulkResponse, FileProcessingEvent, GetFilePreviewDetailsResponse, GetFolderResponse, GetZipBulkDownloadLinkRequest, GetZipBulkDownloadLinkResponse, mapFileDtosToItems, mapFolderDtosToItems, mapFolderDtoToItem, mapGetFolderResponseToItems, mapUploadDtosToItems, SearchFilesTreeRequest, SearchFilesTreeResponse, SendAiFileMessageRequest, SortDirection, SortMode, StartTextractJobRequest, StartTextractJobResponse, SubfolderDto, ThumbnailGenerationStatus, ThumbnailVariant, UpdateAiConversationNameRequest, UpdatePositionsRequest, UploadDto, UploadFileAttachmentRequest, UploadFileThumbnailRequest } from '../services/folders-and-files.api';
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
import { DragStateService } from '../services/drag-state.service';
import { ActivatedRoute, Router } from '@angular/router';
import { SortChange } from './sort-menu/sort-menu.component';
import { DisplayMenuComponent } from './display-menu/display-menu.component';
import { computePositionForInsertion } from '../shared/drag-drop/item-positioning.utils';
import { FilesListComponent } from './files-list/files-list.component';
import { ThumbnailProgressComponent } from './thumbnail-progress/thumbnail-progress.component';
import { SelectionCountComponent } from './selection-count/selection-count.component';
import { thumbnailListDisplay } from '../services/thumbnail-list-display';
import { trackStuckSection } from '../services/track-stuck-section';
import { ThumbnailBatchProgressService } from '../services/thumbnail-batch-progress.service';
import { FileProcessingService } from '../services/file-processing.service';
import { AppCapabilitiesService } from '../services/app-capabilities.service';
import { getFileDetails } from '../services/file-type';

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
        destinationFolderExternalId: string | null,
        destinationPosition?: number | null
    }) => Promise<void>;
    updatePositions: ((request: UpdatePositionsRequest) => Promise<void>) | null;
    updateFileName: (fileExternalId: string, request: { name: string }) => Promise<void>;
    getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => Promise<GetFileDownloadLinkResponse>;
    // Override the default workspace-scoped thumbnail URL. Box/external contexts supply their own
    // path (eg. `/api/access-codes/{code}/files/{id}/thumbnail`); workspace leaves this undefined
    // and falls back to the per-workspace builder in `thumbnailListDisplay`.
    getThumbnailUrl?: (fileExternalId: string) => string;
    // Toggles the "Image" / "Video" preview section (thumbnails grid, metadata, download-as,
    // generate). False in read-only box/external contexts where thumbnail management isn't
    // exposed; true in workspace where the owner manages them.
    isMediaSectionAvailable: boolean;
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
    uploadFileThumbnail: (fileExternalId: string, request: UploadFileThumbnailRequest) => Promise<void>;
    deleteFileThumbnail: (fileExternalId: string, variant: ThumbnailVariant) => Promise<void>;
    generateFileThumbnails: (fileExternalId: string, variants: ThumbnailVariant[]) => Promise<GenerateFileThumbnailsResponse>;
    generateBulkThumbnails: (request: GenerateThumbnailsBulkRequest) => Promise<GenerateThumbnailsBulkResponse>;
    countThumbnailableFiles: (request: CountThumbnailableFilesRequest) => Promise<CountThumbnailableFilesResponse>;
    subscribeThumbnailBatch: (
        batchId: string,
        onStatus: (status: ThumbnailGenerationStatus) => void) => () => void;
    cancelThumbnailBatch: (batchId: string) => Promise<unknown>;
    // Live per-file processing stream (spinners). Optional — only workspace contexts expose the
    // file-processing endpoint; box/external explorers leave this undefined and show no spinners.
    subscribeFileProcessing?: (onEvent: (event: FileProcessingEvent) => void) => () => void;
    downloadFileConverted: (fileExternalId: string, format: DownloadImageFormat, downloadFileName: string) => Promise<void>;

    getZipPreviewDetails: (fileExternalId: string) => Promise<ZipPreviewDetails>;
    getZipContentDownloadLink: (fileExternalId: string, zipEntry: ZipEntry, contentDisposition: ContentDisposition) => Promise<GetFileDownloadLinkResponse>;
    getZipBulkDownloadLink: (fileExternalId: string, request: GetZipBulkDownloadLinkRequest) => Promise<GetZipBulkDownloadLinkResponse>;

    startTextractJob(request: StartTextractJobRequest): Promise<StartTextractJobResponse>;
    checkTextractJobsStatus(request: CheckTextractJobsStatusRequest): Promise<CheckTextractJobsStatusResponse>;

    sendAiFileMessage(fileExternalId: string, request: SendAiFileMessageRequest): Promise<void>;
    updateAiConversationName(fileExternalId: string, fileArtifactExternalId: string, request: UpdateAiConversationNameRequest): Promise<void>;
    deleteAiConversation(fileExternalId: string, fileArtifactExternalId: string): Promise<void>;
    getAiMessages(fileExternalId: string, fileArtifactExternalId: string, fromConversationCounter: number): Promise<GetAiMessagesResponse>;
    getAllAiMessages(fileExternalId: string, fileArtifactExternalId: string): Promise<GetAiMessagesResponse>;
    prefetchAiMessages(fileExternalId: string, fileArtifactExternalId: string): void;

    subscribeToLockStatus: (file: AppFileItem) => void;
    unsubscribeFromLockStatus: (fileExternalId: string) => void;    

    prepareAdditionalHttpHeaders: () => Record<string, string> | undefined;
}

export type InitialContent = {
    folder: CurrentFolderDto | null;
    subfolders: SubfolderDto[];
    files: FileDto[];
    uploads: UploadDto[];
}

export type QuickShareSelection = {
    selectedFiles: string[];
    selectedFolders: string[];
    excludedFiles: string[];
    excludedFolders: string[];
    defaultName: string;
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
        FoldersListComponent,
        FilesListComponent,
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
        ItemSearchComponent,
        DisplayMenuComponent,
        ThumbnailProgressComponent,
        SelectionCountComponent
    ],
    templateUrl: './files-explorer.component.html',
    styleUrl: './files-explorer.component.scss',
    providers: [FileProcessingService]
})
export class FilesExplorerComponent implements OnChanges, OnInit, OnDestroy, AfterViewInit  {
    filesApi = input.required<FilesExplorerApi>();
    uploadsApi = input.required<FileUploadApi | null>();
    currentFolderExternalId = input.required<string | null>();
    currentFileExternalIdInPreview = input.required<string | null>();
    initialContent = input.required<InitialContent | null>();
    topFolderExternalId = input<string>();
    constHeightMode = input<boolean>(false);
    scrollToTopOnOpen = input<boolean>(false);

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
    allowQuickShare = input(false);

    folderPermissions = computed<AppFolderPermissions>(() =>({
        allowDelete: this.allowFolderDelete(),
        allowDownload: this.allowDownload(),
        allowMoveItems: this.allowMoveItems(),
        allowRename: this.allowFolderRename(),
        allowShare: this.allowFolderShare()
    }));
    
    filePermissions = computed<AppFilePermissions>(() => ({
        allowDelete: this.allowFileDelete(),
        allowDownload: this.allowDownload(),
        allowMoveItems: this.allowMoveItems(),
        allowRename: this.allowFileRename()
    }));

    hideContextBar = input(false);
    hideFiles = input(false);
    hideItemsActions = input(false);
    hideItemShareAction = input(false);
    hideReorder = input(false);
    hideBigAddFolderBtn = input(false);
    hideSelectCheckboxes = input(false);

    integrations = input<WorkspaceIntegrations>({textract: null, chatGpt:[]});

    canSelectAll = computed(() => this.hasAnyItem() && !this.hideSelectCheckboxes() && this.canSelectItems());
    canSelectItems = computed(() => this.allowMoveItems() || this.allowDownload() || this.allowFileDelete() || this.allowFileDelete());

    showEmptyFolderMessaage = input(false);

    itemToHighlight = input<ItemToHighlight | null>();

    workspaceExternalId = input<string | null>(null);
    allowDateSort = input(false);

    initialViewMode = input<ViewMode>('list-view');
    initialSortMode = input<SortMode>('custom');
    initialSortDirection = input<SortDirection>('asc');
    initialShowThumbnails = input<boolean>(false);

    disablePreferencePersistence = input<boolean>(false);

    // Opt-in: when true this explorer reflects/restores the list/tree view in
    // the page URL (?view=). Only the routed workspace explorer should set it —
    // embedded instances (dialogs, external access) must not touch the URL.
    syncViewModeToUrl = input(false);

    // When the workspace's trash policy is on, deleting a file is recoverable — the
    // delete-confirmation dialog reflects that instead of "cannot be reverted".
    isTrashEnabled = input(false);

    deleteConfirmSubtitle = computed(() => this.isTrashEnabled()
        ? 'Deleted files are moved to the trash and can be restored from there.'
        : 'This operation cannot be reverted.');

    sortMode = signal<SortMode>('custom');
    sortDirection = signal<SortDirection>('asc');

    // Opt-in mini-thumbnail rendering on list rows (persisted per workspace) + thumbnail URL builder.
    private _thumbnailDisplay = thumbnailListDisplay(this.workspaceExternalId, this.initialShowThumbnails, this.disablePreferencePersistence);
    readonly showThumbnails = this._thumbnailDisplay.showThumbnails;

    // Files with queue work in flight (any job type, any trigger — bulk action, upload, other
    // users) — drives the per-row spinner. Fed by the workspace file-processing channel.
    readonly processingFileIds = computed(() => this._fileProcessing.processingFileIds());

    // Cache-busters for files whose thumbnail generation just finished — fed to the tree-view so
    // its own file nodes (sub-folders / search results) refresh live, same as the list.
    readonly readyMiniEtags = computed(() => this._fileProcessing.refreshedMiniEtags());

    private static readonly SORT_MODE_STORAGE_PREFIX = 'plikshare:sort-mode:';

    folderSelected = output<AppFolderItem | null>();
    boxCreated = output<AppFolderItem>();
    quickShareRequested = output<QuickShareSelection>();
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

    canBulkQuickShare = computed(() =>
        this.allowQuickShare()
        && !this.isAnyUploadSelected()
        && (this.isAnyFolderSelected() || this.isAnyFileSelected()));

    // A box wraps exactly one folder, so the toolbar button only makes sense
    // when the user has a single folder selected and nothing else.
    canCreateBoxFromSelection = computed(() =>
        this.allowFolderShare()
        && this.selectedFoldersCount() === 1
        && this.selectedFilesCount() === 0
        && this.selectedUploadsCount() === 0);

    canBulkTreeQuickShare = computed(() => {
        if (!this.allowQuickShare())
            return false;

        const treeSelectionState = this.treeSelectionState();
        return treeSelectionState.selectedFileExternalIds.length > 0
            || treeSelectionState.selectedFolderExternalIds.length > 0;
    });

    canBulkDelete = computed(() =>
        (this.isAnyFolderSelected() && this.allowFolderDelete())
        || (this.isAnyFileSelected() && this.allowFileDelete())
        || this.isAnyUploadSelected()
        || (this.isAnyFileSelected() && this.filesStats().selectedCount == this.filesStats().selectedUploadedByUserCount));

    canGenerateThumbnails = computed(() => {
        if (!this._capabilities.capabilities().isFfmpegAvailable)
            return false;

        if (this.workspaceExternalId() == null)
            return false;

        if (this.isAnyUploadSelected())
            return false;

        if (this.isAnyFolderSelected())
            return true;

        if (!this.isAnyFileSelected())
            return false;

        return this.files()
            .filter(file => file.isSelected())
            .every(file => {
                const type = getFileDetails(file.extension).type;
                return type === 'image' || type === 'video';
            });
    });

    // Tree-view counterpart: the server resolves the include/exclude tree selection and filters
    // thumbnailability itself, so any selected file/folder is enough to offer the action.
    canBulkTreeGenerateThumbnails = computed(() => {
        if (!this._capabilities.capabilities().isFfmpegAvailable)
            return false;

        if (this.workspaceExternalId() == null)
            return false;

        const treeSelectionState = this.treeSelectionState();

        return treeSelectionState.selectedFileExternalIds.length > 0
            || treeSelectionState.selectedFolderExternalIds.length > 0;
    });

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

    isAnyItemCut = computed(() => this.cutItems().length > 0);

    canUpload = computed(() => this.allowUpload() && this.uploadsApi() != null);
    canCutItems = computed(() => this.isAnyItemSelected() && this.allowMoveItems());
    canPasteItems = computed(() => this.isAnyItemCut() && this.allowMoveItems());

    hasFiles = computed(() => this.filesCount() > 0);
    hasUploads = computed(() => this.uploadsCount() > 0);
    hasFolders = computed(() => this.foldersCount() > 0);
    hasAnyItem = computed(() => this.itemsCount() > 0);

    isFoldersExpanded = signal(true);
    isFilesExpanded = signal(true);

    private uploadSelectionAnchorExternalId: string | null = null;

    onUploadSelectionToggled(upload: AppUploadItem) {
        if (upload.isSelected()) {
            const current = this.uploadSelectionAnchorExternalId;

            if (current == null) {
                this.uploadSelectionAnchorExternalId = upload.externalId;
                return;
            }

            const currentAnchor = this.uploads().find(u => u.externalId === current);

            if (currentAnchor == null || !currentAnchor.isSelected()) {
                this.uploadSelectionAnchorExternalId = upload.externalId;
            }
            return;
        }

        if (this.uploadSelectionAnchorExternalId !== upload.externalId)
            return;

        const firstSelected = this.uploads().find(u => u.isSelected());
        this.uploadSelectionAnchorExternalId = firstSelected?.externalId ?? null;
    }

    onUploadShiftClicked(upload: AppUploadItem) {
        if (!this.uploadSelectionAnchorExternalId) {
            upload.isSelected.update(v => !v);
            this.onUploadSelectionToggled(upload);
            return;
        }
        this.applyRangeSelection(this.uploads(), this.uploadSelectionAnchorExternalId, upload.externalId);
    }

    private applyRangeSelection<T extends { externalId: string, isSelected: WritableSignal<boolean> }>(
        list: T[], anchorExternalId: string, targetExternalId: string
    ) {
        const anchorIdx = list.findIndex(i => i.externalId === anchorExternalId);
        const targetIdx = list.findIndex(i => i.externalId === targetExternalId);
        
        if (anchorIdx === -1 || targetIdx === -1) 
            return;
        
        const [from, to] = anchorIdx <= targetIdx 
            ? [anchorIdx, targetIdx] 
            : [targetIdx, anchorIdx];
        
        list.forEach((item, idx) => {
            const inRange = idx >= from && idx <= to;
            if (item.isSelected() !== inRange) item.isSelected.set(inRange);
        });
    }

    toggleFoldersExpanded = () => this.isFoldersExpanded.update(v => !v);
    toggleFilesExpanded = () => this.isFilesExpanded.update(v => !v);
    expandFoldersSection = () => this.isFoldersExpanded.set(true);
    expandFilesSection = () => this.isFilesExpanded.set(true);

    //if there are no files to show there is no point to show folder bar
    //because only folders are visible anyway
    showFolderBar = computed(() => this.showFilesSection());

    showFoldersSection = computed(() => this.hasFolders() || this.allowCreateFolder());

    showFilesSection = computed(() => !this.hideFiles() 
        && (this.hasFiles() || this.hasUploads() || this.canUpload()));

    isEmptyMessageVisible = computed(() => this.showEmptyFolderMessaage() && this.itemsCount() == 0);

    dragCounter = 0;
    isDragging = signal(false);

    selectedFolder = signal<AppFolderItem | null>(null);

    folders: WritableSignal<AppFolderItem[]> = signal([]);
    files: WritableSignal<AppFileItem[]> = signal([]);
    uploads: WritableSignal<AppUploadItem[]> = signal([]);
    cutItems: WritableSignal<ExplorerItem[]> = signal([]);  
        
    filteredFoldersCount = computed(() => {
        const phrase = this.searchPhrase().toLowerCase();
        if (!phrase) return this.folders().length;
        return this.folders().filter(f => f.name().toLowerCase().includes(phrase)).length;
    });

    filteredFilesCount = computed(() => {
        const phrase = this.searchPhrase().toLowerCase();
        if (!phrase) return this.files().length;
        return this.files().filter(f => (f.name() + f.extension).toLowerCase().includes(phrase)).length;
    });

    isSearchActive = computed(() => this.searchPhrase().length > 0);

    canReorder = computed(() => 
        this.sortMode() === 'custom' 
        && this.filesApi().updatePositions != null 
        && !this.isSearchActive()
        && !this.hideReorder());

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

    searchPhrase = signal<string>('');
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

    // Mobile-only: when search is expanded, hide the rest of the action row to
    // give the input the full width. Desktop ignores this flag — the search
    // input is always visible there.
    isSearchExpandedOnMobile = signal(false);

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

        getThumbnailUrl: (fileExternalId: string) =>
            this.filesApi().getThumbnailUrl?.(fileExternalId)
                ?? this._thumbnailDisplay.getThumbnailUrl(fileExternalId),

        isMediaSectionAvailable: () => this.filesApi().isMediaSectionAvailable,

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

        getZipBulkDownloadLink: (fileExternalId: string, request: GetZipBulkDownloadLinkRequest) =>
            this.filesApi().getZipBulkDownloadLink(fileExternalId, request),

        startTextractJob: (request: StartTextractJobRequest) =>
            this.filesApi().startTextractJob(request),

        updateFileContent: (fileExternalId: string, file:Blob) =>
            this.filesApi().updateFileContent(fileExternalId, file),

        uploadFileAttachment: (fileExternalId: string, request: UploadFileAttachmentRequest) =>
            this.filesApi().uploadFileAttachment(fileExternalId, request),

        uploadFileThumbnail: (fileExternalId: string, request: UploadFileThumbnailRequest) =>
            this.filesApi().uploadFileThumbnail(fileExternalId, request),

        deleteFileThumbnail: (fileExternalId: string, variant: ThumbnailVariant) =>
            this.filesApi().deleteFileThumbnail(fileExternalId, variant),

        generateFileThumbnails: (fileExternalId: string, variants: ThumbnailVariant[]) =>
            this.filesApi().generateFileThumbnails(fileExternalId, variants),

        subscribeThumbnailBatch: (batchId: string, onStatus) =>
            this.filesApi().subscribeThumbnailBatch(batchId, onStatus),
        cancelThumbnailBatch: (batchId: string) =>
            this.filesApi().cancelThumbnailBatch(batchId),

        downloadFileConverted: (fileExternalId: string, format: DownloadImageFormat, downloadFileName: string) =>
            this.filesApi().downloadFileConverted(fileExternalId, format, downloadFileName),

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
            this.filesApi().prefetchAiMessages(fileExternalId, fileArtifactExternalId),

        subscribeToLockStatus: (file: AppFileItem) =>
            this.filesApi().subscribeToLockStatus(file),

        unsubscribeFromLockStatus: (fileExternalId: string) =>
            this.filesApi().unsubscribeFromLockStatus(fileExternalId),

        prepareAdditionalHttpHeaders: () =>
            this.filesApi().prepareAdditionalHttpHeaders()
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
    @ViewChild(ItemSearchComponent) itemSearch?: ItemSearchComponent;
    @ViewChild('toolbarHeaderEl') toolbarHeaderEl?: ElementRef<HTMLElement>;
    @ViewChild('stickyWrapper') stickyWrapperEl?: ElementRef<HTMLElement>;
    @ViewChild('foldersHeader') foldersHeaderEl?: ElementRef<HTMLElement>;
    @ViewChild('filesHeader') filesHeaderEl?: ElementRef<HTMLElement>;

    isToolbarStacked = signal(false);
    private _toolbarResizeObserver?: ResizeObserver;

    // Which section header has scrolled up under the sticky toolbar — surfaced as a context chip in
    // the toolbar so the active section + its counts stay visible while scrolling a long list.
    readonly stuckSection = trackStuckSection(
        () => this.stickyWrapperEl?.nativeElement,
        [
            { id: 'folders', getElement: () => this.foldersHeaderEl?.nativeElement },
            { id: 'files', getElement: () => this.filesHeaderEl?.nativeElement }
        ]);

    constructor(
        public fileUploadManager: FileUploadManager,
        private _renderer: Renderer2,
        private _dragState: DragStateService,
        private _router: Router,
        private _route: ActivatedRoute,
        private _thumbnailBatches: ThumbnailBatchProgressService,
        private _fileProcessing: FileProcessingService,
        private _capabilities: AppCapabilitiesService,
        private _elementRef: ElementRef<HTMLElement>) {

        effect(() => {
            const key = this.sortStorageKey();
            const stored = (!this.disablePreferencePersistence() && key) ? localStorage.getItem(key) : null;
            const parsed = stored
                ? this.parseStoredSortMode(stored)
                : { mode: this.initialSortMode(), direction: this.initialSortDirection() };
            const dateAllowed = this.allowDateSort();
            const mode = (parsed.mode === 'date' && !dateAllowed) ? 'custom' : parsed.mode;
            const dir = (parsed.mode === 'date' && !dateAllowed) ? 'asc' : parsed.direction;
            this.sortMode.set(mode);
            this.sortDirection.set(dir);
        });

        // Apply cache-busters for freshly-generated thumbnails onto the current file items. The
        // processing channel is workspace-wide, so this works regardless of which session (or
        // which user) started the generation.
        effect(() => {
            const etags = this._fileProcessing.refreshedMiniEtags();

            if (etags.size === 0)
                return;

            for (const file of this.files()) {
                const etag = etags.get(file.externalId);
                if (!etag)
                    continue;

                untracked(() => {
                    const current = file.metadata();
                    if (current?.thumbnail?.miniEtag !== etag)
                        file.metadata.set({
                            thumbnail: { miniEtag: etag },
                            dimensions: current?.dimensions ?? null
                        });
                });
            }
        });


        effect(() => {
            if (this.isDragging() && this.canUpload()) {
                this.expandFilesSection();
            }
        });

        effect(() => {
            if (!this.isSearchActive()) return;
            if (this.filteredFilesCount() > 0) this.expandFilesSection();
        });

        effect(() => {
            const dragged = this._dragState.draggedItem();
            const currentFolder = this.selectedFolder()?.externalId ?? null;

            if (dragged?.type === 'file' && dragged.parentFolderExternalId !== currentFolder) {
                untracked(() => this.expandFilesSection());
            }
        });
    }

    private parseStoredSortMode(stored: string | null): { mode: SortMode, direction: SortDirection } {
        if (stored === 'custom') return { mode: 'custom', direction: 'asc' };
        if (stored === 'name-asc')  return { mode: 'name', direction: 'asc' };
        if (stored === 'name-desc') return { mode: 'name', direction: 'desc' };
        if (stored === 'date-asc')  return { mode: 'date', direction: 'asc' };
        if (stored === 'date-desc') return { mode: 'date', direction: 'desc' };
        return { mode: 'custom', direction: 'asc' };
    }

    private sortStorageKey(): string | null {
        const wsId = this.workspaceExternalId();
        if (!wsId) return null;
        return `${FilesExplorerComponent.SORT_MODE_STORAGE_PREFIX}${wsId}`;
    }

    onSortChanged(change: SortChange) {
        if (change.mode === 'date' && !this.allowDateSort()) return;
        this.sortMode.set(change.mode);
        this.sortDirection.set(change.direction);
        this.persistSort(change.mode, change.direction);
    }

    private persistSort(mode: SortMode, direction: SortDirection) {
        if (this.disablePreferencePersistence()) return;
        const key = this.sortStorageKey();
        if (!key) return;
        const value = mode === 'custom' ? 'custom' : `${mode}-${direction}`;
        localStorage.setItem(key, value);
    }

    onShowThumbnailsChanged(value: boolean) {
        this._thumbnailDisplay.setShowThumbnails(value);
    }


    @HostListener('document:dragend')
    onDocumentDragEnd() {
        // Drop handlers stop the drag with 'success' and clear state first;
        // anything still set here means the drag ended without a drop.
        this._dragState.stopDragging({ reason: 'canceled' });
    }

    ngOnInit(): void {
        // App-wide capability flag (drives the bulk "Generate thumbnails" action). Only fetched
        // when the host can manage media — /api/app-capabilities is internal-only, so embedded
        // contexts (box-widget / external) would otherwise 401 on it. filesApi (a required input)
        // is available here in ngOnInit but not in the constructor.
        if (this.filesApi().isMediaSectionAvailable) {
            this._capabilities.ensureLoaded();
        }

        // Restore the list/tree view from the URL on load (survives refresh).
        // Guarded by syncViewModeToUrl so embedded instances ignore the page's
        // query params.
        const urlView = this.syncViewModeToUrl()
            ? this._route.snapshot.queryParamMap.get('view')
            : null;

        this.viewMode.set(
            urlView === 'tree-view' || urlView === 'list-view'
                ? urlView
                : this.initialViewMode());

        this._renderer.listen('window', 'dragenter', this.onDragEnter.bind(this));
        this._renderer.listen('window', 'dragleave', this.onDragLeave.bind(this));
        this._renderer.listen('window', 'dragover', this.onDragOver.bind(this));
        this._renderer.listen('window', 'drop', this.onDrop.bind(this));
        this._renderer.listen('window', 'keydown', this.handleKeyDown.bind(this));

        this._uploadsCompletedSubscription = this.fileUploadManager.uploadCompleted.subscribe({
            next: (uploadCompletedEvent) => this.onUploadCompleted(uploadCompletedEvent)
        });

        // Resurrect bulk thumbnail batches persisted before a page reload — but only ones that
        // belong to THIS explorer's workspace. The service is transport-agnostic, so each owning
        // explorer wires in its own handlers (subscribe / cancel).
        const wsId = this.workspaceExternalId();
        if (wsId != null) {
            const handlers = this.thumbnailBatchHandlers();
            for (const persisted of this._thumbnailBatches.getPersistedBatches()) {
                if (persisted.workspaceExternalId === wsId) {
                    this._thumbnailBatches.resume({ ...persisted, handlers });
                }
            }
        }

        // Live per-file processing stream (spinners + thumbnail refresh). Workspace contexts
        // expose it on their api; box/external ones don't — they simply show no spinners.
        const api = this.filesApi();
        if (api.subscribeFileProcessing) {
            this._fileProcessing.connect({
                subscribe: onEvent => api.subscribeFileProcessing!(onEvent),
            });
        }

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

    ngAfterViewInit(): void {
        const headerEl = this.toolbarHeaderEl?.nativeElement;
        if (!headerEl) return;

        this._toolbarResizeObserver = new ResizeObserver(() => this.measureToolbar());
        this._toolbarResizeObserver.observe(headerEl);
        this.measureToolbar();
    }

    ngOnDestroy(): void {
        this._uploadsCompletedSubscription?.unsubscribe();
        this._uploadsInitiatedSubscription?.unsubscribe();
        this._uploadsAbortedSubscription?.unsubscribe();
        this._workspaceSizeUpdatedSubscription?.unsubscribe();
        this._toolbarResizeObserver?.disconnect();
        this._fileProcessing.disconnect();
    }

    // Content-driven stacking: sum the rendered widths of the action items and
    // compare to the container width. When path + actions can't both fit, drop
    // actions to their own row. Avoids the magic-number breakpoint problem —
    // the threshold shifts as actions appear/disappear based on selection state.
    private measureToolbar(): void {
        const headerEl = this.toolbarHeaderEl?.nativeElement;
        if (!headerEl) return;

        const actionsEl = headerEl.querySelector('.title-header__actions-row') as HTMLElement | null;
        if (!actionsEl) return;

        const GAP_PX = 8;             // matches gap: 0.5rem on actions-row
        const COLUMN_GAP_PX = 8;      // matches column-gap on grid container
        const MIN_PATH_WIDTH_PX = 120; // smallest path display we consider usable
        const SAFETY_BUFFER_PX = 24;  // sub-pixel rounding + last-segment overshoot

        const items = actionsEl.querySelectorAll('app-action-btn, app-item-search, mat-checkbox');
        let actionsWidth = 0;
        let visibleCount = 0;
        items.forEach((node) => {
            const el = node as HTMLElement;
            if (el.offsetParent === null && el.offsetWidth === 0) return;
            actionsWidth += el.offsetWidth;
            visibleCount++;
        });
        if (visibleCount > 1) actionsWidth += (visibleCount - 1) * GAP_PX;

        const containerWidth = headerEl.offsetWidth;
        const needsStack = containerWidth < MIN_PATH_WIDTH_PX + COLUMN_GAP_PX + actionsWidth + SAFETY_BUFFER_PX;

        if (this.isToolbarStacked() !== needsStack) {
            this.isToolbarStacked.set(needsStack);
        }
    }

    private hasOsFiles(event: DragEvent): boolean {
        const types = event.dataTransfer?.types;
        if (!types) return false;
        for (let i = 0; i < types.length; i++) {
            if (types[i] === 'Files') return true;
        }
        return false;
    }

    private onDragEnter(event: DragEvent): void {
        if (!this.hasOsFiles(event)) return;
        event.preventDefault();
        this.dragCounter++;

        this.isDragging.set(true);
    }

    private onDragLeave(event: DragEvent): void {
        if (!this.hasOsFiles(event)) return;
        event.preventDefault();

        if (this.dragCounter > 0)
            this.dragCounter--;

        if (this.dragCounter === 0)
            this.isDragging.set(false);
    }

    private onDragOver(event: DragEvent): void {
        if (!this.hasOsFiles(event)) return;
        event.preventDefault();
        this.isDragging.set(true);
    }

    private onDrop(event: DragEvent): void {
        if (!this.hasOsFiles(event)) {
            this.dragCounter = 0;
            this.isDragging.set(false);
            return;
        }
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

            const currentFolders = this.folders();
            const newPosition = computePositionForInsertion(
                currentFolders,
                currentFolders.length,
                item => item.position()
            );

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
                createdAt: new Date(),
                position: signal(newPosition)
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
        const selectedFolder = this.selectedFolder();

        if (selectedFolder != null && selectedFolder.externalId === folderExternalId){
            return;
        }

        if (folderExternalId === null && this.wasTopFolderLoaded){
            return;
        }

        // Reset the search phrase when navigating to a different folder so the user
        // does not see a filtered/highlighted view from a previous context.
        this.searchPhrase.set('');

        if (!folderExternalId) {
            await this.loadTopFoldersAndFiles();
            this.closeFilePreview();
            this.closePendingBulkUpload();
        } else {
            await this.loadFolderAndFiles(folderExternalId);
            this.closeFilePreview();
            this.closePendingBulkUpload();
        }

        if(this.scrollToTopOnOpen()) {
            this.scrollContainerToTop();
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

        // Widget (scrollToTopOnOpen) keeps its original on-open scroll untouched; every other
        // host gets the conditional scroll that only moves when the image isn't already fully
        // in view.
        if(file) {
            if(this.scrollToTopOnOpen()) {
                this.scrollContainerToTop();
            } else {
                this.scrollPreviewIntoView();
            }
        }
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
        const filesApi = this.filesApi();

        if(!uploadsApi || !filesApi)
            return;

        const filesToUpload: FileToUpload[] = [];
        
        for (const file of files) {
            filesToUpload.push({
                folderExternalId: this.selectedFolder()?.externalId ?? null,
                contentType: file.type,
                name: file.name,
                size: file.size,
                createSlicer: () => new FileSlicer(file)
            });
        }

        this.fileUploadManager.addFiles(filesToUpload, uploadsApi, filesApi);
    }

    onFilesSelected(event: any, fileUpload: HTMLInputElement) {
        try {
            const uploadsApi = this.uploadsApi();
            const filesApi = this.filesApi();

            if(!uploadsApi || !filesApi)
                return;

            const files: File[] = event.target.files;
            const filesToUpload: FileToUpload[] = [];

            for (const file of files) {
                filesToUpload.push({
                    folderExternalId: this.selectedFolder()?.externalId ?? null,
                    contentType: file.type,
                    name: file.name,
                    size: file.size,
                    createSlicer: () => new FileSlicer(file)
                });
            }
            
            this.fileUploadManager.addFiles(filesToUpload, uploadsApi, filesApi);
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

    createBoxFromSelectedFolder() {
        const selected = this.folders().find(f => f.isSelected());
        if (!selected) return;

        this.createBoxFromFolder(selected);
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
            createdAt: new Date(),
            position: signal(0),
            metadata: signal(null),

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

    public openMobileSearch() {
        this.isSearchExpandedOnMobile.set(true);
        this.itemSearch?.focusInput();
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

    // Handlers the ThumbnailBatchProgressService uses per batch — keeps the service transport-agnostic
    // so each explorer (workspace / box / external-link) can plug in its own API. The handlers
    // capture the current filesApi(), which is stable for the lifetime of an open explorer.
    private thumbnailBatchHandlers() {
        const api = this.filesApi();

        return {
            subscribe: (
                batchId: string,
                onStatus: (status: ThumbnailGenerationStatus) => void) =>
                api.subscribeThumbnailBatch(batchId, onStatus),
            cancel: (batchId: string) => api.cancelThumbnailBatch(batchId),
        };
    }

    private async loadThumbnailGenerationSubtitle(request: CountThumbnailableFilesRequest): Promise<string> {
        const fallback = 'Thumbnails will be generated in the background.';

        if (this.workspaceExternalId() == null)
            return fallback;

        try {
            const result = await this.filesApi().countThumbnailableFiles(request);

            if (result.fileCount === 0)
                return 'No images or videos in the selection — nothing to process.';

            const fileLabel = result.fileCount === 1 ? 'file' : 'files';
            const size = StorageSizeUtils.formatSize(result.totalSizeInBytes);

            return `${result.fileCount} ${fileLabel} (${size}) will be processed in the background.`;
        } catch {
            return fallback;
        }
    }

    thumbnailGenerationSubtitleLoader = async (): Promise<string> => {
        return this.loadThumbnailGenerationSubtitle({
            selectedFiles: this.files().filter(f => f.isSelected()).map(f => f.externalId),
            selectedFolders: this.folders().filter(f => f.isSelected()).map(f => f.externalId),
            excludedFiles: [],
            excludedFolders: []
        });
    };

    thumbnailGenerationTreeSubtitleLoader = async (): Promise<string> => {
        const s = this.treeSelectionState();

        return this.loadThumbnailGenerationSubtitle({
            selectedFiles: s.selectedFileExternalIds,
            selectedFolders: s.selectedFolderExternalIds,
            excludedFiles: s.excludedFileExternalIds,
            excludedFolders: s.excludedFolderExternalIds
        });
    };

    async generateThumbnailsForSelectedItems() {
        const workspaceExternalId = this.workspaceExternalId();

        if (workspaceExternalId == null)
            return;

        const fileExternalIds = this
            .files()
            .filter(f => f.isSelected())
            .map(f => f.externalId);

        const folderExternalIds = this
            .folders()
            .filter(f => f.isSelected())
            .map(f => f.externalId);

        if (fileExternalIds.length === 0 && folderExternalIds.length === 0)
            return;

        // Only the Mini variant is rendered today (list rows). Small/Large are reserved for the
        // future gallery mode and would just triple ffmpeg work + storage with nothing reading them.
        const variants: ThumbnailVariant[] = ['Mini'];

        try {
            const response = await this.filesApi().generateBulkThumbnails({
                selectedFiles: fileExternalIds,
                selectedFolders: folderExternalIds,
                excludedFiles: [],
                excludedFolders: [],
                variants
            });

            this._thumbnailBatches.track({
                workspaceExternalId,
                batchId: response.batchId,
                name: `Generating thumbnails — ${response.totalFiles} file(s)`,
                total: response.totalFiles,
                handlers: this.thumbnailBatchHandlers(),
            });

            // Selection has done its job — clear it so the toolbar returns to its default actions.
            this.files().forEach(f => f.isSelected.set(false));
            this.folders().forEach(f => f.isSelected.set(false));
        } catch (err) {
            console.error('Bulk thumbnail generation failed:', err);
        }
    }

    async generateThumbnailsForTreeSelection() {
        const workspaceExternalId = this.workspaceExternalId();

        if (workspaceExternalId == null)
            return;

        const s = this.treeSelectionState();

        if (s.selectedFileExternalIds.length === 0 && s.selectedFolderExternalIds.length === 0)
            return;

        // Only the Mini variant is rendered today (list rows). Small/Large are reserved for the
        // future gallery mode and would just triple ffmpeg work + storage with nothing reading them.
        const variants: ThumbnailVariant[] = ['Mini'];

        try {
            const response = await this.filesApi().generateBulkThumbnails({
                selectedFiles: s.selectedFileExternalIds,
                selectedFolders: s.selectedFolderExternalIds,
                excludedFiles: s.excludedFileExternalIds,
                excludedFolders: s.excludedFolderExternalIds,
                variants
            });

            this._thumbnailBatches.track({
                workspaceExternalId,
                batchId: response.batchId,
                name: `Generating thumbnails — ${response.totalFiles} file(s)`,
                total: response.totalFiles,
                handlers: this.thumbnailBatchHandlers(),
            });

            this.cancelTreeSelection();
        } catch (err) {
            console.error('Bulk thumbnail generation failed:', err);
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

    shareSelectedItems() {
        const selectedFiles = this.files().filter(f => f.isSelected());
        const selectedFolders = this.folders().filter(f => f.isSelected());

        this.quickShareRequested.emit({
            selectedFiles: selectedFiles.map(f => f.externalId),
            selectedFolders: selectedFolders.map(f => f.externalId),
            excludedFiles: [],
            excludedFolders: [],
            defaultName: this.buildDefaultShareName(selectedFiles.length, selectedFolders.length)
        });
    }

    shareSelectedTreeItems() {
        const state = this.treeSelectionState();

        this.quickShareRequested.emit({
            selectedFiles: state.selectedFileExternalIds,
            selectedFolders: state.selectedFolderExternalIds,
            excludedFiles: state.excludedFileExternalIds,
            excludedFolders: state.excludedFolderExternalIds,
            defaultName: this.buildDefaultShareName(
                state.selectedFileExternalIds.length,
                state.selectedFolderExternalIds.length)
        });
    }

    private buildDefaultShareName(filesCount: number, foldersCount: number): string {
        const parts: string[] = [];
        if (foldersCount > 0) parts.push(`${foldersCount} folder${foldersCount > 1 ? 's' : ''}`);
        if (filesCount > 0) parts.push(`${filesCount} file${filesCount > 1 ? 's' : ''}`);
        return parts.length > 0 ? `Quick share — ${parts.join(', ')}` : 'Quick share';
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
            // Skip the global select-all when the user is typing in an input —
            // Ctrl+A there should select the input's text, not toggle items.
            const target = event.target;
            if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) {
                return;
            }

            event.preventDefault();
            this.selectAllItems();
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

    // Toggle selection of only the search-visible folders/files — the "visible" count segment in
    // the section header. Leaves hidden (filtered-out) selections untouched.
    public toggleVisibleFolders() {
        this.toggleSelectionOf(this.searchVisibleFolders());
    }

    public toggleVisibleFiles() {
        this.toggleSelectionOf(this.searchVisibleFiles());
    }

    private searchVisibleFolders(): AppFolderItem[] {
        const phrase = this.searchPhrase().toLowerCase();
        if (!phrase) return this.folders();
        return this.folders().filter(f => f.name().toLowerCase().includes(phrase));
    }

    private searchVisibleFiles(): AppFileItem[] {
        const phrase = this.searchPhrase().toLowerCase();
        if (!phrase) return this.files();
        return this.files().filter(f => (f.name() + f.extension).toLowerCase().includes(phrase));
    }

    private toggleSelectionOf(items: { isSelected: WritableSignal<boolean> }[]) {
        if (items.length === 0) return;
        const allSelected = items.every(i => i.isSelected());
        items.forEach(i => i.isSelected.set(!allSelected));
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

    private getSelectionStats(items: {isSelected: WritableSignal<boolean>;}[]): {
        count: number,
        selectedCount: number,
    } {
        let selectedCount = 0;

        for (const item of items) {
            if(item.isSelected())
                selectedCount ++;
        }

        return  {
            count: items.length,
            selectedCount: selectedCount,
        };
    }

    private getFilesStats(files: AppFileItem[]): {
        count: number,
        selectedCount: number,
        uploadedByUserCount: number,
        selectedUploadedByUserCount: number
    } {
        let selectedCount = 0;
        let uploadedByUserCount = 0;
        let selectedUploadedByUserCount = 0;

        for (const file of files) {
            if(file.isSelected())
                selectedCount ++;

            if(file.wasUploadedByUser)
                uploadedByUserCount ++;

            if(file.wasUploadedByUser && file.isSelected())
                selectedUploadedByUserCount ++;
        }

        return  {
            count: files.length,
            selectedCount: selectedCount,
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
                wasCreatedByUser: true,
                position: signal(0)
            });
        }

        this.folders.update(values => [...values, ...newFolderItems]);
    }

    async setViewMode(viewMode: ViewMode) {
        this.viewMode.set(viewMode);

        // Reflect the choice in the URL so a refresh keeps tree/list. Only when
        // this explorer owns the page route — embedded instances (dialogs,
        // external access) must not mutate the page URL.
        if (this.syncViewModeToUrl()) {
            this._router.navigate([], {
                relativeTo: this._route,
                queryParams: { view: viewMode },
                queryParamsHandling: 'merge',
                replaceUrl: true
            });
        }
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
            this.searchPhrase.set('');
        }
    }

    onTreeSearchActivated() {
        this.scrollContainerToTop();
    }

    private scrollContainerToTop() {
        const container = this._elementRef.nativeElement;
        requestAnimationFrame(() => {
            if (container.getBoundingClientRect().top < 0) {
                container.style.scrollMarginTop = '16px';
                container.scrollIntoView({ block: 'start', behavior: 'instant' });
            }
        });
    }

    // Brings a just-opened preview into view ONLY when it isn't already comfortably visible.
    // The preview's toolbar lives in a position:sticky panel pinned at the top, so the image
    // must land below that panel (its height is the offset) — aligning to y=0 would tuck the
    // image's top edge under it. Waits two frames (Angular swaps list->preview AND image-preview
    // reserves its skeleton height in ngAfterViewInit) so it measures the final layout.
    private scrollPreviewIntoView() {
        const host = this._elementRef.nativeElement;

        requestAnimationFrame(() =>
            requestAnimationFrame(() => {
                const target = host.querySelector<HTMLElement>('.image-preview-frame')
                    ?? host.querySelector<HTMLElement>('.file-content');

                if (!target)
                    return;

                const stickyHeight = this.stickyWrapperEl?.nativeElement.getBoundingClientRect().height ?? 0;
                const rect = target.getBoundingClientRect();
                const viewportHeight = window.innerHeight;

                const topClearOfPanel = rect.top >= stickyHeight;
                const fitsInView = rect.bottom <= viewportHeight;
                const tooTallToFit = (rect.bottom - rect.top) > (viewportHeight - stickyHeight);
                const alreadyAtPanel = Math.abs(rect.top - stickyHeight) <= 1;

                // Don't move when it's already comfortable: either the whole image fits below the
                // sticky panel, or it's taller than the available space but already snug under it.
                if (topClearOfPanel && (fitsInView || (tooTallToFit && alreadyAtPanel)))
                    return;

                target.style.scrollMarginTop = `${stickyHeight}px`;
                target.scrollIntoView({ block: 'start', behavior: 'instant' });
            }));
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