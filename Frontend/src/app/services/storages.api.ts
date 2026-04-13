import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface CreateCloudflareR2StorageRequest {
    name: string;
    accessKeyId: string;
    secretAccessKey:string;
    url: string;
    encryptionType: AppStorageEncryptionType;
    masterPassword?: string;
}

export interface CreateCloudflareR2StorageResponse {
    externalId: string;
    recoveryCode?: string;
}

export interface UpdateCloudflareR2StorageDetailsRequest {
    accessKeyId: string;
    secretAccessKey:string;
    url: string;
}

export interface CreateAwsS3StorageRequest {
    name: string;
    accessKey: string;
    secretAccessKey:string;
    region: string;
    encryptionType: AppStorageEncryptionType;
    masterPassword?: string;
}

export interface CreateAwsS3StorageResponse {
    externalId: string;
    recoveryCode?: string;
}

export interface CreateDigitalOceanSpacesStorageRequest {
    name: string;
    accessKey: string;
    secretKey:string;
    region: string;
    encryptionType: AppStorageEncryptionType;
    masterPassword?: string;
}

export interface UpdateDigitalOceanSpacesStorageDetailsRequest {
    accessKey: string;
    secretKey:string;
    region: string;
}

export interface CreateDigitalOceanSpacesStorageResponse {
    externalId: string;
    recoveryCode?: string;
}

export interface CreateHardDriveStorageRequest {
    name: string;
    volumePath: string;
    folderPath: string;
    encryptionType: AppStorageEncryptionType;
    masterPassword?: string;
}

export interface CreateHardDriveStorageResponse {
    externalId: string;
    recoveryCode?: string;
}

export interface UpdateAwsS3StorageDetailsRequest {
    accessKey: string;
    secretAccessKey:string;
    region: string;
}

export interface UpdateStorageNameRequest {
    name: string;
}

export interface GetStoragesResponse {
    items: GetStorageItem[];
}

export type AppStorageType = 'hard-drive' | 'cloudflare-r2' | 'aws-s3' | 'digitalocean-spaces' | 'backblaze-b2';

export type AppStorageEncryptionType = 'none' | 'managed' | 'full';

export type GetStorageItem = 
    GetHardDriveStorageItem 
    | GetCloudflareR2StorageItem 
    | GetAwsS3StorageItem 
    | GetDigitalOceanSpacesStorageItem
    | GetBackblazeB2StorageItem;

export type GetHardDriveStorageItem = {
    $type: "hard-drive",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;

    volumePath: string;
    folderPath: string;
    fullPath: string;
}

export type GetCloudflareR2StorageItem = {
    $type: "cloudflare-r2",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;

    accessKeyId: string;
    url: string;
}

export type GetAwsS3StorageItem = {
    $type: "aws-s3",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;

    accessKey: string;
    region: string;
}

export type GetDigitalOceanSpacesStorageItem = {
    $type: "digitalocean-spaces",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;

    accessKey: string;
    url: string;
}

export type GetBackblazeB2StorageItem = {
    $type: "backblaze-b2",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;

    keyId: string;
    url: string;
}

export interface GetHardDriveVolumesRespone {
    items: HardDriveVolumeItem[];
}

export interface HardDriveVolumeItem {
    path: string;
    restrictedFolderPaths: string[];
}

export interface UnlockFullEncryptionRequest {
    masterPassword: string;
}

export interface ResetMasterPasswordRequest {
    recoveryCode: string;
    newPassword: string;
}

export interface ChangeMasterPasswordRequest {
    oldPassword: string;
    newPassword: string;
}

export interface CreateBackblazeB2StorageRequest {
    name: string;
    keyId: string;
    applicationKey: string;
    url: string;
    encryptionType: AppStorageEncryptionType;
    masterPassword?: string;
}

export interface CreateBackblazeB2StorageResponse {
    externalId: string;
    recoveryCode?: string;
}

export interface UpdateBackblazeB2StorageDetailsRequest {
    keyId: string;
    applicationKey: string;
    url: string;
}

@Injectable({
    providedIn: 'root'
})
export class StoragesApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async getHardDriveVolumes(): Promise<GetHardDriveVolumesRespone> {
        const call = this
            ._http
            .get<GetHardDriveVolumesRespone>(
                `/api/storages/hard-drive/volumes`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async createHardDriveStorage(request: CreateHardDriveStorageRequest): Promise<CreateHardDriveStorageResponse> {
        const call = this
            ._http
            .post<CreateHardDriveStorageResponse>(
                `/api/storages/hard-drive`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async createAwsS3Storage(request: CreateAwsS3StorageRequest): Promise<CreateAwsS3StorageResponse> {
        const call = this
            ._http
            .post<CreateAwsS3StorageResponse>(
                `/api/storages/aws-s3`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updateAwsS3StorageDetails(externalId: string, request: UpdateAwsS3StorageDetailsRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/storages/aws-s3/${externalId}/details`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async createDigitalOceanSpacesStorage(request: CreateDigitalOceanSpacesStorageRequest): Promise<CreateDigitalOceanSpacesStorageResponse> {
        const call = this
            ._http
            .post<CreateDigitalOceanSpacesStorageResponse>(
                `/api/storages/digitalocean-spaces`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updateDigitalOceanSpacesStorageDetails(externalId: string, request: UpdateDigitalOceanSpacesStorageDetailsRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/storages/digitalocean-spaces/${externalId}/details`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async createCloudflareR2Storage(request: CreateCloudflareR2StorageRequest): Promise<CreateCloudflareR2StorageResponse> {
        const call = this
            ._http
            .post<CreateCloudflareR2StorageResponse>(
                `/api/storages/cloudflare-r2`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updateCloudflareR2StorageDetails(externalId: string, request: UpdateCloudflareR2StorageDetailsRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/storages/cloudflare-r2/${externalId}/details`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateName(externalId: string, request: UpdateStorageNameRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/storages/${externalId}/name`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async deleteStorage(externalId: string): Promise<void> {
        const call = this
            ._http
            .delete(
                `/api/storages/${externalId}`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async getStorages(): Promise<GetStoragesResponse>{
        const call = this
            ._http
            .get<GetStoragesResponse>(
                `/api/storages`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async createBackblazeB2Storage(request: CreateBackblazeB2StorageRequest): Promise<CreateBackblazeB2StorageResponse> {
        const call = this
            ._http
            .post<CreateBackblazeB2StorageResponse>(
                `/api/storages/backblaze-b2`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async unlockFullEncryption(externalId: string, request: UnlockFullEncryptionRequest): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/storages/${externalId}/unlock-full-encryption`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async resetMasterPassword(externalId: string, request: ResetMasterPasswordRequest): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/storages/${externalId}/reset-master-password`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async changeMasterPassword(externalId: string, request: ChangeMasterPasswordRequest): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/storages/${externalId}/change-master-password`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateBackblazeB2StorageDetails(externalId: string, request: UpdateBackblazeB2StorageDetailsRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/storages/backblaze-b2/${externalId}/details`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }
}