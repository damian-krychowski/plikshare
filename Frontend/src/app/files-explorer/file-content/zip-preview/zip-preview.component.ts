import { Component, input, signal, computed, OnChanges, SimpleChanges, output, WritableSignal } from '@angular/core';
import { ZipArchive, ZipArchives, ZipEntry } from '../../../services/zip';
import { ActionButtonComponent } from '../../../shared/buttons/action-btn/action-btn.component';
import { ItemSearchComponent } from '../../../shared/item-search/item-search.component';
import { FileIconPipe } from '../../file-icon-pipe/file-icon.pipe';
import { ContentDisposition, GetZipBulkDownloadLinkRequest } from '../../../services/folders-and-files.api';
import { ZipPreviewDetails } from '../../file-inline-preview/file-inline-preview.component';
import { EMPTY_TREE_SELECTION, StaticFileNode, StaticFileTreeViewComponent, StaticTreeNode, StaticTreeSelection } from '../../../shared/static-file-tree-view/static-file-tree-view.component';

export interface ZipPreviewOperations {
    getZipPreviewDetails: () => Promise<ZipPreviewDetails>;
    getZipContentDownloadLink: (zipEntry: ZipEntry, contentDisposition: ContentDisposition) => Promise<{downloadPreSignedUrl: string}>;
    getZipBulkDownloadLink: (request: GetZipBulkDownloadLinkRequest) => Promise<{downloadPreSignedUrl: string}>;
}

@Component({
    selector: 'app-zip-preview',
    imports: [
        StaticFileTreeViewComponent,
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
    zipFileTreeNodes = computed<StaticTreeNode[]>(() => {
        const archive = this.zipArchive();

        if(!archive)
            return [];

        return ZipArchives.buildArchiveTree(archive);
    });

    // Fed by (selectionChanged) from the tree component, which owns the walk +
    // lazy-aware cascade. We just map the generic string-id payload to the
    // numeric form the bulk-download endpoint expects.
    treeSelection: WritableSignal<StaticTreeSelection> = signal(EMPTY_TREE_SELECTION);

    selectionState = computed<GetZipBulkDownloadLinkRequest>(() => {
        const s = this.treeSelection();
        return {
            selectedFolderIds: s.selectedFolderIds.map(id => parseInt(id)),
            selectedEntryIndices: s.selectedFileIds.map(id => parseInt(id)),
            excludedFolderIds: s.excludedFolderIds.map(id => parseInt(id)),
            excludedEntryIndices: s.excludedFileIds.map(id => parseInt(id))
        };
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

    isArchiveOpened = signal(true);
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

    async onZipEntryClick(fileNode: StaticFileNode) {
        const entry = this.tryGetEntry(
            fileNode);

        if(!entry)
            return;

        this.zipEntryClicked.emit(entry);
    }

    private tryGetEntry(fileNode: StaticFileNode) {
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

}
