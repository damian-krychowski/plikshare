import { Component, OnInit, ViewEncapsulation, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, FormsModule, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AppStorageEncryptionType, HardDriveVolumeItem, StoragesApi } from '../../../../services/storages.api';
import { MatSelectModule } from '@angular/material/select';
import {MatRadioModule} from '@angular/material/radio';
import { Router } from '@angular/router';
import { DataStore } from '../../../../services/data-store.service';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';
import { RecoveryCodeDialogService } from '../../../../shared/recovery-code-display/recovery-code-dialog.service';

@Component({
    selector: 'app-create-hard-drive-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatRadioModule,
        SecureInputDirective
    ],
    templateUrl: './create-hard-drive-storage.component.html',
    styleUrl: './create-hard-drive-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateHardDriveStorageComponent implements OnInit {
    isLoading = signal(true);
    couldNotConnect = signal(false);

    encryption = new FormControl('none', Validators.required);
    name = new FormControl('', [Validators.required]);
    volume = new FormControl(null, [Validators.required]);
    storagePath = new FormControl('', [
        Validators.required,
        this.storagePathValidator(),
        this.storageRestrictedPathValidator()]);
    masterPassword = new FormControl('');
    confirmMasterPassword = new FormControl('');

    formGroup: FormGroup;
    wasSubmitted = signal(false);

    volumes = signal<HardDriveVolumeItem[]>([]);


    constructor(
        private _dataStore: DataStore,
        private _storagesApi: StoragesApi,
        private _router: Router,
        private _recoveryCodeDialog: RecoveryCodeDialogService) {

        this.formGroup = new FormGroup({
            name: this.name,
            volume: this.volume,
            storagePath: this.storagePath,
            encryption: this.encryption,
            masterPassword: this.masterPassword,
            confirmMasterPassword: this.confirmMasterPassword
        });

        this.encryption.valueChanges.subscribe(value => this.updateMasterPasswordValidators(value));
    }

    async ngOnInit(): Promise<void> {
        this.isLoading.set(true);

        try {
            const volumes = await this._storagesApi.getHardDriveVolumes();
            this.volumes.set(volumes.items);
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    markFormAsSubmitted() {
        this.wasSubmitted.set(true);
    }

    async onCreateStorage() {
        this.wasSubmitted.set(true);

        if (!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            const volumePath = this.volume.value!;
            const folderPath = this.storagePath.value!
            const encryptionType = this.encryption.value! as AppStorageEncryptionType;

            const response = await this._storagesApi.createHardDriveStorage({
                name: this.name.value!,
                volumePath: volumePath,
                folderPath: folderPath,
                encryptionType: encryptionType,
                masterPassword: encryptionType === 'full' ? this.masterPassword.value! : undefined
            });

            this._dataStore.clearDashboardData();

            if (response.recoveryCode) {
                await this._recoveryCodeDialog.showOnce(response.recoveryCode, this.name.value!);
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

    private storagePathValidator(): ValidatorFn {
        return (control: AbstractControl): ValidationErrors | null => {
            const value = control.value;
            const isValid = /^\/([a-zA-Z0-9-_]+\/)*[a-zA-Z0-9-_]+$/.test(value);
            return isValid ? null : { invalidPath: true };
        };
    }

    private storageRestrictedPathValidator(): ValidatorFn {
        return (control: AbstractControl): ValidationErrors | null => {
            const volumePath = this.volume.value;

            if(!volumePath)
                return null;

            const storagePath = control.value;

            const selectedVolume = this
                .volumes()
                .find(v => v.path === volumePath);

            if (!selectedVolume)
                return null;

            const isRestricted = selectedVolume
                .restrictedFolderPaths
                .some(restrictedPath => {
                    const restricted = `/${restrictedPath}`;
                    const restrictedSegment = `/${restrictedPath}/`

                    return storagePath == restricted
                        || storagePath.startsWith(restrictedSegment);
                });

            return isRestricted
                ? { restrictedPath: true }
                : null;
        };
    }

    onVolumeChange(){
        this.storagePath.setValue(null);
    }

    goToStorages() {
        this._router.navigate(['settings/storage']);
    }
}
