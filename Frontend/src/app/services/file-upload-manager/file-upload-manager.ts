import { Injectable, Signal, signal, computed, WritableSignal } from "@angular/core";
import { Subject } from "rxjs";
import { AppUploadItem } from "../../files-explorer/upload-item/upload-item.component";
import { BulkInitiateFileUploadRequest, BulkInitiateFileUploadResponse, InitiateFileUploadRequest, InitiateFileUploadResponse, UploadsApi } from "../uploads.api";
import { FileUploadDetails, MAXIMUM_PENDING_UPLOADS } from "./file-upload-utils";
import { MultiStepChunkFileUpload } from "./multi-step-chunk-file-upload";
import { SingleChunkFileUpload } from "./single-chunk-file-upload";
import { toNameAndExtension } from "../filte-type";
import { getBase62Guid } from "../guid-base-62";
import { MultiFileDirectFileUpload } from "./multi-file-direct-file-upload";

export type UploadAlgorithm = "direct-upload" | "single-chunk-upload" | "multi-step-chunk-upload";

export type UploadDetails = {
    algorithm: UploadAlgorithm;
    preSignedUploadLink: string | null
};

export interface IFileSlicer {
    takeSlice: (start: number, end: number) => Promise<Blob>;
    takeWhole: () => Promise<Blob>;
    canBeProcessedInParallel: () => boolean;
}

export interface FileUploadApi {
    bulkInitiateUpload(request: BulkInitiateFileUploadRequest): Promise<BulkInitiateFileUploadResponse>;

    getUploadDetails(externalId: string): Promise<{
        alreadyUploadedPartNumbers: number[];
        expectedPartsCount: number;
    }>;

    initiatePartUpload(externalId: string, partNumber: number): Promise<{
        uploadPreSignedUrl: string;
        startsAtByte: number;
        endsAtByte: number;
        isCompleteFilePartUploadCallbackRequired: boolean;
    }>;

    completePartUpload(externalId: string, partNumber: number, request: {
        eTag: string;
    }): Promise<void>

    completeUpload(externalId: string): Promise<{
        fileExternalId: string;
    }>;

    abort(externalId: string): Promise<void>;

    getXsrfToken(): string;
}

export type UploadCompletedEvent = {
    uploadExternalId: string;
    fileExternalId: string;
}

//todo handle workspace too!
export type UploadsInitiatedEvent = {
    uploads: AppUploadItem[];
}

export type UploadAbortedEvent = {
    uploadExternalIds: string[];
}

export interface IFileUpload {
    uploadProgressPercentage: Signal<number>;
    isPaused: Signal<boolean>;

    pause: () => void;
    resume: () => void;
}

type FileUpload =  SingleFileUpload | MultiFileDirectFileUpload;
type SingleFileUpload = MultiStepChunkFileUpload | SingleChunkFileUpload


//todo handle workspace/box too!
export type FileToUpload = {
    folderExternalId: string | null;
    contentType: string;
    name: string;
    size: number;
    slicer: IFileSlicer;

    reportProgressCallback?: (alreadyUploadedBytes: number) => void
    reportUploadFinishedCallback?: () => void;
}

type PreInitiationFileToUpload = {
    uploadItem: AppUploadItem;
    fileSlicer: IFileSlicer;
    
    reportProgressCallback?: (alreadyUploadedBytes: number) => void
    reportUploadFinishedCallback?: () => void;
}

type InitiatedFileToUpload = {
    uploadItem: AppUploadItem;
    fileSlicer: IFileSlicer;
    initiateUploadResult: InitiateFileUploadResponse;
    
    reportProgressCallback?: (alreadyUploadedBytes: number) => void
    reportUploadFinishedCallback?: () => void;
}

const BATCH_SIZE = 30;
const SMALL_FILE_THRESHOLD = 1024 * 1024; // 1MB in bytes
const SMALL_FILES_BATCH_SIZE = 10 * 1024 * 1024; // 10MB in bytes
const PRE_GROUPING_SMALL_FILES_BATCH_SIZE = SMALL_FILES_BATCH_SIZE * 2; //2 parallel groups

@Injectable({
    providedIn: 'root'
})
export class FileUploadManager {
    public readonly pendingQueueSize: WritableSignal<number> = signal(0);
    
