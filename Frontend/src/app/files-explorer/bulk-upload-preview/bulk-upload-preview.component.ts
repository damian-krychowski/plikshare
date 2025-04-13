import { Component, computed, input, OnInit, output, Renderer2, Signal, signal, WritableSignal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { FileIconPipe } from '../file-icon-pipe/file-icon.pipe';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { ItemSearchComponent } from '../../shared/item-search/item-search.component';
import { ItemButtonComponent } from '../../shared/buttons/item-btn/item-btn.component';
import { getMimeType, toNameAndExtension } from '../../services/filte-type';
import { getBase62Guid } from '../../services/guid-base-62';
import { FileUploadManager, FileUploadApi, FileToUpload } from '../../services/file-upload-manager/file-upload-manager';
import { Zip, ZipArchive, ZipArchives, ZipCdfhRecord, ZipEntry, ZipFolder } from '../../services/zip';
import { StorageSizePipe } from '../../shared/storage-size.pipe';
import { BulkCreateFolderRequest, BulkCreateFolderResponse, BulkCreateFolderTree } from '../../services/folders-and-files.api';
import { BlobSlicer } from '../../services/file-upload-manager/blob-slicer';
import { CompressedBlobSlicer } from '../../services/file-upload-manager/compressed-blob-file-slicer';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TimeService } from '../../services/time.service';
import { ElapsedTimePipe } from '../../shared/elapsed-time.pipe';
import { DropFilesDirective } from '../directives/drop-files.directive';
import { ZipTreeNode, ZipFileTreeViewComponent } from '../../shared/zip-file-tree-view/zip-file-tree-view.component';
import { HttpHeadersFactory } from '../http-headers-factory';

export type BulkUploadZipEntry = {
    fileName: string;
    fileExtension: string;

    filePath: string;
    compressedSizeInBytes: number;
    sizeInBytes: number;
    offsetToLocalFileHeader: number;
    fileNameLength: number;
    compressionMethod: number;
    indexInArchive: number;
};

//todo this structure is kind of misleadng
export type SingleBulkFileUpload = {
    externalId: string;
    fileName: string;
    fileExtension: string;
    fullFileName: string;
    fileSize: number;
    entries: WritableSignal<BulkUploadZipEntry[] | null>;
    archive: WritableSignal<ZipArchive | null>;

    searchPhrase: WritableSignal<string>;    
    isOpened: WritableSignal<boolean>;
    isBroken: WritableSignal<boolean>;
    doesIncludeDeflateCompression: Signal<boolean>;
    doesIncludeNotSupportedCompression: Signal<boolean>;

    file: File;
}

export type CreatedFolder = {
    name: string;
    externalId: string;
}

export type BulkFileUpload = {
    archive: WritableSignal<SingleBulkFileUpload | null>;
    isUploadEnabled: Signal<boolean>;
    isStarted: WritableSignal<boolean>;
    isCompleted: WritableSignal<boolean>;
}

export type BulkUploadFolderOperations = {
    bulkCreateFolders: (request: BulkCreateFolderRequest) => Promise<BulkCreateFolderResponse>;
}

const DEFLATE_COMPRESSION_CODE = 8;
const NO_COMPRESSION_CODE = 0;

@Component({
    selector: 'app-bulk-upload-preview',
    imports: [
        MatButtonModule,
        MatSlideToggleModule,
        FileIconPipe,
        ZipFileTreeViewComponent,
        ActionButtonComponent,
        ItemSearchComponent,
        ItemButtonComponent,
        StorageSizePipe,
        MatProgressBarModule,
        ElapsedTimePipe,
        DropFilesDirective
    ],
    templateUrl: './bulk-upload-preview.component.html',
    styleUrls: ['./bulk-upload-preview.component.scss']
})
export class BulkUploadPreviewComponent implements OnInit {
    bulkUpload = input.required<BulkFileUpload>();
    folderExternalId = input.required<string | null>();

    uploadsApi = input.required<FileUploadApi>();
    foldersApi = input.required<BulkUploadFolderOperations>();
    httpHeadersFactory = input.required<HttpHeadersFactory>();
    
