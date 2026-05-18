import { Component, OnInit, WritableSignal, computed, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ToastrService } from 'ngx-toastr';
import { HttpErrorResponse } from '@angular/common/http';
import { GetQuickShareBulkDownloadLinkRequest, GetQuickShareContentResponse, GetQuickShareInfoResponse, QuickShareContentFile, QuickShareContentFolder, QuickShareExternalAccessApi } from '../../services/quick-share-external-access.api';
import { ZipFileNode, ZipFileTreeViewComponent, ZipFolderNode, ZipTreeNode } from '../../shared/zip-file-tree-view/zip-file-tree-view.component';
import { StorageSizePipe } from '../../shared/storage-size.pipe';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { ActionTextButtonComponent } from '../../shared/buttons/action-text-btn/action-text-btn.component';
import { ItemSearchComponent } from '../../shared/item-search/item-search.component';

@Component({
    selector: 'app-quick-share',
    imports: [
        DatePipe,
        FormsModule,
        MatCheckboxModule,
        MatFormFieldModule,
        MatInputModule,
        ZipFileTreeViewComponent,
        StorageSizePipe,
        ActionButtonComponent,
        ActionTextButtonComponent,
        ItemSearchComponent
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
    fileTree: WritableSignal<ZipTreeNode[]> = signal([]);

    passwordInput = signal('');
    isUnlocking = signal(false);
    isDownloading = signal(false);
    searchPhrase = signal('');

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

    selectionState = computed<GetQuickShareBulkDownloadLinkRequest>(() => {
        const state: GetQuickShareBulkDownloadLinkRequest = {
            selectedFolderExternalIds: [],
            selectedFileExternalIds: [],
            excludedFolderExternalIds: [],
            excludedFileExternalIds: []
        };

        this.collectSelected(this.fileTree(), state);
        return state;
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
            const content = await this._api.getContent(this.slug!, this.token);
            this.content.set(content);
            this.fileTree.set(this.buildTree(content.folders, content.files));
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to load quick share content');
        }
    }

    async unlock() {
        if (this.isUnlocking() || !this.passwordInput().trim()) return;

        try {
            this.isUnlocking.set(true);
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
                this._toastr.error('Wrong password');
            } else {
                console.error(error);
                this._toastr.error('Unlock failed');
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

    private clearDescendantSelection(nodes: ZipTreeNode[]) {
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

    private collectSelected(nodes: ZipTreeNode[], state: GetQuickShareBulkDownloadLinkRequest) {
        for (const node of nodes) {
            if (node.isSelected()) {
                if (node.type === 'folder') {
                    state.selectedFolderExternalIds.push(node.id);
                    this.collectExcludesUnder(node.children, state);
                } else {
                    state.selectedFileExternalIds.push(node.id);
                }
            } else if (node.type === 'folder') {
                this.collectSelected(node.children, state);
            }
        }
    }

    private collectExcludesUnder(nodes: ZipTreeNode[], state: GetQuickShareBulkDownloadLinkRequest) {
        for (const node of nodes) {
            if (node.isExcluded()) {
                if (node.type === 'folder') {
                    state.excludedFolderExternalIds.push(node.id);
                } else {
                    state.excludedFileExternalIds.push(node.id);
                }
            } else if (node.type === 'folder') {
                this.collectExcludesUnder(node.children, state);
            }
        }
    }

    // Server emits folders in parent-before-child order (ordered by ancestor-chain
    // length), so we can wire parent refs in a single pass.
    private buildTree(folders: QuickShareContentFolder[], files: QuickShareContentFile[]): ZipTreeNode[] {
        const root: ZipTreeNode[] = [];
        const folderById = new Map<string, ZipFolderNode>();

        for (const f of folders) {
            const parent = f.parentExternalId !== null
                ? folderById.get(f.parentExternalId) ?? null
                : null;

            const node = this.makeFolderNode(f.externalId, f.name, parent);
            folderById.set(f.externalId, node);

            if (parent) {
                parent.children.push(node);
            } else {
                root.push(node);
            }
        }

        for (const file of files) {
            const parent = file.folderExternalId !== null
                ? folderById.get(file.folderExternalId) ?? null
                : null;

            const node = this.makeFileNode(file, parent);

            if (parent) {
                parent.children.push(node);
            } else {
                root.push(node);
            }
        }

        return root;
    }

    private makeFolderNode(id: string, name: string, parent: ZipFolderNode | null): ZipFolderNode {
        const isExpanded = signal(false);
        const isSelected = signal(false);
        const isExcluded = signal(false);

        const isParentSelected = computed(() =>
            parent ? (parent.isSelected() || parent.isParentSelected()) : false);

        const isParentExcluded = computed(() =>
            parent ? (parent.isExcluded() || parent.isParentExcluded()) : false);

        return {
            type: 'folder',
            id: id,
            name: name,
            nameLower: name.toLowerCase(),
            children: [],
            isExpanded: isExpanded,
            isVisible: signal(true),
            wasRendered: signal(true),
            wasLoaded: true,

            isSelected: isSelected,
            isExcluded: isExcluded,
            parent: parent,
            isParentSelected: isParentSelected,
            isParentExcluded: isParentExcluded
        };
    }

    private makeFileNode(file: QuickShareContentFile, parent: ZipFolderNode | null): ZipFileNode {
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
