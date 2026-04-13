import { Component, Inject, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';
import { StoragesApi } from '../../services/storages.api';
import { FullEncryptionSessionsStore } from '../../services/full-encryption-sessions.store';
import { SecureInputDirective } from '../secure-input.directive';
import { ResetMasterPasswordComponent, ResetMasterPasswordDialogData } from '../reset-master-password/reset-master-password.component';

export interface UnlockFullEncryptionDialogData {
    storageExternalId: string;
}

@Component({
    selector: 'app-unlock-full-encryption',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective
    ],
    templateUrl: './unlock-full-encryption.component.html',
    styleUrl: './unlock-full-encryption.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class UnlockFullEncryptionComponent {
    isLoading = signal(false);
    isWrongPassword = signal(false);
    isSomethingWentWrong = signal(false);

    masterPassword = new FormControl('', [Validators.required]);

    formGroup: FormGroup;

    constructor(
        @Inject(MAT_DIALOG_DATA) public data: UnlockFullEncryptionDialogData,
        private _storagesApi: StoragesApi,
        private _sessionsStore: FullEncryptionSessionsStore,
        private _dialog: MatDialog,
        public dialogRef: MatDialogRef<UnlockFullEncryptionComponent, boolean>) {

        this.formGroup = new FormGroup({
            masterPassword: this.masterPassword
        });
    }

    async onForgotPassword() {
        const ref = this._dialog.open<
            ResetMasterPasswordComponent,
            ResetMasterPasswordDialogData,
            boolean
        >(ResetMasterPasswordComponent, {
            width: '500px',
            position: { top: '80px' },
            disableClose: true,
            data: { storageExternalId: this.data.storageExternalId }
        });

        const wasReset = await firstValueFrom(ref.afterClosed());

        if (wasReset === true) {
            // Password was reset but no session issued server-side.
            // Close the unlock dialog; the user will be prompted to unlock again
            // on the next operation that requires a session. Clear the password
            // field so the caller-triggered flow doesn't carry stale input.
            this.masterPassword.setValue('');
            this.dialogRef.close(false);
        }
    }

    async onUnlock() {
        if (!this.formGroup.valid)
            return;

        this.isWrongPassword.set(false);
        this.isSomethingWentWrong.set(false);

        try {
            this.isLoading.set(true);

            await this._storagesApi.unlockFullEncryption(
                this.data.storageExternalId,
                { masterPassword: this.masterPassword.value! });

            await this._sessionsStore.notifyUnlocked();

            this.dialogRef.close(true);
        } catch (err: any) {
            if (err.status === 400 && err.error?.code === 'invalid-master-password') {
                this.isWrongPassword.set(true);
            } else {
                this.isSomethingWentWrong.set(true);
                console.error(err);
            }
        } finally {
            this.isLoading.set(false);
        }
    }

    onPasswordChange() {
        this.isWrongPassword.set(false);
    }

    cancel() {
        this.dialogRef.close(false);
    }
}
