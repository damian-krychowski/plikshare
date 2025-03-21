import { WritableSignal, Signal, signal, computed } from "@angular/core";
import { IFileSlicer, FileUploadApi, IFileUpload } from "./file-upload-manager";
import { FileUploadDetails, FileUploadUtils, MAXIMUM_PARALLEL_UPLOADS } from "./file-upload-utils";

export class MultiStepChunkFileUpload implements IFileUpload  {
    public type = 'MultiStepChunkFileUpload';

    private _alreadyUploadedPartsCount: WritableSignal<number>;
    public uploadProgressPercentage: Signal<number>;
    
    private _uploadedPartNumbersSet: Set<number>;
    private _partNumbersToUpload: number[];

    public isPaused = signal(false);
    private _abortController: AbortController | null = null;
    private _uploadPromise: Promise<{
        fileExternalId: string;
    } | null> | null = null;

    constructor(
        private _activeUploads: Promise<void>[],
        private _uploadsApi: FileUploadApi,        
        public details: FileUploadDetails
    ) {
        this._uploadedPartNumbersSet = new Set(details.alreadyUploadedPartNumbers);
        this._alreadyUploadedPartsCount = signal(this._uploadedPartNumbersSet.size);
        
        this._partNumbersToUpload = FileUploadUtils.preparePartNumbersToUpload(
            details.allPartsCount,
            this._uploadedPartNumbersSet);

        this.uploadProgressPercentage = computed(() => {
            return Math.round((this._alreadyUploadedPartsCount() / details.allPartsCount) * 100);
        });
    }

    private markPartNumberAsUploaded(partNumber: number) {
        this._uploadedPartNumbersSet.add(partNumber);
        this._alreadyUploadedPartsCount.set(this._uploadedPartNumbersSet.size);
    }

    public resume() {
        this.isPaused.set(false);
    }

    public pause() {
        this.isPaused.set(true);
    }

    public async abort() {
        if (this._abortController) {
            this._abortController.abort("User requested abort");
            this._abortController = null;

            await this._uploadPromise;
        }
    }

    public async upload(): Promise<{ fileExternalId: string; } | null> {
        this._abortController = new AbortController();
        this._uploadPromise = this.uploadFile(this._abortController.signal);
        return await this._uploadPromise;
    }

    private async uploadFile(abortSignal: AbortSignal): Promise<{ fileExternalId: string } | null> {
        // Array to keep track of active uploads
        let currentIndex = 0;
        const fileActiveUplaods: Promise<void>[] = [];

        // Function to manage uploads
        while (currentIndex < this._partNumbersToUpload.length) {
            if (abortSignal.aborted) {
                return null;
            }

            if (this.isPaused()) {
                await new Promise(resolve => setTimeout(resolve, 200));
                continue;
            }

            if (this._activeUploads.length < MAXIMUM_PARALLEL_UPLOADS) {
                const partNumber = this._partNumbersToUpload[currentIndex];

                currentIndex = currentIndex + 1;

                const uploadPromise = this
                    .uploadFilePart(partNumber, abortSignal)
                    .finally(() => {
                        this._activeUploads.splice(this._activeUploads.indexOf(uploadPromise), 1);
                        fileActiveUplaods.splice(fileActiveUplaods.indexOf(uploadPromise), 1);
                    });

                if(!this.details.fileSlicer.canBeProcessedInParallel()) {
                    await uploadPromise;
                }

                this._activeUploads.push(uploadPromise);
                fileActiveUplaods.push(uploadPromise);
            } else {
                await Promise.race(this._activeUploads);
            }
        }

        // Wait for any remaining uploads to complete
        await Promise.all(fileActiveUplaods);

        // Committing upload
        const result = await this._uploadsApi.completeUpload(
            this.details.uploadExternalId);

        return result;        
    }

    private async uploadFilePart(partNumber: number, abortSignal: AbortSignal): Promise<void> {
        try {
            const initiatePartUploadResult = await this._uploadsApi.initiatePartUpload(
                this.details.uploadExternalId,
                partNumber);

            const partBlob = await this.details.fileSlicer.takeSlice(
                initiatePartUploadResult.startsAtByte,
                initiatePartUploadResult.endsAtByte + 1);

            const partUpload: any = await FileUploadUtils.uploadBlob({
                url:initiatePartUploadResult.uploadPreSignedUrl,
                file: partBlob,
                contentType: this.details.contentType,
                abortSignal: abortSignal
            });           

            this.markPartNumberAsUploaded(partNumber);
            
            if(initiatePartUploadResult.isCompleteFilePartUploadCallbackRequired) {
                const etag = partUpload.headers
                    .get('Etag')
                    ?.replace(/"/g, "");

                    if (etag == undefined) {
                        throw new Error('ETag is not defined.');
                    }    

                await this._uploadsApi.completePartUpload(
                    this.details.uploadExternalId,
                    partNumber, {
                    eTag: etag
                });
            }

            if(this.details.reportProgressCallback) {
                this.details.reportProgressCallback(partBlob.size);
            }

            if(this.details.allPartsCount == partNumber && this.details.reportUploadFinishedCallback) {
                this.details.reportUploadFinishedCallback();
            }
        } catch (error: any) {
            if(error === "User requested abort") {
                return;
            }

            console.error(error);
        }
    }
}