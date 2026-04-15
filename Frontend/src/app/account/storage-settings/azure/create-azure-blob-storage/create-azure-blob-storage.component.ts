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

@Component({
    selector: 'app-create-azure-blob-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective,
        MatRadioModule
    ],
    templateUrl: './create-azure-blob-storage.component.html',
    styleUrl: './create-azure-blob-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateAzureBlobStorageComponent {
    isLoading = signal(false);
    couldNotConnect = signal(false);

    authType = new FormControl<AzureBlobAuthType>('shared-key', Validators.required);
    encryption = new FormControl('none', Validators.required);
    name = new FormControl('', [Validators.required]);
    accountName = new FormControl('');
    accountKey = new FormControl('');
    serviceUrl = new FormControl('', [Validators.required]);
    sasToken = new FormControl('');
    managedIdentityClientId = new FormControl('');

    formGroup: FormGroup;
    wasSubmitted = signal(false);

    constructor(
        private _dataStore: DataStore,
        private _storagesApi: StoragesApi,
        private _router: Router) {

        this.formGroup = new FormGroup({
            name: this.name,
            authType: this.authType,
            accountName: this.accountName,
            accountKey: this.accountKey,
            serviceUrl: this.serviceUrl,
            sasToken: this.sasToken,
            managedIdentityClientId: this.managedIdentityClientId,
            encryption: this.encryption
        });

        this.authType.valueChanges.subscribe(() => this.applyAuthTypeValidators());
        this.applyAuthTypeValidators();
    }

    private applyAuthTypeValidators() {
        const authType = this.authType.value;

        this.accountName.clearValidators();
        this.accountKey.clearValidators();
        this.sasToken.clearValidators();

        if (authType === 'shared-key') {
            this.accountName.setValidators([Validators.required]);
            this.accountKey.setValidators([Validators.required]);
        }

        if (authType === 'sas') {
            this.sasToken.setValidators([Validators.required]);
        }

        this.accountName.updateValueAndValidity({ emitEvent: false });
        this.accountKey.updateValueAndValidity({ emitEvent: false });
        this.sasToken.updateValueAndValidity({ emitEvent: false });
    }

    async onCreateStorage() {
        this.wasSubmitted.set(true);

        if (!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            const encryptionType = this.encryption.value! as AppStorageEncryptionType;

            await this._storagesApi.createAzureBlobStorage({
                name: this.name.value!,
                authType: this.authType.value!,
                accountName: this.accountName.value ?? '',
                accountKey: this.accountKey.value ?? '',
                serviceUrl: this.serviceUrl.value!,
                sasToken: this.sasToken.value ?? undefined,
                managedIdentityClientId: this.managedIdentityClientId.value ?? undefined,
                encryptionType
            });

            this._dataStore.clearDashboardData();
            this.goToStorages();
        } catch (err: any) {
            if (err.error.code === 'storage-url-invalid' || err.error.code === 'storage-invalid-url') {
                this.serviceUrl.setErrors({
                    invalidUrl: true
                });
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
