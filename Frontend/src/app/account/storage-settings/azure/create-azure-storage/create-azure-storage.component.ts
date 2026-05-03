import { Component, ViewEncapsulation, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AppStorageEncryptionType, AzureBlobAuthType, StoragesApi } from '../../../../services/storages.api';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';
import { MatRadioModule } from '@angular/material/radio';
import { Router } from '@angular/router';
import { DataStore } from '../../../../services/data-store.service';
import { RecoveryCodeDialogService } from '../../../../shared/recovery-code-display/recovery-code-dialog.service';
import { EncryptionTypeSelectorComponent } from '../../../../shared/encryption-type-selector/encryption-type-selector.component';

@Component({
    selector: 'app-create-azure-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective,
        MatRadioModule,
        EncryptionTypeSelectorComponent
    ],
    templateUrl: './create-azure-storage.component.html',
    styleUrl: './create-azure-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateAzureStorageComponent {
    isLoading = signal(false);
    couldNotConnect = signal(false);

    name = new FormControl('', [Validators.required]);
    authType = new FormControl<AzureBlobAuthType>('shared-key', Validators.required);
    serviceUrl = new FormControl('', [Validators.required]);
    accountName = new FormControl('');
    accountKey = new FormControl('');
    sasToken = new FormControl('');
    encryption = new FormControl<AppStorageEncryptionType>('none', Validators.required);

    formGroup: FormGroup;
    wasSubmitted = signal(false);

    constructor(
        private _dataStore: DataStore,
        private _storagesApi: StoragesApi,
        private _router: Router,
        private _recoveryCodeDialog: RecoveryCodeDialogService) {

        this.formGroup = new FormGroup({
            name: this.name,
            authType: this.authType,
            serviceUrl: this.serviceUrl,
            accountName: this.accountName,
            accountKey: this.accountKey,
            sasToken: this.sasToken,
            encryption: this.encryption
        });

        this.authType.valueChanges.subscribe(() => this.applyAuthTypeValidators());
        this.applyAuthTypeValidators();
    }

    private applyAuthTypeValidators() {
        this.accountName.clearValidators();
        this.accountKey.clearValidators();
        this.sasToken.clearValidators();

        if (this.authType.value === 'shared-key') {
            this.accountName.setValidators([Validators.required]);
            this.accountKey.setValidators([Validators.required]);
        } else if (this.authType.value === 'sas') {
            this.sasToken.setValidators([Validators.required]);
        }

        this.accountName.updateValueAndValidity({ emitEvent: false });
        this.accountKey.updateValueAndValidity({ emitEvent: false });
        this.sasToken.updateValueAndValidity({ emitEvent: false });
    }

    async onCreateStorage() {
        this.wasSubmitted.set(true);

        if(!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            const authType = this.authType.value!;

            const response = await this._storagesApi.createAzureBlobStorage({
                name: this.name.value!,
                authType,
                serviceUrl: this.serviceUrl.value!,
                accountName: authType === 'shared-key' ? this.accountName.value! : undefined,
                accountKey: authType === 'shared-key' ? this.accountKey.value! : undefined,
                sasToken: authType === 'sas' ? this.sasToken.value! : undefined,
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
            if(err.error.code === 'storage-url-invalid'){
                this.serviceUrl.setErrors({
                    invalidUrl: true
                })
            } else if (err.error.code === 'storage-connection-failed') {
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
