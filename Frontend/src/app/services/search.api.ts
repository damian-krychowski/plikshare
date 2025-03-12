import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { getSearchResponseDtoProtobuf } from "../protobuf/search-response-dto.protobuf";
import * as protobuf from 'protobufjs';
import { ProtoHttp } from "./protobuf-http.service";

//todo we should not pass this from frontend, its only temporary
export interface SearchRequest {
    phrase: string;
    workspaceExternalIds: string[];
    boxExternalIds: string[];
}

export interface SearchWorkspaceGroupDto {
    externalId: string;
    name: string;
    allowShare: boolean;
    isOwnedByUser: boolean;
}

export interface SearchExternalBoxGroupDto {
    externalId: string;
    name: string;
    allowDownload: boolean;
    allowUpload: boolean;
    allowList: boolean;
    allowDeleteFile: boolean;
    allowRenameFile: boolean;
    allowMoveItems: boolean;
    allowCreateFolder: boolean;
    allowDeleteFolder: boolean;
    allowRenameFolder: boolean;
}

export interface SearchWorkspaceItemDto {
    externalId: string;
    name: string;
    currentSizeInBytes: number;
    ownerEmail: string;
    ownerExternalId: string;
    isOwnedByUser: boolean;
    allowShare: boolean;
    isUsedByIntegration: boolean;
    isBucketCreated: boolean;
}

export interface SearchWorkspaceFolderItemDto {
    externalId: string;
    name: string;
    workspaceExternalId: string;
    ancestors: {
        externalId: string;
        name: string;
    }[];
};

export interface SearchWorkspaceBoxItemDto {
    externalId: string;
    name: string;
    workspaceExternalId: string;
    isEnabled: boolean;
    folderPath: {
        externalId: string;
        name: string;
    }[];
}

export interface SearchWorkspaceFileItemDto {
    externalId: string;
    name: string;
    workspaceExternalId: string;
    sizeInBytes: number;
    extension: string;
    folderPath: {
        externalId: string;
        name: string;
    }[];
}

export interface SearchExternalBoxItemDto {
    externalId: string;
    name: string;
    ownerEmail: string;
    ownerExternalId: string;
    
    allowDownload: boolean;
    allowUpload: boolean;
    allowList: boolean;
    allowDeleteFile: boolean;
    allowRenameFile: boolean;
    allowMoveItems: boolean;
    allowCreateFolder: boolean;
    allowDeleteFolder: boolean;
    allowRenameFolder: boolean;
}

export interface SearchExternalBoxFolderItemDto {
    externalId: string;
    name: string;
    boxExternalId: string;
    ancestors: {
        externalId: string;
        name: string;
    }[];
}

export interface SearchExternalBoxFileItemDto {
    externalId: string;
    name: string;
    boxExternalId: string;
    sizeInBytes: number;
    extension: string;
    folderPath: {
        externalId: string;
        name: string;
    }[];
    wasUploadedByUser: boolean;
}

export interface SearchResponseDto {
    workspaceGroups: SearchWorkspaceGroupDto[];
    externalBoxGroups: SearchExternalBoxGroupDto[];

    workspaces: SearchWorkspaceItemDto[];
    workspaceFolders: SearchWorkspaceFolderItemDto[];
    workspaceBoxes: SearchWorkspaceBoxItemDto[];
    workspaceFiles: SearchWorkspaceFileItemDto[];

    externalBoxes: SearchExternalBoxItemDto[];
    externalBoxFolders: SearchExternalBoxFolderItemDto[];
    externalBoxFiles: SearchExternalBoxFileItemDto[];
}


const searchResponseDtoProtobuf = getSearchResponseDtoProtobuf();

@Injectable({
    providedIn: 'root'
})
export class SearchApi {
    constructor(
        private _protoHttp: ProtoHttp) {        
    }

    public async search(request: SearchRequest): Promise<SearchResponseDto> {
        const result = await this._protoHttp.postJsonToProto<SearchRequest, SearchResponseDto>({
            route: `/api/search`,
            request: request,
            responseProtoType: searchResponseDtoProtobuf
        });
        
        return result;
    }
}