import { IFileSlicer } from "./file-upload-manager";

export const MAXIMUM_PARALLEL_UPLOADS = 5;
export const MAXIMUM_PENDING_UPLOADS = 30;

export class FileUploadUtils {
    public static preparePartNumbersToUpload(
        allPartsCount: number,
        alreadyUploadedPartNumbers: Set<number>): number[] {
        const partNumbers: number[] = [];
        
        for(let partNumber = 1; partNumber <= allPartsCount; partNumber++) {
            if(!alreadyUploadedPartNumbers.has(partNumber)){
                partNumbers.push(partNumber);
            }
        }

        return partNumbers;
    }

    public static async uploadBlob(url: string, file: Blob, contentType: string, signal: AbortSignal): Promise<Response> {
        const response = await fetch(url, {
            method: 'PUT',
            body: file,
            headers: {
                'Content-Type': contentType
            },
            signal: signal
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        return response;
    }
}

export type FileUploadDetails = {
    uploadExternalId: string;
    fileSizeInBytes: number;
    contentType: string;
    allPartsCount: number;
    alreadyUploadedPartNumbers: number[];
    fileSlicer: IFileSlicer;
        
    reportProgressCallback?: (alreadyUploadedBytes: number) => void;
    reportUploadFinishedCallback?: () => void;
}