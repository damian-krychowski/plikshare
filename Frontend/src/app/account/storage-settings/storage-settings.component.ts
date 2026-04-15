import { Router } from "@angular/router";
import { Component, OnInit, signal, WritableSignal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { AuthService } from "../../services/auth.service";
import { AppStorage, StorageItemComponent } from "../../shared/storage-item/storage-item.component";
import { MatDialog } from "@angular/material/dialog";
import { EditAwsStorageComponent } from "./aws/edit-aws-storage/edit-aws-storage.component";
import { EditCloudflareStorageComponent } from "./cloudflare/edit-cloudflare-storage/edit-cloudflare-storage.component";
import { DataStore } from "../../services/data-store.service";
import { pushItems, removeItems } from "../../shared/signal-utils";
import { ItemButtonComponent } from "../../shared/buttons/item-btn/item-btn.component";
import { ActionButtonComponent } from "../../shared/buttons/action-btn/action-btn.component";
import { EditDigitalOceanStorageComponent } from "./digitalocean/edit-digitalocean-storage/edit-digitalocean-storage.component";
import { EditBackblazeStorageComponent } from "./backblaze/edit-backblaze-storage/edit-backblaze-storage.component";
import { EditAzureBlobStorageComponent } from "./azure/edit-azure-blob-storage/edit-azure-blob-storage.component";
import { GetAzureBlobStorageItem, GetStorageItem } from "../../services/storages.api";

@Component({
    selector: 'app-storage-settings',
    imports: [
        MatButtonModule,
        MatTooltipModule,
        StorageItemComponent,
        ItemButtonComponent,
        ActionButtonComponent
    ],
    templateUrl: './storage-settings.component.html',
    styleUrl: './storage-settings.component.scss'
})
export class StorageSettingsComponent implements OnInit {       
    isLoading = signal(false);

    storages: WritableSignal<AppStorage[]> = signal([]);
    private _storageItemsById = new Map<string, GetStorageItem>();

    private async refreshStoragesAfterEdit() {
        this.isLoading.set(true);

        try {
            await this.loadStorages();
            this._dataStore.clearDashboardData();
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    constructor(
        public auth: AuthService,
        private _dialog: MatDialog,
        private _dataStore: DataStore,
        private _router: Router
    ) {}

    async ngOnInit(): Promise<void> {
        this.isLoading.set(true);

        try {
            await this.loadStorages();
        } catch (error) {
            console.error(error);    
        } finally {
            this.isLoading.set(false);
        }
    }

    private async loadStorages() {
        const result = await this._dataStore.getStorages();

        this._storageItemsById = new Map(
            result.items.map(item => [item.externalId, item]));

        this.storages.set(result.items.map(s => {
            const details = this.getStorageDetails(s);

            const storage: AppStorage = {
                externalId: s.externalId,
                name: signal(s.name),
                type: s.$type,
                details: details,
                encryptionType: s.encryptionType,
                workspacesCount: s.workspacesCount,
                isNameEditing: signal(false),
                isHighlighted: signal(false)
            };

            return storage;
        }));
    }

    private getStorageDetails(item: GetStorageItem) {
        if(item.$type == 'hard-drive')
            return `Path: ${item.fullPath}`;

        if(item.$type == 'aws-s3')
            return `AccessKey: ${item.accessKey} <br> Region: ${item.region}`;

        if(item.$type == 'cloudflare-r2')
            return `AccessKeyId: ${item.accessKeyId} <br> Url: ${item.url}`

        if(item.$type == 'digitalocean-spaces')
            return `AccessKey: ${item.accessKey} <br> Url: ${item.url}`;

        if(item.$type == 'backblaze-b2')            
            return `KeyId: ${item.keyId} <br> Url: ${item.url}`;

        if(item.$type == 'azure-blob')
            return `AccountName: ${item.accountName} <br> Url: ${item.serviceUrl}`;

        throw new Error("Uknown storage type " + (item as any).$type);
    }

    goToAccount() {
        this._router.navigate(['account']);
    }


    onAddCoudlfareR2Storage() {
        this._router.navigate(['settings/storage/add/cloudflare-r2']);     
    }

    onAddAwsS3Storage() {
        this._router.navigate(['settings/storage/add/aws-s3']);   
    }
    
    onAddDigitalOceanSpacesStorage() {
        this._router.navigate(['settings/storage/add/digital-ocean-spaces']);   
    }

    onAddBackblazeB2Storage() {
        this._router.navigate(['settings/storage/add/backblaze-b2']);   
    }

    onAddAzureBlobStorage() {
        this._router.navigate(['settings/storage/add/azure-blob']);
    }

    onStorageEdit(storage: AppStorage) {     
        if(storage.type == 'cloudflare-r2'){               
            this._dialog.open(EditCloudflareStorageComponent, {
                width: '500px',
                data: {
                    storageExternalId: storage.externalId
                },
                position: {
                    top: '100px'
                },
                disableClose: true
            }).afterClosed().subscribe(async isEdited => {
                if (isEdited)
                    await this.refreshStoragesAfterEdit();
            });
        } else if(storage.type == 'aws-s3') {            
            this._dialog.open(EditAwsStorageComponent, {
                width: '500px',
                data: {
                    storageExternalId: storage.externalId
                },
                position: {
                    top: '100px'
                },
                disableClose: true
            }).afterClosed().subscribe(async isEdited => {
                if (isEdited)
                    await this.refreshStoragesAfterEdit();
            });
        } else if(storage.type == 'digitalocean-spaces') {            
            this._dialog.open(EditDigitalOceanStorageComponent, {
                width: '500px',
                data: {
                    storageExternalId: storage.externalId
                },
                position: {
                    top: '100px'
                },
                disableClose: true
            }).afterClosed().subscribe(async isEdited => {
                if (isEdited)
                    await this.refreshStoragesAfterEdit();
            });
        } else if(storage.type == 'backblaze-b2') {
            this._dialog.open(EditBackblazeStorageComponent, {
                width: '500px',
                data: {
                    storageExternalId: storage.externalId
                },
                position: {
                    top: '100px'
                },
                disableClose: true
            }).afterClosed().subscribe(async isEdited => {
                if (isEdited)
                    await this.refreshStoragesAfterEdit();
            });
        } else if(storage.type == 'azure-blob') {
            const azureDetails = this._storageItemsById.get(storage.externalId) as GetAzureBlobStorageItem | undefined;

            this._dialog.open(EditAzureBlobStorageComponent, {
                width: '500px',
                data: {
                    storageExternalId: storage.externalId,
                    authType: azureDetails?.authType ?? 'shared-key',
                    accountName: azureDetails?.accountName ?? '',
                    serviceUrl: azureDetails?.serviceUrl ?? '',
                    sasToken: azureDetails?.sasToken ?? '',
                    managedIdentityClientId: azureDetails?.managedIdentityClientId ?? '',
                    encryptionType: azureDetails?.encryptionType ?? 'none'
                },
                position: {
                    top: '100px'
                },
                disableClose: true
            }).afterClosed().subscribe(async isEdited => {
                if (isEdited)
                    await this.refreshStoragesAfterEdit();
            });
        }
    }

    onAddHardDriveStorage() {
        this._router.navigate(['settings/storage/add/hard-drive']);        
    }

    private addStorage(newStorage: AppStorage) {
        pushItems(this.storages, newStorage);
        this._dataStore.clearDashboardData();
    }

    onStorageDelete(storage: AppStorage) {
        removeItems(this.storages, storage);
        this._dataStore.clearDashboardData();
    }
}
