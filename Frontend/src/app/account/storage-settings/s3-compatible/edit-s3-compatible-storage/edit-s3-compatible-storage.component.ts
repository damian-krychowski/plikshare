import { Component, Inject, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { StoragesApi } from '../../../../services/storages.api';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';

@Component({
    selector: 'app-edit-s3-compatible-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatCheckboxModule,
        SecureInputDirective
    ],
    templateUrl: './edit-s3-compatible-storage.component.html',
    styleUrl: './edit-s3-compatible-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class EditS3CompatibleStorageComponent{
    isLoading = signal(false);
    couldNotConnect = signal(false);

    accessKey = new FormControl('', [Validators.required]);
    secretKey = new FormControl('', [Validators.required]);
    url = new FormControl('', [Validators.required]);
    region = new FormControl('');
    forcePathStyle = new FormControl(true, {nonNullable: true});

    formGroup: FormGroup;
    wasSubmitted = signal(false);


    constructor(
        private _storagesApi: StoragesApi,
        public dialogRef: MatDialogRef<EditS3CompatibleStorageComponent>,
        @Inject(MAT_DIALOG_DATA) public data: {storageExternalId: string}) {

        this.formGroup = new FormGroup({
            accessKey: this.accessKey,
            secretKey: this.secretKey,
            url: this.url,
            region: this.region,
            forcePathStyle: this.forcePathStyle
        });
    }

    async onSaveStorageDetails() {
        this.wasSubmitted.set(true);

        if(!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            await this._storagesApi.updateS3CompatibleStorageDetails(this.data.storageExternalId, {
                accessKey: this.accessKey.value!,
                secretKey: this.secretKey.value!,
                url: this.url.value!,
                region: this.region.value || null,
                forcePathStyle: this.forcePathStyle.value
            });

            this.dialogRef.close();
        } catch (err: any) {
            if(err.error.code === 'storage-url-invalid'){
                this.url.setErrors({
                    invalidUrl: true
                })
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
