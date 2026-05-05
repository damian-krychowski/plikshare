import { Component, Inject, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { StoragesApi } from '../../../../services/storages.api';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';

@Component({
    selector: 'app-edit-google-cloud-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective
    ],
    templateUrl: './edit-google-cloud-storage.component.html',
    styleUrl: './edit-google-cloud-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class EditGoogleCloudStorageComponent {
    isLoading = signal(false);
    couldNotConnect = signal(false);

    accessKey = new FormControl('', [Validators.required]);
    secretKey = new FormControl('', [Validators.required]);

    formGroup: FormGroup;
    wasSubmitted = signal(false);

    constructor(
        private _storagesApi: StoragesApi,
        public dialogRef: MatDialogRef<EditGoogleCloudStorageComponent>,
        @Inject(MAT_DIALOG_DATA) public data: { storageExternalId: string }) {

        this.formGroup = new FormGroup({
            accessKey: this.accessKey,
            secretKey: this.secretKey
        });
    }

    async onUpdateStorage() {
        this.wasSubmitted.set(true);

        if (!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            await this._storagesApi.updateGoogleCloudStorageDetails(this.data.storageExternalId, {
                accessKey: this.accessKey.value!,
                secretKey: this.secretKey.value!
            });

            this.dialogRef.close();
        } catch (err: any) {
            if (err.error.code === 'storage-connection-failed') {
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
