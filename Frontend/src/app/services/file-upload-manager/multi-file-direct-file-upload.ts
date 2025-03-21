import { Signal, signal, computed } from "@angular/core";
import { IFileUpload } from "./file-upload-manager";
import { FileUploadDetails, MAXIMUM_PARALLEL_UPLOADS } from "./file-upload-utils";
import { XSRF_TOKEN_HEADER_NAME } from "../../shared/xsrf";

export class MultiFileDirectFileUpload implements IFileUpload {
    public type = 'MultiFileDirectFileUpload';

    public uploadProgressPercentage: Signal<number> = computed(() =>  this._isUploadFinished() ? 100 : 0);
    private _isUploadFinished = signal(false);

    public isPaused = signal(false);
    private _abortController: AbortController | null = null;
    private _uploadPromise: Promise<{ fileExternalId: string; uploadExternalId: string }[] | null> | null = null

    constructor(
        private _activeUploads: Promise<void>[],
        public detailsList: FileUploadDetails[],
        private _getXsrfTokenFunc: () => string
    ) {
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

    public async upload(preSignedUploadLink: string): Promise<{ fileExternalId: string; uploadExternalId: string }[] | null> {
        this._abortController = new AbortController();
        this._uploadPromise = this.uploadFiles(this._abortController.signal, preSignedUploadLink);
        return await this._uploadPromise;
    }

    private async uploadFiles(abortSignal: AbortSignal, preSignedUploadLink: string): Promise<{ fileExternalId: string; uploadExternalId: string }[] | null> {
        const formData = new FormData();
        const fileExternalIds: string[] = [];

        // Calculate total size
        let totalSizeInBytes = 0;
        for (const fileDetails of this.detailsList) {
            totalSizeInBytes += fileDetails.fileSizeInBytes;
        }
        
        for (const fileDetails of this.detailsList) {
            if (abortSignal.aborted) {
                return null;
            }

            const wholeBlob = await fileDetails.fileSlicer.takeWhole();
            formData.append('files', wholeBlob, fileDetails.uploadExternalId);
            fileExternalIds.push(fileDetails.uploadExternalId);
        }

        let directUploadPromise: Promise<{ fileExternalId: string; uploadExternalId: string }[]> | null = null;
        let shouldRun = true;

        while (shouldRun) {
            if (abortSignal.aborted) {
                return null;
            }

            if (this.isPaused()) {
                await new Promise(resolve => setTimeout(resolve, 200));
                continue;
            }

            if (this._activeUploads.length < MAXIMUM_PARALLEL_UPLOADS) {
                shouldRun = false;

                directUploadPromise = fetch(preSignedUploadLink, {
                    method: 'POST',
                    headers: {
                        'x-total-size-in-bytes': totalSizeInBytes.toString(),
                        'x-number-of-files': this.detailsList.length.toString(),
                        [XSRF_TOKEN_HEADER_NAME]: this._getXsrfTokenFunc()
                    },
                    body: formData,
                    signal: abortSignal
                })
                .then(async response => {
                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }
                    const result = await response.json();
                    return result as { fileExternalId: string; uploadExternalId: string }[];
                });

                const uploadPromise = directUploadPromise
                    .then(() => {})
                    .finally(() => this._activeUploads.splice(this._activeUploads.indexOf(uploadPromise), 1));

                this._activeUploads.push(uploadPromise);
            } else {
                await Promise.race(this._activeUploads);
            }
        }

        if(!directUploadPromise)
            return null;

        const result = await directUploadPromise;
        
        this._isUploadFinished.set(true);
                        
        for (const file of this.detailsList) {
            if(file.reportProgressCallback) {
                file.reportProgressCallback(file.fileSizeInBytes);
            }

            if(file.reportUploadFinishedCallback) {
                file.reportUploadFinishedCallback();
            }
        }

        return result;
    }
}