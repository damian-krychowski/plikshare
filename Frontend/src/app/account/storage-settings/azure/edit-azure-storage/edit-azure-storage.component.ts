import { Component, Inject, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatRadioModule } from '@angular/material/radio';
import { AzureBlobAuthType, StoragesApi } from '../../../../services/storages.api';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';

@Component({
    selector: 'app-edit-azure-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatRadioModule,
        SecureInputDirective
    ],
    templateUrl: './edit-azure-storage.component.html',
    styleUrl: './edit-azure-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class EditAzureStorageComponent {
    isLoading = signal(false);
    couldNotConnect = signal(false);

    authType = new FormControl<AzureBlobAuthType>('shared-key', Validators.required);
    serviceUrl = new FormControl('', [Validators.required]);
    accountName = new FormControl('');
    accountKey = new FormControl('');
    sasToken = new FormControl('');

    formGroup: FormGroup;
    wasSubmitted = signal(false);

    constructor(
        private _storagesApi: StoragesApi,
        public dialogRef: MatDialogRef<EditAzureStorageComponent>,
        @Inject(MAT_DIALOG_DATA) public data: { storageExternalId: string }) {

        this.formGroup = new FormGroup({
            authType: this.authType,
            serviceUrl: this.serviceUrl,
            accountName: this.accountName,
            accountKey: this.accountKey,
            sasToken: this.sasToken
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

    async onUpdateStorage() {
        this.wasSubmitted.set(true);

        if (!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            const authType = this.authType.value!;

            await this._storagesApi.updateAzureBlobStorageDetails(this.data.storageExternalId, {
                authType,
                serviceUrl: this.serviceUrl.value!,
                accountName: authType === 'shared-key' ? this.accountName.value! : undefined,
                accountKey: authType === 'shared-key' ? this.accountKey.value! : undefined,
                sasToken: authType === 'sas' ? this.sasToken.value! : undefined
            });

            this.dialogRef.close();
        } catch (err: any) {
            if (err.error.code === 'storage-url-invalid') {
                this.serviceUrl.setErrors({
                    invalidUrl: true
                });
            } else if (err.error.code === 'storage-connection-failed') {
                this.couldNotConnect.set(true);
            } else {
                console.error(err);
            }
        } finally {
            this.isLoading.set(false);
        }
    }

    cancel() {
        this.dialogRef.close();
    }
}
