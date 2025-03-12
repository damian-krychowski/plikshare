import { Component, ViewEncapsulation, signal } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AppStorageEncryptionType, StoragesApi } from '../../../../services/storages.api';
import { AppStorage } from '../../../../shared/storage-item/storage-item.component';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';
import { MatRadioModule } from '@angular/material/radio';
import { Router } from '@angular/router';

@Component({
    selector: 'app-create-cloudflare-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective,
        MatRadioModule
    ],
    templateUrl: './create-cloudflare-storage.component.html',
    styleUrl: './create-cloudflare-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateCloudflareStorageComponent{
    isLoading = signal(false);
    couldNotConnect = signal(false);

    encryption = new FormControl('none', Validators.required);
    name = new FormControl('', [Validators.required]);
    accessKeyId = new FormControl('', [Validators.required]);    
    secretAccessKey = new FormControl('', [Validators.required]);    
    url = new FormControl('', [Validators.required]);    

    formGroup: FormGroup;
    wasSubmitted = signal(false);
      

    constructor(
        private _storagesApi: StoragesApi,  
        private _router: Router) {    
            
        this.formGroup = new FormGroup({
            name: this.name,
            accessKeyId: this.accessKeyId,
            secretAccessKey: this.secretAccessKey,
            url: this.url,
            encryption: this.encryption
        });
    }

    async onCreateStorage() {
        this.wasSubmitted.set(true);

        if(!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);
            
            const encryptionType = this.encryption.value! as AppStorageEncryptionType;

            await this._storagesApi.createCloudflareR2Storage({
                name: this.name.value!,
                accessKeyId: this.accessKeyId.value!,
                secretAccessKey: this.secretAccessKey.value!,
                url: this.url.value!,
                encryptionType: encryptionType
            });

            this.goToStorages();
        } catch (err: any) {
            if(err.error.code === 'storage-url-invalid'){
                this.url.setErrors({
                    invalidUrl: true
                })
            } else if (err.error.code === 'storage-connection-failed') {
                this.couldNotConnect.set(true);
            } else if (err.error.code === 'storage-name-not-unique') {
                this.name.setErrors({
                    notUnique: true
                });
            }else {
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
