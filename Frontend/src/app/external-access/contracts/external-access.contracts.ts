import { GetFolderResponse } from "../../services/folders-and-files.api";

export interface GetBoxDetailsAndFolderResponse extends GetFolderResponse {
    details: GetBoxDetails;
}

export interface GetBoxDetails {
    isTurnedOn: boolean;
    name: string;
    ownerEmail: string | null;
    workspaceExternalId: string | null;
    allowDownload: boolean;
    allowUpload: boolean;
    allowList: boolean;
    allowDeleteFile: boolean;
    allowDeleteFolder: boolean;
    allowRenameFile: boolean;
    allowRenameFolder: boolean;
    allowMoveItems: boolean;
    allowCreateFolder: boolean;
}

export interface GetBoxHtmlResponse {
    headerHtml: string | null;
    footerHtml: string | null;
}

export interface BoxUpdateFileNameRequest {
    name: string;
}

export interface BoxUpdateFolderNameRequest {
    name: string;
}

export interface BoxMoveItemsToFolderRequest {
    fileExternalIds: string[];
    folderExternalIds: string[];
    fileUploadExternalIds: string[];
    destinationFolderExternalId: string | null
}

export interface BoxGetUploadListResponse {
    items: BoxUpload[];
}

export interface BoxUpload {
    externalId: string;
    fileName: string;
    fileExtension: string;
    fileContentType: string;
    fileSizeInBytes: number;
    
    folderName: string;
    folderExternalId: string;
    folderPath: string[];

    alreadyUploadedPartNumbers: number[];
}  

export interface BoxGetFileUploadDetailsResponse {
    alreadyUploadedPartNumbers: number[];
    expectedPartsCount: number;
}   

export interface BoxInitiateFilePartUploadResponse {
    uploadPreSignedUrl: string;
    startsAtByte: number;
    endsAtByte: number;
    isCompleteFilePartUploadCallbackRequired: boolean;
}

export interface BoxCompleteFilePartUploadRequest {
    eTag: string;
}

export interface BoxCompleteFileUploadResponse {
    fileExternalId: string;
}