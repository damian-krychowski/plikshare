import { Component, input, signal, computed, OnChanges, SimpleChanges, output } from '@angular/core';
import { ZipArchive, ZipArchives, ZipEntry } from '../../../services/zip';
import { ActionButtonComponent } from '../../../shared/buttons/action-btn/action-btn.component';
import { ItemSearchComponent } from '../../../shared/item-search/item-search.component';
import { FileIconPipe } from '../../file-icon-pipe/file-icon.pipe';
import { ContentDisposition } from '../../../services/folders-and-files.api';
import { ZipPreviewDetails } from '../../file-inline-preview/file-inline-preview.component';
import { ZipFileNode, ZipTreeNode, ZipFileTreeViewComponent } from '../../../shared/zip-file-tree-view/zip-file-tree-view.component';

export interface ZipPreviewOperations {
    getZipPreviewDetails: () => Promise<ZipPreviewDetails>;
    getZipContentDownloadLink: (zipEntry: ZipEntry, contentDisposition: ContentDisposition) => Promise<{downloadPreSignedUrl: string}>;
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
        const archive = ZipArchives.getStructure(result.items);
        this.zipArchive.set(archive);
    }

    async onZipEntryDownloadClick(fileNode: ZipFileNode) {
        const entry = this.tryGetEntry(
            fileNode);

        if(!entry)
            return;

        const response = await this
            .operations()
            .getZipContentDownloadLink(
                entry,
                'attachment');

        const link = document.createElement('a');
        link.href = response.downloadPreSignedUrl;
        link.download = ZipArchives.getFullName(entry);
        link.click();
        link.remove();
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
}