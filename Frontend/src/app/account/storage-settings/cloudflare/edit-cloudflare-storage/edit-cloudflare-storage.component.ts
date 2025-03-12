import { Component, Inject, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { StoragesApi } from '../../../../services/storages.api';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';

@Component({
    selector: 'app-edit-cloudflare-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective
    ],
    templateUrl: './edit-cloudflare-storage.component.html',
    styleUrl: './edit-cloudflare-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class EditCloudflareStorageComponent{
    isLoading = signal(false);
    couldNotConnect = signal(false);
    isUrlInvalid = signal(false);

    accessKeyId = new FormControl('', [Validators.required]);    
    secretAccessKey = new FormControl('', [Validators.required]);    
    url = new FormControl('', [Validators.required]);    

    formGroup: FormGroup;
    wasSubmitted = signal(false);
      

    constructor(
        private _storagesApi: StoragesApi,
        public dialogRef: MatDialogRef<EditCloudflareStorageComponent>,
        @Inject(MAT_DIALOG_DATA) public data: {storageExternalId: string}) {    
            
        this.formGroup = new FormGroup({
            accessKeyId: this.accessKeyId,
            secretAccessKey: this.secretAccessKey,
            url: this.url
        });
    }

    async onCreateStorage() {
        this.wasSubmitted.set(true);

        if(!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            const result = await this._storagesApi.updateCloudflareR2StorageDetails(this.data.storageExternalId, {
                accessKeyId: this.accessKeyId.value!,
                secretAccessKey: this.secretAccessKey.value!,
                url: this.url.value!
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
