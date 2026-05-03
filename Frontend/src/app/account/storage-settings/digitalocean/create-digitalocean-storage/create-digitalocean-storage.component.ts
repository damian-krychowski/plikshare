import { Component, ViewEncapsulation, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AppStorageEncryptionType, StoragesApi } from '../../../../services/storages.api';
import { RegionInputComponent } from '../../../../shared/region-input/region-input.component';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';
import { DigitalOceanRegions } from '../../../../services/digitalocean-regions';
import { Router } from '@angular/router';
import { DataStore } from '../../../../services/data-store.service';
import { RecoveryCodeDialogService } from '../../../../shared/recovery-code-display/recovery-code-dialog.service';
import { EncryptionTypeSelectorComponent } from '../../../../shared/encryption-type-selector/encryption-type-selector.component';

@Component({
    selector: 'app-create-digitalocean-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        RegionInputComponent,
        SecureInputDirective,
        EncryptionTypeSelectorComponent
    ],
    templateUrl: './create-digitalocean-storage.component.html',
    styleUrl: './create-digitalocean-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateDigitalOceanStorageComponent{
    isLoading = signal(false);
    couldNotConnect = signal(false);

    digitaloceanRegions = DigitalOceanRegions.Spaces();

    encryption = new FormControl<AppStorageEncryptionType>('none', Validators.required);
    name = new FormControl('', [Validators.required]);
    accessKey = new FormControl('', [Validators.required]);
    secretKey = new FormControl('', [Validators.required]);
    region = new FormControl('', [Validators.required]);
    formGroup: FormGroup;
    wasSubmitted = signal(false);


    constructor(
        private _dataStore: DataStore,
        private _storagesApi: StoragesApi,
        private _router: Router,
        private _recoveryCodeDialog: RecoveryCodeDialogService) {

        this.formGroup = new FormGroup({
            name: this.name,
            accessKey: this.accessKey,
            secretKey: this.secretKey,
            region: this.region,
            encryption: this.encryption
        });
    }

    async onCreateStorage() {
        this.wasSubmitted.set(true);

        if(!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            const response = await this._storagesApi.createDigitalOceanSpacesStorage({
                name: this.name.value!,
                accessKey: this.accessKey.value!,
                secretKey: this.secretKey.value!,
                region: this.region.value!,
                encryptionType: this.encryption.value!
            });

            this._dataStore.clearDashboardData();

            if (response.recoveryCode) {
                const storageName = this.name.value!;
                await this._recoveryCodeDialog.show({
                    recoveryCode: response.recoveryCode,
                    title: 'Save your storage recovery code',
                    warning: `If the database for "${storageName}" is ever lost or damaged, this code is the only way to recover the storage encryption key and decrypt your files.`,
                    dangerNotice: `Anyone who obtains this code can decrypt files on this storage. Store it somewhere only you can reach — password manager, offline note, safe. If you lose this code and the database is damaged, your files cannot be recovered.`,
                    fileHeader: `PlikShare storage recovery code\nStorage: ${storageName}`,
                    fileWarning: 'If the database is ever lost or damaged, this code is the ONLY way to recover the storage encryption key. It will not be shown again. Guard it like a password.',
                    fileName: `plikshare-recovery-${storageName.replace(/[^a-zA-Z0-9-_]/g, '_')}.txt`
                });
            }

            this.goToStorages();
        } catch (err: any) {
            if (err.error.code === 'storage-connection-failed') {
                this.couldNotConnect.set(true);
            } else if (err.error.code === 'storage-name-not-unique') {
                this.name.setErrors({
                    notUnique: true
                });
            } else {
                console.error(err);
            }
        } finally {
            this.isLoading.set(false);
        }
    }

    goToStorages() {
        this._router.navigate(['settings/storage']);
    }

}
