import { Component, OnInit, Signal, WritableSignal, computed, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ToastrService } from 'ngx-toastr';
import { HttpErrorResponse } from '@angular/common/http';
import { GetQuickShareBulkDownloadLinkRequest, GetQuickShareContentResponse, GetQuickShareInfoResponse, QuickShareContentFile, QuickShareContentFolder, QuickShareExternalAccessApi } from '../../services/quick-share-external-access.api';
import { collectSelectionFromChildren, EMPTY_TREE_SELECTION, StaticFileNode, StaticFileTreeViewComponent, StaticFolderNode, StaticSearchCorpusEntry, StaticTreeNode, StaticTreeSelection } from '../../shared/static-file-tree-view/static-file-tree-view.component';
import { StorageSizePipe } from '../../shared/storage-size.pipe';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { ActionTextButtonComponent } from '../../shared/buttons/action-text-btn/action-text-btn.component';
import { ItemSearchComponent } from '../../shared/item-search/item-search.component';
import { AppFileForContent, FileContentComponent, FileContentOperations } from '../../files-explorer/file-content/file-content.component';
import { ZipArchives, ZipEntry } from '../../services/zip';

@Component({
    selector: 'app-quick-share',
    imports: [
        DatePipe,
        FormsModule,
        MatButtonModule,
        MatCheckboxModule,
        MatFormFieldModule,
        MatInputModule,
        StaticFileTreeViewComponent,
        StorageSizePipe,
        ActionButtonComponent,
        ActionTextButtonComponent,
        ItemSearchComponent,
        FileContentComponent
    ],
    templateUrl: './quick-share.component.html',
    styleUrl: './quick-share.component.scss'
})
export class QuickShareComponent implements OnInit {
    slug: string | null = null;
    token: string | null = null;

    isInitialLoading = signal(true);
    notFound = signal(false);
    info: WritableSignal<GetQuickShareInfoResponse | null> = signal(null);
    content: WritableSignal<GetQuickShareContentResponse | null> = signal(null);
    fileTree: WritableSignal<StaticTreeNode[]> = signal([]);

    passwordInput = signal('');
    isUnlocking = signal(false);
    unlockError = signal<string | null>(null);
    unlockErrorMatcher: ErrorStateMatcher = {
        isErrorState: () => this.unlockError() !== null
    };
    isDownloading = signal(false);
    searchPhrase = signal('');

    currentFilePreviewId: WritableSignal<string | null> = signal(null);

    previewedFile = computed<QuickShareContentFile | null>(() => {
        const id = this.currentFilePreviewId();
        if (!id) return null;
        return this.content()?.files.find(f => f.externalId === id) ?? null;
    });

    // FileContentComponent expects name as a Signal so it can react to renames.
    // In quick-share the name is immutable for the lifetime of the preview, so
    // we wrap the static string in a plain signal each time the previewed file
    // changes — recomputing the whole AppFileForContent on every change.
    previewFileForContent = computed<AppFileForContent | null>(() => {
        const file = this.previewedFile();
        if (!file) return null;
        return {
            name: signal(file.name),
            extension: file.extension,
            sizeInBytes: file.sizeInBytes
        };
    });

    filePreviewOperations = computed<FileContentOperations | null>(() => {
        const id = this.currentFilePreviewId();
        if (!id) return null;
        return this.buildFileContentOperations(id);
    });

    // FileContentComponent in archive mode only emits (zipEntryClicked) — the
    // parent has to render the actual entry preview. We stack a second
    // <app-file-content> over the zip view when an entry is in preview, mirroring
    // what FileInlinePreviewComponent does in the workspace flow.
    zipEntryInPreview: WritableSignal<ZipEntry | null> = signal(null);

    zipEntryFileForContent = computed<AppFileForContent | null>(() => {
        const entry = this.zipEntryInPreview();
        if (!entry) return null;

        const nameAndExt = ZipArchives.getFileNameAndExtension(entry);
        return {
            name: signal(nameAndExt.name),
            extension: nameAndExt.extension,
            // ZipPreviewComponent uses compressedSizeInBytes for its preview file;
            // mirror that — the inline preview only uses size for "open as text"
            // gating, and compressed size is conservative enough.
            sizeInBytes: entry.compressedSizeInBytes
        };
    });