    archive = computed(() => this.bulkUpload().archive());
    archiveFileTree = computed<ZipTreeNode[]>(() => {
        const bulkFileUpload = this.archive();

        if(!bulkFileUpload)
            return [];

        const zipArchive = bulkFileUpload.archive();

        if(!zipArchive)
            return [];

        return ZipArchives.buildArchiveTree(zipArchive);
    });

    isDecompressionSupported: boolean = (typeof DecompressionStream === 'function');

    closed = output();
    started = output();
    completed = output();

    foldersCreated = output<CreatedFolder[]>();

    alreadyUploadedBytes = signal(0);
    alreadyUploadedFiles = signal(0);

    totalEntriesSizeInBytes = computed(() => this.calculateTotalEntriesSizeInBytes());
    progressPercentage = computed(() => this.calculateUploadProgressPercentage());
    
    isUploadStarted = computed(() => this.bulkUpload().isStarted());
    uploadStartedAt = signal<Date | null>(null);

    isUploadCompleted = computed(() => this.bulkUpload().isCompleted());
    uploadCompletedAt = signal<Date | null>(null);

    elapsedUploadSeconds = computed(() => {
        const startedAt = this.uploadStartedAt();

        if (!startedAt) {
            return 0;
        }

        const startedAtTime = startedAt.getTime();

        const completedAt = this.uploadCompletedAt();

        if(completedAt) {
            const completedAtTime = completedAt.getTime();
            return Math.floor((completedAtTime - startedAtTime) / 1000); // Convert to seconds
        }

        const currentTime = this._timeService.currentTime(); // Date.now()
        return Math.floor((currentTime - startedAtTime) / 1000); // Convert to seconds
    });
    
    dragCounter = 0;
    isDragging = signal(false);
    fileDropTooManyFiles = signal(false);
    fileDropWrongExtension = signal(false);

    constructor(
        private _timeService: TimeService,
        private _renderer: Renderer2,
        private _fileUploadManager: FileUploadManager) {
    }

