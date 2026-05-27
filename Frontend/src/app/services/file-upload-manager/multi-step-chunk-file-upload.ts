import { WritableSignal, Signal, signal, computed } from "@angular/core";
import { IFileSlicer, FileUploadApi, IFileUpload } from "./file-upload-manager";
import { FileUploadDetails, FileUploadUtils, MAXIMUM_PARALLEL_UPLOADS } from "./file-upload-utils";
import { HttpHeadersFactory } from "../../files-explorer/http-headers-factory";



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

    // Created lazily in upload() so the slicer's resources (decompression
    // stream, reader lock) are only live for the duration of the upload.
    private _slicer: IFileSlicer | null = null;

    constructor(
        private _httpHeadersFactory: HttpHeadersFactory,
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
        this._slicer = this.details.createSlicer();
        try {
            this._uploadPromise = this.uploadFile(this._abortController.signal);
            return await this._uploadPromise;
        } finally {
            this._slicer.dispose();
            this._slicer = null;
        }
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

                // Push BEFORE awaiting — otherwise the .finally above fires
                // during the await with indexOf returning -1, and splice(-1, 1)
                // silently removes some other in-flight upload's promise from
                // the shared _activeUploads array. That corrupts concurrency
                // accounting across files: parallel uploads from store-mode
                // entries get kicked out, _activeUploads.length under-counts,
                // and the manager keeps launching new fetches well past
                // MAXIMUM_PARALLEL_UPLOADS — which is the actual OOM trigger
                // for deflate zips (CompressedBlobSlicer is the only slicer
                // with canBeProcessedInParallel() == false).
                this._activeUploads.push(uploadPromise);
                fileActiveUplaods.push(uploadPromise);

                if(!this._slicer!.canBeProcessedInParallel()) {
                    await uploadPromise;
                }
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

            const partBlob = await this._slicer!.takeSlice(
                initiatePartUploadResult.startsAtByte,
                initiatePartUploadResult.endsAtByte + 1);

            const partUpload: any = await FileUploadUtils.uploadBlob({
                url:initiatePartUploadResult.uploadPreSignedUrl,
                file: partBlob,
                contentType: this.details.contentType,
                abortSignal: abortSignal,
                additionalHeaders: this._httpHeadersFactory.prepareAdditionalHttpHeaders()
            });           

            this.markPartNumberAsUploaded(partNumber);

            const callback = initiatePartUploadResult.completeCallback;

            if (callback) {
                let etag: string | null = null;

                if (callback.eTagSourceHeader) {
                    etag = partUpload.headers
                        .get(callback.eTagSourceHeader)
                        ?.replace(/"/g, "") ?? null;

                    if (etag === null) {
                        throw new Error(`Expected response header '${callback.eTagSourceHeader}' is missing.`);
                    }
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