import { Component, input, signal, computed, OnChanges, SimpleChanges, output } from '@angular/core';
import { ZipArchive, ZipArchives, ZipEntry } from '../../../services/zip';
import { ActionButtonComponent } from '../../../shared/buttons/action-btn/action-btn.component';
import { ItemSearchComponent } from '../../../shared/item-search/item-search.component';
import { FileIconPipe } from '../../file-icon-pipe/file-icon.pipe';
import { ContentDisposition, GetZipBulkDownloadLinkRequest } from '../../../services/folders-and-files.api';
import { ZipPreviewDetails } from '../../file-inline-preview/file-inline-preview.component';
import { ZipFileNode, ZipTreeNode, ZipFileTreeViewComponent } from '../../../shared/zip-file-tree-view/zip-file-tree-view.component';

export interface ZipPreviewOperations {
    getZipPreviewDetails: () => Promise<ZipPreviewDetails>;
    getZipContentDownloadLink: (zipEntry: ZipEntry, contentDisposition: ContentDisposition) => Promise<{downloadPreSignedUrl: string}>;
    getZipBulkDownloadLink: (request: GetZipBulkDownloadLinkRequest) => Promise<{downloadPreSignedUrl: string}>;
}

@Component({
    selector: 'app-zip-preview',
    imports: [
        ZipFileTreeViewComponent,
        ActionButtonComponent,
        ItemSearchComponent,
        FileIconPipe
    ],
    templateUrl: './zip-preview.component.html',
    styleUrl: './zip-preview.component.scss'
})
export class ZipPreviewComponent implements OnChanges {
    fileName = input.required<string>();
    fileExtension = input.required<string>();
    operations = input.required<ZipPreviewOperations>();

    zipEntryClicked = output<ZipEntry>();

    fileFullName = computed(() => this.fileName() + this.fileExtension());

    zipArchive = signal<ZipArchive | null>(null);
    zipFileTreeNodes = computed<ZipTreeNode[]>(() => {
        const archive = this.zipArchive();

        if(!archive)
            return [];

        return ZipArchives.buildArchiveTree(archive);
    });

    // Aggregated 4-array view of the in-tree selection state. Recomputes whenever
    // any node's isSelected/isExcluded signal changes — the bulk-download button's
    // count and the eventual server payload both feed off this.
    selectionState = computed<GetZipBulkDownloadLinkRequest>(() => {
        const state: GetZipBulkDownloadLinkRequest = {
            selectedFolderIds: [],
            selectedEntryIndices: [],
            excludedFolderIds: [],
            excludedEntryIndices: []
        };

        this.collectSelected(this.zipFileTreeNodes(), state);
        return state;
    });

    selectionSummary = computed(() => {
        const state = this.selectionState();
        const totalSelected = state.selectedFolderIds.length + state.selectedEntryIndices.length;

        const isSingleFileOnly = state.selectedFolderIds.length === 0
            && state.selectedEntryIndices.length === 1
            && state.excludedFolderIds.length === 0
            && state.excludedEntryIndices.length === 0;

        return {
            count: totalSelected,
            isSingleFileOnly: isSingleFileOnly
        };
    });

    isArchiveOpened = signal(false);
    zipSearchPhrase = signal('');

    async ngOnChanges(changes: SimpleChanges): Promise<void> {
        const operations = this.operations();

        if(changes['operations'] && operations) {
            await this.loadZipDetails(operations);
        }
    }

    closeArchive() {
        this.isArchiveOpened.set(false);
        this.zipSearchPhrase.set('');
    }

    async loadZipDetails(operations: ZipPreviewOperations) {
        const result = await operations.getZipPreviewDetails();
        const archive = ZipArchives.getStructure(
            result.items,
            result.folders);
        this.zipArchive.set(archive);
    }

    async downloadSelected() {
        const summary = this.selectionSummary();
        if (summary.count === 0)
            return;

        const archive = this.zipArchive();
        if (!archive)
            return;

        const state = this.selectionState();
        const ops = this.operations();

        // One entry, no folders, no excludes → the user effectively picked a single
        // file, so route to the existing single-entry endpoint. That delivers the
        // raw decompressed bytes instead of a one-file zip, which is what they want.
        if (summary.isSingleFileOnly) {
            const entry = archive.entriesMap.get(state.selectedEntryIndices[0]);
            if (!entry)
                return;

            const response = await ops.getZipContentDownloadLink(entry, 'attachment');
            this.triggerBrowserDownload(
                response.downloadPreSignedUrl,
                ZipArchives.getFullName(entry));
            return;
        }

        const response = await ops.getZipBulkDownloadLink(state);
        this.triggerBrowserDownload(
            response.downloadPreSignedUrl,
            `${this.fileName()}-selection.zip`);
    }

    async onZipEntryClick(fileNode: ZipFileNode) {
        const entry = this.tryGetEntry(
            fileNode);

        if(!entry)
            return;

        this.zipEntryClicked.emit(entry);
    }

    private tryGetEntry(fileNode: ZipFileNode) {
        const archive = this.zipArchive();

        if(!archive)
            return null;

        const entry = archive
            .entriesMap
            .get(parseInt(fileNode.id));

        if(!entry)
            return null;

        return entry;
    }

    private triggerBrowserDownload(url: string, filename: string) {
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        link.click();
        link.remove();
    }

    // Selected folders short-circuit descent: they include all their contents by
    // contract, so the children are only walked to harvest excludes inside them.
    // Non-selected folders recurse normally to find sub-selections.
    private collectSelected(nodes: ZipTreeNode[], state: GetZipBulkDownloadLinkRequest) {
        for (const node of nodes) {
            if (node.isSelected()) {
                if (node.type === 'folder') {
                    state.selectedFolderIds.push(parseInt(node.id));
                    this.collectExcludesUnder(node.children, state);
                } else {
                    state.selectedEntryIndices.push(parseInt(node.id));
                }
            } else if (node.type === 'folder') {
                this.collectSelected(node.children, state);
            }
        }
    }

    private collectExcludesUnder(nodes: ZipTreeNode[], state: GetZipBulkDownloadLinkRequest) {
        for (const node of nodes) {
            if (node.isExcluded()) {
                if (node.type === 'folder') {
                    state.excludedFolderIds.push(parseInt(node.id));
                    // Excluded folder prunes the whole subtree from the payload;
                    // descending further would only collect dead-letter excludes.
                } else {
                    state.excludedEntryIndices.push(parseInt(node.id));
                }
            } else if (node.type === 'folder') {
                this.collectExcludesUnder(node.children, state);
            }
        }
    }
}
