import { Component, ViewEncapsulation, signal, computed } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AppStorageEncryptionType, StoragesApi } from '../../../../services/storages.api';
import { AppStorage } from '../../../../shared/storage-item/storage-item.component';
import { RegionInputComponent } from '../../../../shared/region-input/region-input.component';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';
import { DigitalOceanRegions } from '../../../../services/digitalocean-regions';
import { MatRadioModule } from '@angular/material/radio';
import { Router } from '@angular/router';
import { DataStore } from '../../../../services/data-store.service';
import { RecoveryCodeDialogService } from '../../../../shared/recovery-code-display/recovery-code-dialog.service';
import { AuthService } from '../../../../services/auth.service';
import { MatDialog } from '@angular/material/dialog';
import { SetupEncryptionPasswordComponent } from '../../../../shared/setup-encryption-password/setup-encryption-password.component';
import { firstValueFrom } from 'rxjs';

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
        MatRadioModule
    ],
    templateUrl: './create-digitalocean-storage.component.html',
    styleUrl: './create-digitalocean-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateDigitalOceanStorageComponent{
    isLoading = signal(false);
    couldNotConnect = signal(false);

    digitaloceanRegions = DigitalOceanRegions.Spaces();

    selectedEncryption = signal<string>('none');
    needsEncryptionSetup = computed(() =>
        this.selectedEncryption() === 'full' && !this.auth.isEncryptionConfigured());

    encryption = new FormControl('none', Validators.required);
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
        private _recoveryCodeDialog: RecoveryCodeDialogService,
        private _dialog: MatDialog,
        public auth: AuthService) {

        this.formGroup = new FormGroup({
            name: this.name,
            accessKey: this.accessKey,
            secretKey: this.secretKey,
            region: this.region,
            encryption: this.encryption
        });

        this.encryption.valueChanges.subscribe(value =>
            this.selectedEncryption.set(value ?? 'none'));
    }

    async onCreateStorage() {
        this.wasSubmitted.set(true);

        if(!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            const encryptionType = this.encryption.value! as AppStorageEncryptionType;

            const response = await this._storagesApi.createDigitalOceanSpacesStorage({
                name: this.name.value!,
                accessKey: this.accessKey.value!,
                secretKey: this.secretKey.value!,
                region: this.region.value!,
                encryptionType: encryptionType
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
    
    async openSetupEncryptionPassword() {
        const ref = this._dialog.open(SetupEncryptionPasswordComponent, {
            width: '500px',
            position: { top: '100px' },
            disableClose: true
        });
        await firstValueFrom(ref.afterClosed());
    }

    goToStorages() {
        this._router.navigate(['settings/storage']);
    }

}
