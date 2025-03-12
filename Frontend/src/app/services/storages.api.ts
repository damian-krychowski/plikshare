import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface CreateCloudflareR2StorageRequest {
    name: string;
    accessKeyId: string;
    secretAccessKey:string;
    url: string;
    encryptionType: AppStorageEncryptionType;
}

export interface CreateCloudflareR2StorageResponse {
    externalId: string;
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
}

export interface CreateAwsS3StorageResponse {
    externalId: string;
}

export interface CreateDigitalOceanSpacesStorageRequest {
    name: string;
    accessKey: string;
    secretKey:string;
    region: string;
    encryptionType: AppStorageEncryptionType;
}

export interface UpdateDigitalOceanSpacesStorageDetailsRequest {
    accessKey: string;
    secretKey:string;
    region: string;
}

export interface CreateDigitalOceanSpacesStorageResponse {
    externalId: string;
}

export interface CreateHardDriveStorageRequest {
    name: string;
    volumePath: string;
    folderPath: string;
    encryptionType: AppStorageEncryptionType;
}

export interface CreateHardDriveStorageResponse {
    externalId: string;
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

export type AppStorageType = 'hard-drive' | 'cloudflare-r2' | 'aws-s3' | 'digitalocean-spaces';

export type AppStorageEncryptionType = 'none' | 'managed';

export type GetStorageItem = 
    GetHardDriveStorageItem 
    | GetCloudflareR2StorageItem 
    | GetAwsS3StorageItem 
    | GetDigitalOceanSpacesStorageItem;

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

export interface GetHardDriveVolumesRespone {
    items: HardDriveVolumeItem[];
}

export interface HardDriveVolumeItem {
    path: string;
    restrictedFolderPaths: string[];
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
}