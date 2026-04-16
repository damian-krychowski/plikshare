import { Component, OnInit, ViewEncapsulation, computed, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, FormsModule, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AppStorageEncryptionType, HardDriveVolumeItem, StoragesApi } from '../../../../services/storages.api';
import { MatSelectModule } from '@angular/material/select';
import {MatRadioModule} from '@angular/material/radio';
import { Router } from '@angular/router';
import { DataStore } from '../../../../services/data-store.service';
import { RecoveryCodeDialogService } from '../../../../shared/recovery-code-display/recovery-code-dialog.service';
import { AuthService } from '../../../../services/auth.service';
import { MatDialog } from '@angular/material/dialog';
import { SetupEncryptionPasswordComponent } from '../../../../shared/setup-encryption-password/setup-encryption-password.component';
import { firstValueFrom } from 'rxjs';

@Component({
    selector: 'app-create-hard-drive-storage',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatRadioModule
    ],
    templateUrl: './create-hard-drive-storage.component.html',
    styleUrl: './create-hard-drive-storage.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateHardDriveStorageComponent implements OnInit {
    isLoading = signal(true);
    couldNotConnect = signal(false);

    selectedEncryption = signal<string>('none');
    needsEncryptionSetup = computed(() =>
        this.selectedEncryption() === 'full' && !this.auth.isEncryptionConfigured());

    encryption = new FormControl('none', Validators.required);
    name = new FormControl('', [Validators.required]);
    volume = new FormControl(null, [Validators.required]);
    storagePath = new FormControl('', [
        Validators.required,
        this.storagePathValidator(),
        this.storageRestrictedPathValidator()]);

    formGroup: FormGroup;
    wasSubmitted = signal(false);

    volumes = signal<HardDriveVolumeItem[]>([]);


    constructor(
        private _dataStore: DataStore,
        private _storagesApi: StoragesApi,
        private _router: Router,
        private _recoveryCodeDialog: RecoveryCodeDialogService,
        private _dialog: MatDialog,
        public auth: AuthService) {

        this.formGroup = new FormGroup({
            name: this.name,
            volume: this.volume,
            storagePath: this.storagePath,
            encryption: this.encryption
        });

        this.encryption.valueChanges.subscribe(value =>
            this.selectedEncryption.set(value ?? 'none'));
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
                encryptionType: encryptionType
            });

            this._dataStore.clearDashboardData();

            if (response.recoveryCode) {
                const storageName = this.name.value!;
                await this._recoveryCodeDialog.show({
                    recoveryCode: response.recoveryCode,
                    title: 'Save your storage recovery code',
                    warning: `If the database for "${storageName}" is ever lost or damaged, this code is the only way to recover the storage encryption key and decrypt your files.`,
                    dangerNotice: `Anyone who obtains this code can decrypt files on this storage. Store it somewhere only you can reach — password manager, offline note, safe. If you lose this code and the database is damaged, your files cannot be recovered.`,
                    fileHeader: `PlikShare storage recovery code\nStorage: ${storageName}`,
                    fileWarning: 'If the database is ever lost or damaged, this code is the ONLY way to recover the storage encryption key. It will not be shown again. Guard it like a password.',
                    fileName: `plikshare-recovery-${storageName.replace(/[^a-zA-Z0-9-_]/g, '_')}.txt`
                });
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

    async openSetupEncryptionPassword() {
        const ref = this._dialog.open(SetupEncryptionPasswordComponent, {
            width: '500px',
            position: { top: '100px' },
            disableClose: true
        });

        await firstValueFrom(ref.afterClosed());
    }

    goToStorages() {
        this._router.navigate(['settings/storage']);
    }
}
