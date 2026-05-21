import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { TrashPolicyDto } from "./workspaces.api";

export interface CreateCloudflareR2StorageRequest {
    name: string;
    accessKeyId: string;
    secretAccessKey:string;
    url: string;
    encryptionType: AppStorageEncryptionType;
    defaultTrashPolicy: TrashPolicyDto;
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
    defaultTrashPolicy: TrashPolicyDto;
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
    defaultTrashPolicy: TrashPolicyDto;
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
    defaultTrashPolicy: TrashPolicyDto;
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

export type AppStorageType = 'hard-drive' | 'cloudflare-r2' | 'aws-s3' | 'digital-ocean-spaces' | 'backblaze-b2' | 'azure-blob' | 'google-cloud-storage';

export type AppStorageEncryptionType = 'none' | 'managed' | 'full';

export type AzureBlobAuthType = 'shared-key' | 'sas';

export type GetStorageItem =
    GetHardDriveStorageItem
    | GetCloudflareR2StorageItem
    | GetAwsS3StorageItem
    | GetDigitalOceanSpacesStorageItem
    | GetBackblazeB2StorageItem
    | GetAzureBlobStorageItem
    | GetGoogleCloudStorageItem;

export type GetHardDriveStorageItem = {
    $type: "hard-drive",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;
    defaultTrashPolicy: TrashPolicyDto;

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
    defaultTrashPolicy: TrashPolicyDto;

    accessKeyId: string;
    url: string;
}

export type GetAwsS3StorageItem = {
    $type: "aws-s3",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;
    defaultTrashPolicy: TrashPolicyDto;

    accessKey: string;
    region: string;
}

export type GetDigitalOceanSpacesStorageItem = {
    $type: "digital-ocean-spaces",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;
    defaultTrashPolicy: TrashPolicyDto;

    accessKey: string;
    url: string;
}

export type GetBackblazeB2StorageItem = {
    $type: "backblaze-b2",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;
    defaultTrashPolicy: TrashPolicyDto;

    keyId: string;
    url: string;
}

export type GetAzureBlobStorageItem = {
    $type: "azure-blob",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;
    defaultTrashPolicy: TrashPolicyDto;

    authType: AzureBlobAuthType;
    serviceUrl: string;
    accountName: string | null;
}

export type GetGoogleCloudStorageItem = {
    $type: "google-cloud-storage",
    externalId: string;
    name: string;
    workspacesCount: number;
    encryptionType: AppStorageEncryptionType;
    defaultTrashPolicy: TrashPolicyDto;

    accessKey: string;
}

export interface GetHardDriveVolumesRespone {
    items: HardDriveVolumeItem[];
}

export interface HardDriveVolumeItem {
    path: string;
    restrictedFolderPaths: string[];
}


export interface CreateBackblazeB2StorageRequest {
    name: string;
    keyId: string;
    applicationKey: string;
    url: string;
    encryptionType: AppStorageEncryptionType;
    defaultTrashPolicy: TrashPolicyDto;
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

export interface CreateAzureBlobStorageRequest {
    name: string;
    authType: AzureBlobAuthType;
    serviceUrl: string;
    accountName?: string;
    accountKey?: string;
    sasToken?: string;
    encryptionType: AppStorageEncryptionType;
    defaultTrashPolicy: TrashPolicyDto;
}

export interface CreateAzureBlobStorageResponse {
    externalId: string;
    recoveryCode?: string;
}

export interface UpdateAzureBlobStorageDetailsRequest {
    authType: AzureBlobAuthType;
    serviceUrl: string;
    accountName?: string;
    accountKey?: string;
    sasToken?: string;
}

export interface CreateGoogleCloudStorageRequest {
    name: string;
    accessKey: string;
    secretKey: string;
    encryptionType: AppStorageEncryptionType;
    defaultTrashPolicy: TrashPolicyDto;
}

export interface CreateGoogleCloudStorageResponse {
    externalId: string;
    recoveryCode?: string;
}

export interface UpdateGoogleCloudStorageDetailsRequest {
    accessKey: string;
    secretKey: string;
}

export interface GetStorageNamesResponse {
    items: StorageNameItem[];
}

export interface StorageNameItem {
    externalId: string;
    name: string;
    encryptionType: AppStorageEncryptionType;
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
                `/api/storages/digital-ocean-spaces`, request, {
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
                `/api/storages/digital-ocean-spaces/${externalId}/details`, request, {
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

    public async updateDefaultTrashPolicy(externalId: string, request: TrashPolicyDto): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/storages/${externalId}/default-trash-policy`, request, {
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

    public async getStorageNames(): Promise<GetStorageNamesResponse> {
        const call = this
            ._http
            .get<GetStorageNamesResponse>(
                `/api/storages/names`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
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

    public async createAzureBlobStorage(request: CreateAzureBlobStorageRequest): Promise<CreateAzureBlobStorageResponse> {
        const call = this
            ._http
            .post<CreateAzureBlobStorageResponse>(
                `/api/storages/azure-blob`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updateAzureBlobStorageDetails(externalId: string, request: UpdateAzureBlobStorageDetailsRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/storages/azure-blob/${externalId}/details`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async createGoogleCloudStorage(request: CreateGoogleCloudStorageRequest): Promise<CreateGoogleCloudStorageResponse> {
        const call = this
            ._http
            .post<CreateGoogleCloudStorageResponse>(
                `/api/storages/google-cloud-storage`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updateGoogleCloudStorageDetails(externalId: string, request: UpdateGoogleCloudStorageDetailsRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/storages/google-cloud-storage/${externalId}/details`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }
}