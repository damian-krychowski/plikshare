import { Component, Inject, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { StoragesApi } from '../../../../services/storages.api';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';

@Component({
    selector: 'app-edit-backblaze-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective
    ],
    templateUrl: './edit-backblaze-storage.component.html',
    styleUrl: './edit-backblaze-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class EditBackblazeStorageComponent {
    isLoading = signal(false);
    couldNotConnect = signal(false);
    isUrlInvalid = signal(false);

    keyId = new FormControl('', [Validators.required]);    
    applicationKey = new FormControl('', [Validators.required]);    
    endpointUrl = new FormControl('', [Validators.required]);    

    formGroup: FormGroup;
    wasSubmitted = signal(false);
      
    constructor(
        private _storagesApi: StoragesApi,
        public dialogRef: MatDialogRef<EditBackblazeStorageComponent>,
        @Inject(MAT_DIALOG_DATA) public data: {storageExternalId: string}) {    
            
        this.formGroup = new FormGroup({
            keyId: this.keyId,
            applicationKey: this.applicationKey,
            endpointUrl: this.endpointUrl
        });
    }

    async onUpdateStorage() {
        this.wasSubmitted.set(true);

        if(!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            await this._storagesApi.updateBackblazeB2StorageDetails(this.data.storageExternalId, {
                keyId: this.keyId.value!,
                applicationKey: this.applicationKey.value!,
                url: this.endpointUrl.value!
            });

            this.dialogRef.close();
        } catch (err: any) {
            if(err.error.code === 'storage-url-invalid'){
                this.endpointUrl.setErrors({
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