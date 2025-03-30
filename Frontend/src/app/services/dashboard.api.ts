import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { BoxPermissions } from "./boxes.api";
import { AppStorageEncryptionType, AppStorageType } from "./storages.api";
import * as protobuf from 'protobufjs';
import { getDashboardContentDtoProtobuf } from "../protobuf/dashboard-content-dto.protobuf";

export interface GetDashboardDataResponse {
    storages: {
        externalId: string;
        name: string;
        type: AppStorageType;
        workspacesCount: number | null;
        encryptionType: AppStorageEncryptionType;
    }[];

    workspaces: {
        externalId: string;
        name: string;
        currentSizeInBytes: number;
        maxSizeInBytes: number | null;
        owner: {
            externalId: string;
            email: string;
        };
        storageName: string | null;
        permissions: {
            allowShare: boolean;
        };
        isUsedByIntegration: boolean;
        isBucketCreated: boolean;
    }[];

    otherWorkspaces: {
        externalId: string;
        name: string;
        currentSizeInBytes: number;
        maxSizeInBytes: number | null;
        owner: {
            externalId: string;
            email: string;
        };
        storageName: string | null;
        permissions: {
            allowShare: boolean;
        };
        isUsedByIntegration: boolean;
        isBucketCreated: boolean;
    }[];

    workspaceInvitations: {
        workspaceExternalId: string;
        workspaceName: string;
        owner: {
            externalId: string;
            email: string;
        };
        inviter: {
            externalId: string;
            email: string;
        };
        permissions: {
            allowShare: boolean;
        };
        storageName: string | null;
        isUsedByIntegration: boolean;
        isBucketCreated: boolean;
    }[];

    boxes: {
        boxExternalId: string;
        boxName: string;
        owner: {
            externalId: string;
            email: string;
        };
        permissions: BoxPermissions
    }[];

    boxInvitations: {
        boxExternalId: string;
        boxName: string;
        owner: {
            externalId: string;
            email: string;
        };
        inviter: {
            externalId: string;
            email: string;
        };
        permissions: BoxPermissions;
    }[];
}

export type AcceptWorkspaceInvitationResponse = {
    workspaceCurrentSizeInBytes: number;
};

const dashboardContentProtobuf = getDashboardContentDtoProtobuf();

@Injectable({
    providedIn: 'root'
})
export class DashboardApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async getDashboardData(): Promise<GetDashboardDataResponse> {
        const call = this._http.get(`/api/dashboard`, {
            responseType: 'arraybuffer',
            headers: new HttpHeaders({
                'Accept': 'application/x-protobuf'
            })
        });
    
        const compressedData: ArrayBuffer = await firstValueFrom(call);        
        const reader = protobuf.Reader.create(new Uint8Array(compressedData));
        const result: GetDashboardDataResponse = dashboardContentProtobuf.decode(reader) as any;
    
        return result;
    }
}