    private smallFilesQueue: FileToUpload[] = [];
    private largeFilesQueue: FileToUpload[] = [];
    private isProcessing = false;

    private _uploadsCountChangedSubject: Subject<number> = new Subject();
    public readonly uploadsCountChanged$ = this._uploadsCountChangedSubject.asObservable();

    private _fileUploads: FileUpload[] = [];
    private _activeUploads: Promise<void>[] = [];

    public uploadsInitiated: Subject<UploadsInitiatedEvent> = new Subject();
    public uploadCompleted: Subject<UploadCompletedEvent> = new Subject();
    public uploadAborted: Subject<UploadAbortedEvent> = new Subject();

    //todo passing uploadsApi like here wont work on cross workspace/boxes scenarios -gonna to figure it out later
    public addFiles(files: FileToUpload[], uploadsApi: FileUploadApi) {
        for (const file of files) {
            if (file.size <= SMALL_FILE_THRESHOLD) {
                this.smallFilesQueue.push(file);
            } else {
                this.largeFilesQueue.push(file);
            }
        }
        
        this.updateQueueSize();
        
        if (!this.isProcessing) {
            this.processQueue(uploadsApi);
        }
    }

    private updateQueueSize(): void {
        this.pendingQueueSize.set(this.smallFilesQueue.length + this.largeFilesQueue.length);
    }

    private selectBatch(): FileToUpload[] {
        let totalSize = 0;
        const batch: FileToUpload[] = [];

        // First ensure we have at least BATCH_SIZE small files (if available)
        while (this.smallFilesQueue.length > 0 && batch.length < BATCH_SIZE) {
            const nextFile = this.smallFilesQueue.shift()!;
            batch.push(nextFile);
            totalSize += nextFile.size;
        }

        // Then keep adding more small files until we hit 10MB x 3 so that at least 2 groups are running in parallel
        while (this.smallFilesQueue.length > 0 && totalSize <= PRE_GROUPING_SMALL_FILES_BATCH_SIZE) {
            const nextFile = this.smallFilesQueue[0];
            if (totalSize + nextFile.size <= PRE_GROUPING_SMALL_FILES_BATCH_SIZE) {
                batch.push(this.smallFilesQueue.shift()!);
                totalSize += nextFile.size;
            } else {
                break;
            }
        }

        // If we have fewer small files than BATCH_SIZE and haven't reached 10MB,
        // then we can add large files up to BATCH_SIZE limit
        if (batch.length < BATCH_SIZE && totalSize < SMALL_FILES_BATCH_SIZE && this.largeFilesQueue.length > 0) {
            const remainingBatchCapacity = BATCH_SIZE - batch.length;
            const largeFilesBatch = this.largeFilesQueue.splice(0, remainingBatchCapacity);
            batch.push(...largeFilesBatch);
        }

        return batch;
    }

