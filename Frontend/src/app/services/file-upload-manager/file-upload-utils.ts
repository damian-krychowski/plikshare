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

    public static async uploadBlob(args: {
        url: string, 
        file: Blob, 
        contentType: string, 
        abortSignal: AbortSignal,
        additionalHeaders?: Record<string, string>
    }): Promise<Response> {              
        const headers: Record<string, string> = {
            'Content-Type': args.contentType
        };

        if (args.additionalHeaders) {
            Object.assign(headers, args.additionalHeaders);
        }

        const response = await fetch(args.url, {
            method: 'PUT',
            body: args.file,
            headers: headers,
            signal: args.abortSignal
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