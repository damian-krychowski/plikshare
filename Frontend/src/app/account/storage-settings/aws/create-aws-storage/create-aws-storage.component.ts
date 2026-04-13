import { Component, ViewEncapsulation, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AppStorageEncryptionType, StoragesApi } from '../../../../services/storages.api';
import { RegionInputComponent } from '../../../../shared/region-input/region-input.component';
import { AwsRegions } from '../../../../services/aws-regions';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';
import { MatRadioModule } from '@angular/material/radio';
import { Router } from '@angular/router';
import { DataStore } from '../../../../services/data-store.service';
import { RecoveryCodeDialogService } from '../../../../shared/recovery-code-display/recovery-code-dialog.service';

@Component({
    selector: 'app-create-aws-storage',
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
    templateUrl: './create-aws-storage.component.html',
    styleUrl: './create-aws-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateAwsStorageComponent{
    isLoading = signal(false);
    couldNotConnect = signal(false);

    awsRegions = AwsRegions.S3();

    encryption = new FormControl('none', Validators.required);
    name = new FormControl('', [Validators.required]);
    accessKey = new FormControl('', [Validators.required]);
    secretAccessKey = new FormControl('', [Validators.required]);
    region = new FormControl('', [Validators.required]);
    masterPassword = new FormControl('');
    confirmMasterPassword = new FormControl('');

    formGroup: FormGroup;
    wasSubmitted = signal(false);


    constructor(
        private _dataStore: DataStore,
        private _storagesApi: StoragesApi,
        private _router: Router,
        private _recoveryCodeDialog: RecoveryCodeDialogService) {

        this.formGroup = new FormGroup({
            name: this.name,
            accessKey: this.accessKey,
            secretAccessKey: this.secretAccessKey,
            region: this.region,
            encryption: this.encryption,
            masterPassword: this.masterPassword,
            confirmMasterPassword: this.confirmMasterPassword
        });

        this.encryption.valueChanges.subscribe(value => this.updateMasterPasswordValidators(value));
    }

    async onCreateStorage() {
        this.wasSubmitted.set(true);

        if(!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);
            const encryptionType = this.encryption.value! as AppStorageEncryptionType;

            const response = await this._storagesApi.createAwsS3Storage({
                name: this.name.value!,
                accessKey: this.accessKey.value!,
                secretAccessKey: this.secretAccessKey.value!,
                region: this.region.value!,
                encryptionType: encryptionType,
                masterPassword: encryptionType === 'full' ? this.masterPassword.value! : undefined
            });

            this._dataStore.clearDashboardData();

            if (response.recoveryCode) {
                await this._recoveryCodeDialog.showOnce(
                    response.recoveryCode, this.name.value!, encryptionType);
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

    goToStorages() {
        this._router.navigate(['settings/storage']);
    }

    private updateMasterPasswordValidators(encryptionValue: string | null) {
        if (encryptionValue === 'full') {
            this.masterPassword.setValidators([
                Validators.required,
                Validators.minLength(8),
                Validators.pattern(/(?=.*[0-9])/),
                Validators.pattern(/(?=.*[A-Z])/),
                Validators.pattern(/(?=.*[a-z])/),
                Validators.pattern(/(?=.*[!@#$%^&*])/)
            ]);
            this.confirmMasterPassword.setValidators([
                Validators.required,
                this.matchMasterPassword.bind(this)
            ]);
        } else {
            this.masterPassword.clearValidators();
            this.confirmMasterPassword.clearValidators();
            this.masterPassword.setValue('');
            this.confirmMasterPassword.setValue('');
        }
        this.masterPassword.updateValueAndValidity();
        this.confirmMasterPassword.updateValueAndValidity();
    }

    private matchMasterPassword(control: FormControl): { [s: string]: boolean } | null {
        if (this.masterPassword && control.value !== this.masterPassword.value) {
            return { 'passwordMismatch': true };
        }
        return null;
    }
}