    private async processQueue(uploadsApi: FileUploadApi) {
        this.isProcessing = true;

        while (this.smallFilesQueue.length > 0 || this.largeFilesQueue.length > 0) {
            while (this.processingCapacityLeft() <= 0) {
                await new Promise(resolve => setTimeout(resolve, 50));
            }

            const batchToUpload = this.selectBatch();
            
            try {
                //todo that can be improved
                const preInitiationFileToUploadsMap = this.mapToUploadItems(
                    batchToUpload);

                this.uploadsInitiated.next({
                    uploads: Array.from(
                        preInitiationFileToUploadsMap.values(), 
                        item => item.uploadItem)
                });

                var bulkUploadInitiateResult = await uploadsApi.bulkInitiateUpload({
                    items: Array.from(
                        preInitiationFileToUploadsMap.values(), 
                        item => {
                            const request: InitiateFileUploadRequest = {
                                fileContentType: item.uploadItem.fileContentType,
                                fileNameWithExtension: `${item.uploadItem.fileName()}${item.uploadItem.fileExtension}`,
                                fileSizeInBytes: item.uploadItem.fileSizeInBytes,
                                fileUploadExternalId: item.uploadItem.externalId,
                                folderExternalId: item.uploadItem.folderExternalId
                            };
            
                            return request;
                        })
                });

                var initiatedFileToUploads = this.mapInitiatedFileToUploads(
                    preInitiationFileToUploadsMap,
                    bulkUploadInitiateResult.items);

                var directUploads = initiatedFileToUploads
                    .filter(x => x.initiateUploadResult.algorithm === 'direct-upload');

                var restOfUploads = initiatedFileToUploads
                    .filter(x => x.initiateUploadResult.algorithm !== 'direct-upload');

                if(directUploads.length > 0) {
                    const groups = this.groupDirectUploadsBySize(
                        directUploads); 
                        
                    for(const group of groups) {
                        const fileUploadDetails = group.map(upload => {
                            const details: FileUploadDetails = {
                                uploadExternalId: upload.uploadItem.externalId,
                                allPartsCount: upload.initiateUploadResult.expectedPartsCount,
                                alreadyUploadedPartNumbers: [],
                                contentType: upload.uploadItem.fileContentType,
                                fileSlicer: upload.fileSlicer,
                                fileSizeInBytes: upload.uploadItem.fileSizeInBytes,
                                reportProgressCallback: upload.reportProgressCallback,
                                reportUploadFinishedCallback: upload.reportUploadFinishedCallback,
                            };

                            return details;
                        });

                        var multiFileDirectUpload = new MultiFileDirectFileUpload(
                            this._activeUploads,
                            fileUploadDetails,
                            () => uploadsApi.getXsrfToken());

                        var promise = multiFileDirectUpload.upload(
                            bulkUploadInitiateResult.preSignedMultiFileDirectUploadLink!);

                        this.handleMultiFileDirectUpload({
                            fileUpload: multiFileDirectUpload,
                            uploadPromise: promise
                        });
                    }                    
                }
        
                for (const upload of restOfUploads) {
                    this.uploadFile({
                        file: upload,
                        uploadsApi: uploadsApi
                    });   
                }
            } catch (error) {
                console.error('Failed to initiate batch:', error);
                // Return failed files to appropriate queues
                for (const file of batchToUpload) {
                    if (file.size <= SMALL_FILE_THRESHOLD) {
                        this.smallFilesQueue.unshift(file);
                    } else {
                        this.largeFilesQueue.unshift(file);
                    }
                }
            }
            
            this.updateQueueSize();
        }

        this.isProcessing = false;
    }

    /**
     * Groups direct uploads using Best Fit bin packing algorithm to minimize the number of groups while respecting size limit.
     * 
     * Algorithm steps:
     * 1. Sort files by size in descending order (optimization for bin packing)
     * 2. For each file, find the group with smallest remaining space that can still fit the file
     * 3. If no suitable group exists, create a new one
     * 
     * Time complexity: O(n * m) where n is number of files and m is number of groups
     * Space complexity: O(n) for storing the groups
     * 
     * This typically produces better results than First Fit or Next Fit algorithms,
     * especially with varied file sizes, as it tries to minimize wasted space in each group.
     */
    private groupDirectUploadsBySize(
        directUploads: InitiatedFileToUpload[]
    ) {
        type Group = {
            items: InitiatedFileToUpload[];
            currentSize: number;
        };

        const groups: Group[] = [];

        // Sort files by size in descending order for better bin packing
        const sortedUploads = [...directUploads].sort((a, b) => {
            return b.uploadItem.fileSizeInBytes - a.uploadItem.fileSizeInBytes; // Descending order
        });

        for (const upload of sortedUploads) {            
            // Find the best fitting group (one with smallest remaining space that can fit the file)
            let bestGroupIndex = -1;
            let bestRemainingSpace = SMALL_FILES_BATCH_SIZE;

            groups.forEach((group, index) => {
                const remainingSpace = SMALL_FILES_BATCH_SIZE - group.currentSize;

                // "&& group.items.length < 50" is my temporary idea how to improve responsiveness for s3 uploads, which has rate limit
                // which backend needs to adhere to. Probably there should be additional data per storage controlling what is the best
                // algorithm to make groups for bulk uploads
                if (remainingSpace >= upload.uploadItem.fileSizeInBytes && remainingSpace < bestRemainingSpace && group.items.length < 50) {
                    bestGroupIndex = index;
                    bestRemainingSpace = remainingSpace;
                }
            });

            if (bestGroupIndex !== -1) {
                // Add to existing group
                groups[bestGroupIndex].items.push(upload);
                groups[bestGroupIndex].currentSize += upload.uploadItem.fileSizeInBytes;
            } else {
                // Create new group
                groups.push({
                    items: [upload],
                    currentSize: upload.uploadItem.fileSizeInBytes
                });
            }
        }

        return groups.map(g => g.items);
    }