    zipEntryPreviewOperations = computed<FileContentOperations | null>(() => {
        const fileId = this.currentFilePreviewId();
        const entry = this.zipEntryInPreview();
        if (!fileId || !entry) return null;
        return this.buildZipEntryContentOperations(fileId, entry);
    });

    name = computed(() => this.info()?.name ?? '');
    mode = computed(() => this.info()?.mode ?? 'browser');
    requiresPassword = computed(() => this.info()?.requiresPassword ?? false);
    isUnlocked = computed(() => this.info()?.isUnlocked ?? false);
    isExpired = computed(() => this.info()?.isExpired ?? false);
    isExhausted = computed(() => this.info()?.isExhausted ?? false);
    isOwnerPreview = computed(() => this.info()?.isOwnerPreview ?? false);
    allowIndividualFileDownload = computed(() => this.info()?.allowIndividualFileDownload ?? false);
    totalSizeInBytes = computed(() => this.content()?.totalSizeInBytes ?? 0);
    filesCount = computed(() => this.content()?.files.length ?? 0);

    isReady = computed(() => {
        const info = this.info();
        if (!info) return false;
        if (info.isExpired || info.isExhausted) return false;
        if (info.requiresPassword && !info.isUnlocked) return false;
        return true;
    });

    // Fed by the tree component's (selectionChanged) output. The tree owns the
    // walk + lazy-aware cascade — we just hold the latest payload and map it to
    // the API shape below.
    treeSelection: WritableSignal<StaticTreeSelection> = signal(EMPTY_TREE_SELECTION);

    selectionState = computed<GetQuickShareBulkDownloadLinkRequest>(() => {
        const s = this.treeSelection();
        return {
            selectedFolderExternalIds: s.selectedFolderIds,
            selectedFileExternalIds: s.selectedFileIds,
            excludedFolderExternalIds: s.excludedFolderIds,
            excludedFileExternalIds: s.excludedFileIds
        };
    });

    selectionSummary = computed(() => {
        const s = this.selectionState();
        const total = s.selectedFolderExternalIds.length + s.selectedFileExternalIds.length;
        const isSingleFile = s.selectedFolderExternalIds.length === 0
            && s.selectedFileExternalIds.length === 1
            && s.excludedFolderExternalIds.length === 0
            && s.excludedFileExternalIds.length === 0;

        return {
            count: total,
            isSingleFile: isSingleFile,
            singleFileId: isSingleFile ? s.selectedFileExternalIds[0] : null
        };
    });

    // 'all' = every root is selected with no descendant excludes, 'none' = empty
    // payload, 'some' = anything in between. Drives the master checkbox tri-state.
    selectAllState = computed<'all' | 'some' | 'none'>(() => {
        const tree = this.fileTree();
        if (tree.length === 0) return 'none';

        const s = this.selectionState();
        const total = s.selectedFolderExternalIds.length + s.selectedFileExternalIds.length;
        if (total === 0) return 'none';

        const allRootsSelected = tree.every(n => n.isSelected());
        const hasExcludes = s.excludedFolderExternalIds.length > 0
            || s.excludedFileExternalIds.length > 0;

        return allRootsSelected && !hasExcludes ? 'all' : 'some';
    });

    constructor(
        private _route: ActivatedRoute,
        private _api: QuickShareExternalAccessApi,
        private _toastr: ToastrService
    ) {
    }

    async ngOnInit() {
        this.slug = this._route.snapshot.params['slug'] || null;
        this.token = this._route.snapshot.queryParamMap.get('token');

        if (!this.slug) {
            this.notFound.set(true);
            this.isInitialLoading.set(false);
            return;
        }

        try {
            await this.loadInfo();

            if (this.isReady() && this.mode() === 'direct') {
                await this.downloadAll();
            } else if (this.isReady()) {
                await this.loadContent();
            }
        } finally {
            this.isInitialLoading.set(false);
        }
    }

    private async loadInfo() {
        try {
            const info = await this._api.getInfo(this.slug!, this.token);
            this.info.set(info);
        } catch (error) {
            if (error instanceof HttpErrorResponse && error.status === 404) {
                this.notFound.set(true);
            } else {
                console.error(error);
                this._toastr.error('Failed to load quick share');
            }
        }
    }

    private async loadContent() {
        try {
            const content = await this._api.getContent(
                this.slug!, 
                this.token);

            this.content.set(content);

            const tree = this.buildTree(
                content.folders, 
                content.files);

            this.fileTree.set(tree);
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to load quick share content');
        }
    }

