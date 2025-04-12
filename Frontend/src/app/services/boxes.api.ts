import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { DataStore } from "./data-store.service";
import { FileDto, SubfolderDto } from "./folders-and-files.api";

export interface GetBoxListResponse {
    items: BoxListItemDto[];
}

export interface BoxListItemDto {
    externalId: string;
    name: string;
    isEnabled: boolean;
    folderPath: {
        name: string;
        externalId: string;
    }[];
}

export interface BoxPermissions {
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

export interface GetBoxResponse {
    details: {
        externalId: string;
        name: string;
        isEnabled: boolean;
        header: {
            isEnabled: boolean;
            json: string;
        };
        footer: {
            isEnabled: boolean;
            json: string;
        };
        folderPath: {
            name: string;
            externalId: string;
        }[];
    };
    
    links: BoxLink[];
    members: BoxMember[];    

    subfolders: SubfolderDto[];
    files: FileDto[];
}

export interface CreateBoxRequest {
    folderExternalId: string;
    name: string;
}

export interface CreateBoxResponse {
    externalId: string;
}

export interface UpdateBoxNameRequest {
    name: string;
}

export interface UpdateBoxFolderRequest {
    folderExternalId: string;
}

export interface UpdateBoxIsEnabled {
    isEnabled: boolean;
}

export interface GetBoxLinksResponse {
    items: BoxLink[];
}

export interface BoxLink {
    externalId: string;
    name: string;
    isEnabled: boolean;
    accessCode: string;
    permissions: BoxPermissions;   
    widgetOrigins: string[]; 
}

export interface CreateBoxMemberInvitationRequest {
    memberEmails: string[];
}

export interface CreateBoxMemberInvitationResponse {
    members: {
        email: string;
        externalId: string;
    }[];
}

export interface BoxMember {
    memberExternalId: string;
    memberEmail: string;
    inviterEmail: string;
    wasInvitationAccepted: boolean;
    permissions: BoxPermissions;
}

export interface UpdateBoxCustomSectionRequest {
    json: string;
    html: string;
}

export interface UpdateBoxCustomSectionIsEnabledRequest {
    isEnabled: boolean;
}

export interface CreateBoxLinkRequest {
    name: string;
}

export interface CreateBoxLinkResponse {
    externalId: string;
    accessCode: string;
}

@Injectable({
    providedIn: 'root'
})
export class BoxesSetApi {
    constructor(
        private _http: HttpClient,
        private _dataStore: DataStore) {        
    }

    public async updateBoxFooter(workspaceExternalId: string, boxExternalId: string, request: UpdateBoxCustomSectionRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/footer`,
                request);

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));        
    }

    public async updateBoxFooterIsEnabled(workspaceExternalId: string, boxExternalId: string, request: UpdateBoxCustomSectionIsEnabledRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/footer/is-enabled`,
                request);

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));
    }

    public async updateBoxHeader(workspaceExternalId: string, boxExternalId: string, request: UpdateBoxCustomSectionRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/header`,
                request);

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));
    }

    public async updateBoxHeaderIsEnabled(workspaceExternalId: string, boxExternalId: string, request: UpdateBoxCustomSectionIsEnabledRequest): Promise<void> {
        const call = this
            ._http
            .patch<void>(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/header/is-enabled`,
                request);

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));
    }

    public async updateBoxMemberPermissions(
        workspaceExternalId: string, 
        boxExternalId: string, 
        memberExternalId: string, 
        request: BoxPermissions): Promise<void> {
        const call = this
            ._http
            .patch<void>(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/members/${memberExternalId}/permissions`,
                request);

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));
    }
  
    public async revokeMember(workspaceExternalId: string, boxExternalId: string, memberExternalId: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/members/${memberExternalId}`);

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));
    }

    public async createMemberInvitation(workspaceExternalId: string, boxExternalId: string, request: CreateBoxMemberInvitationRequest): Promise<CreateBoxMemberInvitationResponse> {
        const call = this
            ._http
            .post<CreateBoxMemberInvitationResponse>(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/members`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        const result = await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));

        return result;
    }

    public async createBox(workspaceExternalId: string, request: CreateBoxRequest): Promise<CreateBoxResponse> {
        const call = this
            ._http
            .post<CreateBoxResponse>(
                `/api/workspaces/${workspaceExternalId}/boxes`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        const result = await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxesKey(workspaceExternalId));

        return result;
    }

    public async updateBoxName(workspaceExternalId: string, boxExternalId: string, request: UpdateBoxNameRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/name`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxesKey(workspaceExternalId));
    }
    
    public async updateBoxFolder(workspaceExternalId: string, boxExternalId: string, request: UpdateBoxFolderRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/folder`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxesKey(workspaceExternalId));
    }

    public async updateBoxIsEnabled(workspaceExternalId: string, boxExternalId: string, request: UpdateBoxIsEnabled): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/is-enabled`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxesKey(workspaceExternalId));
    }

    public async deleteBox(workspaceExternalId: string, boxExternalId: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxesKey(workspaceExternalId));
    }

    public async createBoxLink(workspaceExternalId: string, boxExternalId: string, request: CreateBoxLinkRequest): Promise<CreateBoxLinkResponse> {
        const call = this
            ._http
            .post<CreateBoxLinkResponse>(
                `/api/workspaces/${workspaceExternalId}/boxes/${boxExternalId}/box-links`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        const result = await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key == this._dataStore.boxKey(workspaceExternalId, boxExternalId));

        return result;
    }
}

@Injectable({
    providedIn: 'root'
})
export class BoxesGetApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async getBox(workspaceExternalId: string, externalId: string): Promise<GetBoxResponse> {
        const call = this
            ._http
            .get<GetBoxResponse>(
                `/api/workspaces/${workspaceExternalId}/boxes/${externalId}`);

        return await firstValueFrom(call);
    }

    public async getBoxes(workspaceExternalId: string): Promise<GetBoxListResponse> {
        const call = this
            ._http
            .get<GetBoxListResponse>(
                `/api/workspaces/${workspaceExternalId}/boxes`);

        return await firstValueFrom(call);
    }
}