    private mapToUploadItems(batchToUpload: FileToUpload[]): Map<string, PreInitiationFileToUpload> {
        const result: Map<string, PreInitiationFileToUpload> = new Map();
        
        for (const file of batchToUpload) {
            const nameAndExtension = toNameAndExtension(file.name);

            const uploadItem: AppUploadItem = {
                type: 'upload',
                externalId: `fu_${getBase62Guid()}`,
                folderExternalId: file.folderExternalId,
                fileName: signal(nameAndExtension.name),
                fileExtension: nameAndExtension.extension,
                fileContentType: file.contentType,
                fileSizeInBytes: file.size,
                alreadyUploadedPartNumbers: [],
                fileUpload: signal(undefined),
                isSelected: signal(false),
                isCut: signal(false)
            };

            const preInitiationUpload: PreInitiationFileToUpload = {
                uploadItem: uploadItem,
                fileSlicer: file.slicer,
                reportProgressCallback: file.reportProgressCallback,
                reportUploadFinishedCallback: file.reportUploadFinishedCallback,
                
            };

            result.set(uploadItem.externalId, preInitiationUpload);
        }

        return result;
    }

    //improve to O(1)
    private mapInitiatedFileToUploads(
        preInitiationUploads: Map<string, PreInitiationFileToUpload>, 
        initiateResults: InitiateFileUploadResponse[]): InitiatedFileToUpload[]{
        var results: InitiatedFileToUpload[] = [];

        for (const initiateResult of initiateResults) {
            const preInitation = preInitiationUploads.get(
                initiateResult.uploadExternalId);

            if(!preInitation)
                throw new Error(`InitiateFileUploadResponse for UploadExternalId '${initiateResult.uploadExternalId}' was not found`);

            results.push({
                uploadItem: preInitation.uploadItem,
                fileSlicer: preInitation.fileSlicer,
                reportProgressCallback: preInitation.reportProgressCallback,
                reportUploadFinishedCallback: preInitation.reportUploadFinishedCallback,

                initiateUploadResult: initiateResult,
            });
        }

        return results;
    }

    public processingCapacityLeft() {
        return MAXIMUM_PENDING_UPLOADS - (this._activeUploads.length + this._fileUploads.length);
    }

    public uploadFile(args: {
        file: InitiatedFileToUpload,
        uploadsApi: FileUploadApi
    }) {
        const upload = this.createFileUpload(args);
        
        args.file.uploadItem.fileUpload.set(
            upload.fileUpload);
               
        //we dont await this on purpose
        this.handleUpload(upload);

        return;
    }

    //todo test this for different algorithms
    public async resumeUpload(args: {
        contentType: string,
        fileSlicer: IFileSlicer,
        uploadExternalId: string,
        uploadsApi: FileUploadApi,
        fileSizeInBytes: number
    }, callbacks: {
        uploadResumed: (args: { fileUpload: IFileUpload }) => void;
    }) {
        const uploadDetails = await args.uploadsApi.getUploadDetails(
            args.uploadExternalId);       

        const fileUploadDetails: FileUploadDetails = {
            uploadExternalId: args.uploadExternalId,
            contentType: args.contentType,
            allPartsCount: uploadDetails.expectedPartsCount,
            alreadyUploadedPartNumbers: uploadDetails.alreadyUploadedPartNumbers,
            fileSizeInBytes: args.fileSizeInBytes,
            fileSlicer: args.fileSlicer
        };

        const fileUpload = new MultiStepChunkFileUpload(
            this._activeUploads,
            args.uploadsApi,
            fileUploadDetails);

        callbacks.uploadResumed({
            fileUpload: fileUpload
        });

        this.handleUpload({
            fileUpload: fileUpload,
            uploadPromise: fileUpload.upload()
        });

        return;
    }

    private async handleUpload(args: {
        fileUpload: SingleFileUpload,
        uploadPromise: Promise<{fileExternalId: string} | null>
    }) {
        this.addFileUpload(args.fileUpload);

        const completedResult = await args.uploadPromise;

        if (completedResult === null) {
            this.uploadAborted.next({
                uploadExternalIds: [args.fileUpload.details.uploadExternalId]
            });
        } else {
            this.uploadCompleted.next({
                uploadExternalId: args.fileUpload.details.uploadExternalId,
                fileExternalId: completedResult.fileExternalId,
            });
        }

        this.removeFileUpload(args.fileUpload);
    }