    onPasswordInputChange(value: string) {
        this.passwordInput.set(value);
        this.unlockError.set(null);
    }

    async unlock() {
        if (this.isUnlocking()) return;

        if (!this.passwordInput().trim()) {
            this.unlockError.set('Password is required');
            return;
        }

        try {
            this.isUnlocking.set(true);
            this.unlockError.set(null);

            await this._api.unlock(this.slug!, this.token, { password: this.passwordInput() });
            this.passwordInput.set('');

            await this.loadInfo();

            if (this.isReady() && this.mode() === 'direct') {
                await this.downloadAll();
            } else if (this.isReady()) {
                await this.loadContent();
            }
        } catch (error) {
            if (error instanceof HttpErrorResponse && error.status === 401) {
                this.unlockError.set('Wrong password');
            } else {
                console.error(error);
                this.unlockError.set('Something went wrong. Please try again.');
            }
        } finally {
            this.isUnlocking.set(false);
        }
    }

    async downloadAll() {
        if (this.isDownloading()) return;

        try {
            this.isDownloading.set(true);
            const result = await this._api.getBulkDownloadLink(this.slug!, this.token);

            const link = document.createElement('a');
            link.href = result.preSignedUrl;
            link.click();
            link.remove();

            await this.loadInfo();
        } catch (error) {
            if (error instanceof HttpErrorResponse && error.status === 410) {
                this._toastr.error('Download limit reached');
                await this.loadInfo();
            } else {
                console.error(error);
                this._toastr.error('Download failed');
            }
        } finally {
            this.isDownloading.set(false);
        }
    }

    async downloadSelected() {
        if (this.isDownloading()) return;

        const summary = this.selectionSummary();
        if (summary.count === 0) return;

        try {
            this.isDownloading.set(true);

            // Exactly one file with no folder roots → use the single-file endpoint so
            // the user gets the file as-is instead of a one-file zip. Only honour this
            // shortcut when individual file downloads are actually allowed; otherwise
            // fall through to the bulk endpoint (a 1-file zip).
            if (summary.isSingleFile && summary.singleFileId && this.allowIndividualFileDownload()) {
                const single = await this._api.getFileDownloadLink(
                    this.slug!, this.token,
                    summary.singleFileId,
                    'attachment');

                this.triggerBrowserDownload(single.downloadPreSignedUrl);
            } else {
                const bulk = await this._api.getBulkDownloadLink(
                    this.slug!, this.token,
                    this.selectionState());

                this.triggerBrowserDownload(bulk.preSignedUrl);
            }

            await this.loadInfo();
        } catch (error) {
            if (error instanceof HttpErrorResponse && error.status === 410) {
                this._toastr.error('Download limit reached');
                await this.loadInfo();
            } else {
                console.error(error);
                this._toastr.error('Download failed');
            }
        } finally {
            this.isDownloading.set(false);
        }
    }

    onFilePreviewed(node: StaticFileNode) {
        if (!this.allowIndividualFileDownload()) {
            this._toastr.error('File preview is disabled by the share owner');
            return;
        }

        this.currentFilePreviewId.set(node.id);
        this.zipEntryInPreview.set(null);
    }

    closePreview() {
        this.currentFilePreviewId.set(null);
        this.zipEntryInPreview.set(null);
    }

    onZipEntryClicked(entry: ZipEntry) {
        this.zipEntryInPreview.set(entry);
    }

    closeZipEntryPreview() {
        this.zipEntryInPreview.set(null);
    }

    private buildFileContentOperations(fileExternalId: string): FileContentOperations {
        const slug = this.slug!;
        const token = this.token;

        return {
            getDownloadLink: () => this._api.getFilePreviewLink(slug, token, fileExternalId),

            getZipPreviewDetails: () => this._api.getZipPreviewDetails(slug, token, fileExternalId),

            // Inline disposition = browsing an entry (preview, no quota burn);
            // attachment = actually downloading it. Two endpoints handle these
            // separately so the audit/quota semantics match the single-file
            // preview vs download split.
            getZipContentDownloadLink: (zipEntry, contentDisposition) => contentDisposition === 'inline'
                ? this._api.getZipContentPreviewLink(slug, token, fileExternalId, zipEntry)
                : this._api.getZipContentDownloadLink(slug, token, fileExternalId, zipEntry, contentDisposition),

            getZipBulkDownloadLink: (request) => this._api.getZipBulkDownloadLink(slug, token, fileExternalId, request),

            prepareAdditionalHttpHeaders: () => undefined
        };
    }

