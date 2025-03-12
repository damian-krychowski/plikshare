import { IFileSlicer } from "./file-upload-manager";

export class FileSlicer implements IFileSlicer {
    constructor(private _file: File){}

    canBeProcessedInParallel(){
        return true;
    }

    async takeSlice(start: number, end: number) {
        return this._file.slice(start, end);
    };

    async takeWhole () {
        return this._file;
    }
}