    private async handleMultiFileDirectUpload(args: {
        fileUpload: MultiFileDirectFileUpload,
        uploadPromise: Promise<{ fileExternalId: string; uploadExternalId: string }[] | null>
    }) {
        this.addFileUpload(args.fileUpload);

        const completedResults = await args.uploadPromise;

        if (completedResults === null) {
            this.uploadAborted.next({
                uploadExternalIds: args.fileUpload.detailsList.map(d => d.uploadExternalId)
            });
        } else {
            for (const result of completedResults) {
                this.uploadCompleted.next({
                    uploadExternalId: result.uploadExternalId,
                    fileExternalId: result.fileExternalId,
                });                
            }
        }

        this.removeFileUpload(args.fileUpload);
    }

    private createFileUpload(args: {
        file: InitiatedFileToUpload,
        uploadsApi: FileUploadApi
    }): { fileUpload: SingleFileUpload, uploadPromise: Promise<{fileExternalId: string} | null>} {
        const algorithm = args.file.initiateUploadResult.algorithm;
        
        const details: FileUploadDetails = {
            uploadExternalId: args.file.initiateUploadResult.uploadExternalId,
            allPartsCount: args.file.initiateUploadResult.expectedPartsCount,
            alreadyUploadedPartNumbers: [],
            contentType: args.file.uploadItem.fileContentType,
            fileSlicer: args.file.fileSlicer,
            fileSizeInBytes: args.file.uploadItem.fileSizeInBytes,
            reportProgressCallback: args.file.reportProgressCallback,
            reportUploadFinishedCallback: args.file.reportUploadFinishedCallback
        };

        if(algorithm === 'direct-upload'){
            throw new Error("Direct uploads should be handled by multi-file-direct-upload.")
        }

        if(algorithm === 'single-chunk-upload'){            
            const fileUpload = new SingleChunkFileUpload(
                this._activeUploads,
                args.uploadsApi,
                details
            );

            const uploadPromise = fileUpload.upload(
                args.file.initiateUploadResult.preSignedUploadLink!);

            return {fileUpload, uploadPromise};
        }

        if(algorithm === 'multi-step-chunk-upload') {
            const fileUpload =  new MultiStepChunkFileUpload(
                this._activeUploads,
                args.uploadsApi,
                details
            );

            const uploadPromise = fileUpload.upload();

            return {fileUpload, uploadPromise};
        }

        throw new Error("Unknown upload algorithm: " + algorithm);
    }

    public async abortUpload(uploadExternalId: string) {
        const fileUpload = this.findFileUpload(uploadExternalId);

        if (fileUpload) {
            await fileUpload.abort();
            this.removeFileUpload(fileUpload);
        }
    }

    public async abortUploads(uploadExternalIds: string[]) {
        const promises = uploadExternalIds.map(
            uploadExternalId => this.abortUpload(uploadExternalId));

        await Promise.all(promises);
    }

    public getFileUploadRef(uploadExternalId: string): IFileUpload | undefined {
        return this.findFileUpload(uploadExternalId);
    }

    private addFileUpload(fileUpload: FileUpload) {
        this._fileUploads.push(fileUpload)
        this._uploadsCountChangedSubject.next(this._activeUploads.length + this._fileUploads.length);
    }

    private removeFileUpload(fileUpload: FileUpload) {
        this._fileUploads = this._fileUploads.filter(upload => upload !== fileUpload);
        this._uploadsCountChangedSubject.next(this._activeUploads.length + this._fileUploads.length);
    }

    private findFileUpload(uploadExternalId: string) {
        for (const fileUpload of this._fileUploads) {
            if(fileUpload instanceof MultiFileDirectFileUpload) {
                const matchingUpload = fileUpload.detailsList.find(d => d.uploadExternalId === uploadExternalId);

                if(matchingUpload) {
                    return fileUpload
                }
            } else {
                if(fileUpload.details.uploadExternalId === uploadExternalId) {
                    return fileUpload;
                }
            }
        }

        return undefined;
    }
}