    // Operations bound to a SPECIFIC zip entry inside the parent file. Used by
    // the nested <app-file-content> that renders the clicked entry's contents.
    // getDownloadLink here resolves to the zip-content endpoints (preview vs
    // download by disposition); the nested file-content never recursively
    // opens its own zip, so the zip-* fields are unreachable but must satisfy
    // the FileContentOperations contract.
    private buildZipEntryContentOperations(fileExternalId: string, zipEntry: ZipEntry): FileContentOperations {
        const slug = this.slug!;
        const token = this.token;

        return {
            getDownloadLink: (contentDisposition) => contentDisposition === 'inline'
                ? this._api.getZipContentPreviewLink(slug, token, fileExternalId, zipEntry)
                : this._api.getZipContentDownloadLink(slug, token, fileExternalId, zipEntry, contentDisposition),

            getZipPreviewDetails: () => Promise.reject(new Error('nested zip preview not supported')),
            getZipContentDownloadLink: () => Promise.reject(new Error('nested zip preview not supported')),
            getZipBulkDownloadLink: () => Promise.reject(new Error('nested zip preview not supported')),

            prepareAdditionalHttpHeaders: () => undefined
        };
    }

    toggleSelectAll() {
        const shouldSelectAll = this.selectAllState() !== 'all';

        for (const root of this.fileTree()) {
            root.isSelected.set(shouldSelectAll);
            root.isExcluded.set(false);
            if (root.type === 'folder') {
                this.clearDescendantSelection(root.children);
            }
        }
    }

    private clearDescendantSelection(nodes: StaticTreeNode[]) {
        for (const node of nodes) {
            node.isSelected.set(false);
            node.isExcluded.set(false);
            if (node.type === 'folder') {
                this.clearDescendantSelection(node.children);
            }
        }
    }

    private triggerBrowserDownload(url: string) {
        const link = document.createElement('a');
        link.href = url;
        link.click();
        link.remove();
    }

    // Indexes source data by parent so lazy folder nodes (and their search-
    // metadata enumeration) can resolve children without scanning the flat lists.
    private buildTree(folders: QuickShareContentFolder[], files: QuickShareContentFile[]): StaticTreeNode[] {
        const childFoldersByParent = new Map<string | null, QuickShareContentFolder[]>();
        const childFilesByFolder = new Map<string | null, QuickShareContentFile[]>();

        for (const f of folders) {
            const key = f.parentExternalId;
            const bucket = childFoldersByParent.get(key);
            if (bucket) bucket.push(f);
            else childFoldersByParent.set(key, [f]);
        }

        for (const file of files) {
            const key = file.folderExternalId;
            const bucket = childFilesByFolder.get(key);
            if (bucket) bucket.push(file);
            else childFilesByFolder.set(key, [file]);
        }

        return this.materializeLevel(
            null, 
            null, 
            childFoldersByParent, 
            childFilesByFolder);
    }

    // Materializes immediate children of `parentFolderId`. Called once for root
    // and on every folder's first expand (via loadChildren).
    private materializeLevel(
        parentFolderId: string | null,
        parent: StaticFolderNode | null,
        childFoldersByParent: Map<string | null, QuickShareContentFolder[]>,
        childFilesByFolder: Map<string | null, QuickShareContentFile[]>
    ): StaticTreeNode[] {
        const result: StaticTreeNode[] = [];

        const subFolders = childFoldersByParent.get(parentFolderId) ?? [];

        for (const subfolder of subFolders) {
            const folderNode = this.makeFolderNode(
                subfolder, 
                parent, 
                childFoldersByParent, 
                childFilesByFolder);

            result.push(folderNode);
        }

        const subFiles = childFilesByFolder.get(parentFolderId) ?? [];

        for (const file of subFiles) {
            const fileNode = this.makeFileNode(
                file,
                parent);

            result.push(fileNode);
        }

        return result;
    }

