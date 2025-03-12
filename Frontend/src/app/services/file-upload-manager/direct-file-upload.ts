import { WritableSignal, Signal, signal, computed } from "@angular/core";
import { IFileUpload } from "./file-upload-manager";
import { FileUploadDetails, FileUploadUtils, MAXIMUM_PARALLEL_UPLOADS } from "./file-upload-utils";

export class DirectFileUpload implements IFileUpload  {
    public type = 'DirectFileUpload';

    public uploadProgressPercentage: Signal<number> = computed(() =>  this._isUploadFinished() ? 100 : 0);
    private _isUploadFinished = signal(false);

    public isPaused = signal(false);
    private _abortController: AbortController | null = null;
    private _uploadPromise: Promise<{
        fileExternalId: string;
    } | null> | null = null;

    constructor(
        private _activeUploads: Promise<void>[],
        public details: FileUploadDetails
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

    public async upload(preSignedUploadLink: string): Promise<{ fileExternalId: string; } | null> {
        this._abortController = new AbortController();
        this._uploadPromise = this.uploadFile(this._abortController.signal, preSignedUploadLink);
        return await this._uploadPromise;
    }

    private async uploadFile(abortSignal: AbortSignal, preSignedUploadLink: string): Promise<{ fileExternalId: string } | null> {
        let directUploadPromise: Promise<string> | null = null;
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

                const wholeBlob = await this
                    .details
                    .fileSlicer
                    .takeWhole();

                directUploadPromise = FileUploadUtils
                    .uploadBlob(
                        preSignedUploadLink, 
                        wholeBlob, 
                        this.details.contentType, 
                        abortSignal)
                    .then(async reponse => {
                        const result = await reponse.json();
                        
                        return result as string;
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

        // Wait for any remaining uploads to complete
        const fileExternalId = await directUploadPromise;

        this._isUploadFinished.set(true);

        if(this.details.reportProgressCallback) {
            this.details.reportProgressCallback(this.details.fileSizeInBytes);
        }

        return { 
            fileExternalId: fileExternalId!
        };        
    }
}