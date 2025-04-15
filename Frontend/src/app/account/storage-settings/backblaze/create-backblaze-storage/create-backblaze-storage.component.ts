import { Component, ViewEncapsulation, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AppStorageEncryptionType, StoragesApi } from '../../../../services/storages.api';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';
import { MatRadioModule } from '@angular/material/radio';
import { Router } from '@angular/router';
import { DataStore } from '../../../../services/data-store.service';

@Component({
    selector: 'app-create-backblaze-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective,
        MatRadioModule
    ],
    templateUrl: './create-backblaze-storage.component.html',
    styleUrl: './create-backblaze-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateBackblazeStorageComponent {
    isLoading = signal(false);
    couldNotConnect = signal(false);

    encryption = new FormControl('none', Validators.required);
    name = new FormControl('', [Validators.required]);
    keyId = new FormControl('', [Validators.required]);    
    applicationKey = new FormControl('', [Validators.required]);    
    endpointUrl = new FormControl('', [Validators.required]);

    formGroup: FormGroup;
    wasSubmitted = signal(false);
      
    constructor(
        private _dataStore: DataStore,
        private _storagesApi: StoragesApi,  
        private _router: Router) {    
            
        this.formGroup = new FormGroup({
            name: this.name,
            keyId: this.keyId,
            applicationKey: this.applicationKey,
            endpointUrl: this.endpointUrl,
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

            await this._storagesApi.createBackblazeB2Storage({
                name: this.name.value!,
                keyId: this.keyId.value!,
                applicationKey: this.applicationKey.value!,
                url: this.endpointUrl.value!,
                encryptionType: encryptionType
            });

            this._dataStore.clearDashboardData();
            this.goToStorages();
        } catch (err: any) {
            if(err.error.code === 'storage-url-invalid'){
                this.endpointUrl.setErrors({
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