    // Walks the source-data maps to produce flat search metadata for a folder's
    // whole subtree WITHOUT materializing any StaticTreeNode. Ancestor chains
    // are relative to `folderId` — the consumer re-anchors them.
    private enumerateFolderForSearch(
        folderId: string,
        childFoldersByParent: Map<string | null, QuickShareContentFolder[]>,
        childFilesByFolder: Map<string | null, QuickShareContentFile[]>
    ): StaticSearchCorpusEntry[] {
        const result: StaticSearchCorpusEntry[] = [];

        type Frame = { parentId: string; ancestorIds: string[]; };
        const stack: Frame[] = [{ parentId: folderId, ancestorIds: [] }];

        while (stack.length > 0) {
            const frame = stack.pop()!;

            const subFolders = childFoldersByParent.get(frame.parentId) ?? [];
            for (const sub of subFolders) {
                result.push({
                    id: sub.externalId,
                    name: sub.name,
                    nameLower: sub.name.toLowerCase(),
                    type: 'folder',
                    ancestorFolderIds: frame.ancestorIds
                });
                stack.push({
                    parentId: sub.externalId,
                    ancestorIds: [...frame.ancestorIds, sub.externalId]
                });
            }

            const subFiles = childFilesByFolder.get(frame.parentId) ?? [];
            for (const file of subFiles) {
                const fullName = file.name + file.extension;
                result.push({
                    id: file.externalId,
                    name: fullName,
                    nameLower: fullName.toLowerCase(),
                    type: 'file',
                    ancestorFolderIds: frame.ancestorIds
                });
            }
        }

        return result;
    }

    private makeFolderNode(
        folder: QuickShareContentFolder,
        parent: StaticFolderNode | null,
        childFoldersByParent: Map<string | null, QuickShareContentFolder[]>,
        childFilesByFolder: Map<string | null, QuickShareContentFile[]>
    ): StaticFolderNode {
        const isSelected = signal(false);
        const isExcluded = signal(false);

        const isParentSelected = computed(() =>
            parent ? (parent.isSelected() || parent.isParentSelected()) : false);

        const isParentExcluded = computed(() =>
            parent ? (parent.isExcluded() || parent.isParentExcluded()) : false);

        const children: StaticTreeNode[] = [];

        // Signal-of-signal — see makeFolderNode in zip.ts for the rationale.
        const subtreeInner = signal<Signal<StaticTreeSelection>>(signal(EMPTY_TREE_SELECTION));
        const subtreeState = computed(() => subtreeInner()());

        const node: StaticFolderNode = {
            type: 'folder',
            id: folder.externalId,
            name: folder.name,
            nameLower: folder.name.toLowerCase(),
            children: children,
            isExpanded: signal(false),
            isVisible: signal(true),

            // Built on first expand; consumer nulls this out afterwards.
            loadChildren: () => {
                const built = this.materializeLevel(
                    folder.externalId,
                    node,
                    childFoldersByParent,
                    childFilesByFolder);

                for (const child of built){
                    children.push(child);
                }

                subtreeInner.set(
                    computed(() => collectSelectionFromChildren(children)));
            },

            // Source-data driven search enumeration so the n-gram corpus covers
            // every entry without forcing materialization of unopened subtrees.
            enumerateDescendantsForSearch: () =>
                this.enumerateFolderForSearch(folder.externalId, childFoldersByParent, childFilesByFolder),

            isSelected: isSelected,
            isExcluded: isExcluded,
            parent: parent,
            isParentSelected: isParentSelected,
            isParentExcluded: isParentExcluded,

            subtreeState: subtreeState,
            selectedDescendantsCount: computed(() => {
                const s = subtreeState();
                return s.selectedFolderIds.length + s.selectedFileIds.length;
            })
        };

        return node;
    }

    private makeFileNode(file: QuickShareContentFile, parent: StaticFolderNode | null): StaticFileNode {
        const isSelected = signal(false);
        const isExcluded = signal(false);

        const isParentSelected = computed(() =>
            parent ? (parent.isSelected() || parent.isParentSelected()) : false);

        const isParentExcluded = computed(() =>
            parent ? (parent.isExcluded() || parent.isParentExcluded()) : false);

        return {
            type: 'file',
            id: file.externalId,
            extension: file.extension,
            fullName: file.name + file.extension,
            fullNameLower: (file.name + file.extension).toLowerCase(),
            sizeInBytes: file.sizeInBytes,
            isVisible: signal(true),

            isSelected: isSelected,
            isExcluded: isExcluded,
            parent: parent,
            isParentSelected: isParentSelected,
            isParentExcluded: isParentExcluded
        };
    }
}
