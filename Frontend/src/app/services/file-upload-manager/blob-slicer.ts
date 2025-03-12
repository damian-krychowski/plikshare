import { IFileSlicer } from "./file-upload-manager";

export class BlobSlicer implements IFileSlicer {
    constructor(private _blob: Blob){}

    canBeProcessedInParallel(){
        return true;
    }

    async takeSlice(start: number, end: number) {
        return this._blob.slice(start, end);
    };

    async takeWhole () {
        return this._blob;
    }
}