    ngOnInit(): void {
        this._renderer.listen('window', 'dragenter', this.onDragEnter.bind(this));
        this._renderer.listen('window', 'dragleave', this.onDragLeave.bind(this));
        this._renderer.listen('window', 'dragover', this.onDragOver.bind(this));
        this._renderer.listen('window', 'drop', this.onDrop.bind(this));
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

    private calculateUploadProgressPercentage() {              
        const totalSizeInBytes = this.totalEntriesSizeInBytes();
        const currentSizeInBytes = this.alreadyUploadedBytes(); 

        return Math.round((currentSizeInBytes / totalSizeInBytes) * 100);
    }

    private calculateTotalEntriesSizeInBytes() {
        const archive = this.archive();

        if(!archive)
            return 0;

        const entries = archive.entries();

        if(!entries)
            return 0;

        const totalSize = entries.reduce((acc, current) => acc + current.sizeInBytes, 0);

        return totalSize;
    }

    public async runBulkUpload() {
        const bulkUpload = this.bulkUpload();

        if(bulkUpload.isStarted())
            return;

        const archive = bulkUpload.archive();
     
        if(!archive)
            return;
     
        const zipArchive = archive.archive();
     
        if(!zipArchive)
            return;
       
        bulkUpload.isStarted.set(true);
        this.uploadStartedAt.set(new Date());
        this.started.emit();

        const { foldersTree, entriesMap } = this.prepareFoldersTree(zipArchive);
     
        const bulkCreateFoldersRequest: BulkCreateFolderRequest = {
            ensureUniqueNames: true,
            parentExternalId: this.folderExternalId(),
            folderTrees: foldersTree
        };
     
        const folderResult = await this
            .foldersApi()
            .bulkCreateFolders(bulkCreateFoldersRequest);
     
        const createdFolders: CreatedFolder[] = []
     
        for (const topFolder of foldersTree) {
            const createdFolderResult = folderResult
                .items
                .find(x => x.temporaryId === topFolder.temporaryId);
     
            if(!createdFolderResult)
                throw new Error(`Folder '${topFolder.name}' was not created`);
     
            createdFolders.push({
                externalId: createdFolderResult?.externalId,
                name: topFolder.name
            });
        }
     
        this.foldersCreated.emit(createdFolders);
        
        const CHUNK_SIZE = 150;
        let filesToUploadPromises: Promise<FileToUpload>[] = [];
             
        for (const topLevelEntry of zipArchive.entries) {
            const fileToUpload = this.mapZipEntryToFileToUpload({
                archive: archive,
                entry: topLevelEntry,
                folderExternalId: this.folderExternalId(),
            });
     
            filesToUploadPromises.push(fileToUpload);
     
            if(filesToUploadPromises.length >= CHUNK_SIZE) {
                await this.processChunk(filesToUploadPromises);
                filesToUploadPromises = []
            }
        }

        for (const folder of folderResult.items) {
            const folderEntries = entriesMap.get(folder.temporaryId) ?? [];
            
            for(const entry of folderEntries) {
                const fileToUpload = this.mapZipEntryToFileToUpload({
                    archive: archive,
                    entry: entry,
                    folderExternalId: folder.externalId
                });
     
                filesToUploadPromises.push(fileToUpload);
     
                if(filesToUploadPromises.length >= CHUNK_SIZE) {
                    await this.processChunk(filesToUploadPromises);
                    filesToUploadPromises = []
                }
            }           
        }
     
        if(filesToUploadPromises.length > 0) {
            await this.processChunk(filesToUploadPromises);
        }
     }

    private async processChunk(filesToUploadPromises: Promise<FileToUpload>[]) {
        const results = await Promise.all(filesToUploadPromises);
        
        this._fileUploadManager.addFiles(
            results,
            this.uploadsApi(),
            this.httpHeadersFactory());
    }

    private async mapZipEntryToFileToUpload(args: {
        entry: ZipEntry, 
        folderExternalId: string | null,
        archive: SingleBulkFileUpload
    }): Promise<FileToUpload> {
        const {entry, folderExternalId, archive} = args;
        const {name, extension} = ZipArchives.getFileNameAndExtension(entry);
        const fullName = ZipArchives.getFullName(entry);
        
        const lfhRecord = await Zip.readLfhRecord(
            archive.file,
            entry.offsetToLocalFileHeader);

        const blob = archive
            .file
            .slice(
                entry.offsetToLocalFileHeader + Zip.LFH_MINIMUM_SIZE + entry.fileNameLength + lfhRecord.extraFieldLength, 
                entry.offsetToLocalFileHeader + Zip.LFH_MINIMUM_SIZE + entry.fileNameLength + lfhRecord.extraFieldLength + entry.compressedSizeInBytes);

        const fileToUpload: FileToUpload = {
            folderExternalId: folderExternalId,
            name: fullName,
            size: entry.sizeInBytes,
            contentType: getMimeType(extension),
            slicer: entry.compressionMethod == DEFLATE_COMPRESSION_CODE
                ? new CompressedBlobSlicer(
                    entry.sizeInBytes,
                    blob)
                : new BlobSlicer(blob),

            reportProgressCallback: (alreadyUploadedBytes) => this.alreadyUploadedBytes.update(value => value + alreadyUploadedBytes),
            reportUploadFinishedCallback: () => {
                this.alreadyUploadedFiles.update(value => value += 1);

                if(this.archive()?.entries()?.length == this.alreadyUploadedFiles()) {
                    this.bulkUpload().isCompleted.set(true);
                    this.uploadCompletedAt.set(new Date());
                    this.completed.emit();
                }
            }
        };

        return fileToUpload;
    }
   
    private prepareFoldersTree(zipArchive: ZipArchive): {foldersTree: BulkCreateFolderTree[], entriesMap: Map<number, ZipEntry[]>} {
        type NodeToProcess = {
            parentCollection: BulkCreateFolderTree[];
            folders: ZipFolder[];
        };

        let temporaryId = 1;
        
        const stack: NodeToProcess[] = [];
        const results: BulkCreateFolderTree[] = [];
        const entriesMap = new Map<number, ZipEntry[]>();

        stack.push({
            parentCollection: results,
            folders: zipArchive.folders
        });

        while(stack.length > 0) {
            const current = stack.pop();

            if(!current)
                break;

            for (const folder of current.folders) {
                const newParent:BulkCreateFolderTree = {
                    name: folder.name,
                    temporaryId: temporaryId,
                    subfolders: []
                }

                entriesMap.set(temporaryId, folder.entries);

                temporaryId += 1;

                current.parentCollection.push(newParent);

                if(folder.folders.length > 0) {
                    stack.push({
                        parentCollection: newParent.subfolders,
                        folders: folder.folders
                    });
                }
            }
        }

        return {
            foldersTree: results,
            entriesMap: entriesMap
        };
    }

    public onCancel() {
        this.closed.emit();
    }

    removeArchive(upload: SingleBulkFileUpload) {
        this.bulkUpload().archive.set(null);
    }

    closeArchive(upload: SingleBulkFileUpload) {
        upload.isOpened.set(false);
        upload.searchPhrase.set('');
    }

    addZipArchive(fileUpload: HTMLInputElement) {
        fileUpload.click();
    }

    onFilesDropped(files: File[]) {
        if (!files || files.length !== 1) {
            this.fileDropTooManyFiles.set(true);
            setTimeout(() => this.fileDropTooManyFiles.set(false), 1500);
            return;
        }
    
        const file = files[0];
        if (!file.name.toLowerCase().endsWith('.zip')) {
            this.fileDropWrongExtension.set(true);
            setTimeout(() => this.fileDropWrongExtension.set(false), 1500);
            return;
        }

        this.onBulkFileSelected(files);
    }

    async onFilesSelected(event: any, fileUpload: HTMLInputElement) {
        await this.onBulkFileSelected(event.target.files);
        fileUpload.value = "";
    }

    private async onBulkFileSelected(files: File[]) {     
        if(files.length === 0) return;

        const file = files[0];

        const nameAndExtension = toNameAndExtension(file.name);
        const entries = signal<BulkUploadZipEntry[] | null>(null);

        const pendingBulkUpload: SingleBulkFileUpload = {
            externalId: `bfu_${getBase62Guid()}`,
            fileName: nameAndExtension.name,
            fileExtension: nameAndExtension.extension,
            fullFileName: `${nameAndExtension.name}${nameAndExtension.extension}`,
            fileSize: file.size,
            entries,
            archive: signal(null),
            file,
            
            searchPhrase: signal(''),
            isOpened: signal(false),
            isBroken: signal(false),

            doesIncludeDeflateCompression: computed(() => {
                const entriesVal = entries();
                if(!entriesVal) return false;
                return entriesVal.some(e => e.compressionMethod === DEFLATE_COMPRESSION_CODE);
            }),

            doesIncludeNotSupportedCompression: computed(() => {
                const entriesVal = entries();
                if(!entriesVal) return false;
                return entriesVal.some(e => 
                    e.compressionMethod !== NO_COMPRESSION_CODE && 
                    !(e.compressionMethod === DEFLATE_COMPRESSION_CODE && this.isDecompressionSupported)
                );
            })
        };
    
        this.bulkUpload().archive.set(pendingBulkUpload);

        const readZipResult = await this.readZipEntries(pendingBulkUpload.file);

        if (readZipResult.isBroken) {
            pendingBulkUpload.isBroken.set(true);
        } else {
            const zipEntries = readZipResult.entries.map(entry => this.mapToZipEntry(entry));
            const archive = ZipArchives.getStructure(zipEntries);

            pendingBulkUpload.entries.set(zipEntries);
            pendingBulkUpload.archive.set(archive);
        }
    }

    private async readZipEntries(file: File): Promise<{ entries: ZipCdfhRecord[], isBroken: boolean }> {
        return await Zip.readZipFile(file);
    }

    private mapToZipEntry(cdfh: ZipCdfhRecord): BulkUploadZipEntry {
        const nameAndExt = this.filePathToNameAndExtensions(cdfh.fileName);

        return {
            fileName: nameAndExt.name,
            fileExtension: nameAndExt.extension,
            filePath: cdfh.fileName,
            compressedSizeInBytes: cdfh.compressedSize,
            sizeInBytes: cdfh.uncompressedSize,
            offsetToLocalFileHeader: cdfh.offsetToLocalFileHeader,
            fileNameLength: cdfh.fileNameLength,
            compressionMethod: cdfh.compressionMethod,
            indexInArchive: cdfh.indexInArchive,            
        };
    }

    private filePathToNameAndExtensions(filePath: string) {
        const path = filePath.split('/');     
        return toNameAndExtension(path[path.length - 1]);   
    }
}
