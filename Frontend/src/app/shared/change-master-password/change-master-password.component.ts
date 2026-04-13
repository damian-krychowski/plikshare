import { Component, Inject, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';
import { StoragesApi } from '../../services/storages.api';
import { SecureInputDirective } from '../secure-input.directive';
import { ResetMasterPasswordComponent, ResetMasterPasswordDialogData } from '../reset-master-password/reset-master-password.component';

export interface ChangeMasterPasswordDialogData {
    storageExternalId: string;
    storageName?: string;
}

@Component({
    selector: 'app-change-master-password',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective
    ],
    templateUrl: './change-master-password.component.html',
    styleUrl: './change-master-password.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class ChangeMasterPasswordComponent {
    isLoading = signal(false);
    serverError = signal<'invalid-old-password' | 'other' | null>(null);

    oldPassword = new FormControl('', [Validators.required]);
    newPassword = new FormControl('', [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/(?=.*[0-9])/),
        Validators.pattern(/(?=.*[A-Z])/),
        Validators.pattern(/(?=.*[a-z])/),
        Validators.pattern(/(?=.*[!@#$%^&*])/)
    ]);
    confirmNewPassword = new FormControl('', [Validators.required, this.matchNewPassword.bind(this)]);

    formGroup: FormGroup;

    constructor(
        @Inject(MAT_DIALOG_DATA) public data: ChangeMasterPasswordDialogData,
        private _storagesApi: StoragesApi,
        private _dialog: MatDialog,
        public dialogRef: MatDialogRef<ChangeMasterPasswordComponent, boolean>) {

        this.formGroup = new FormGroup({
            oldPassword: this.oldPassword,
            newPassword: this.newPassword,
            confirmNewPassword: this.confirmNewPassword
        });

        this.newPassword.valueChanges.subscribe(() => this.confirmNewPassword.updateValueAndValidity());
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
            data: {
                storageExternalId: this.data.storageExternalId,
                storageName: this.data.storageName
            }
        });

        const wasReset = await firstValueFrom(ref.afterClosed());

        if (wasReset === true) {
            this.dialogRef.close(true);
        }
    }

    async onSubmit() {
        if (!this.formGroup.valid)
            return;

        this.serverError.set(null);
        this.isLoading.set(true);

        try {
            await this._storagesApi.changeMasterPassword(
                this.data.storageExternalId,
                {
                    oldPassword: this.oldPassword.value!,
                    newPassword: this.newPassword.value!
                });

            this.dialogRef.close(true);
        } catch (err: any) {
            const code = err?.error?.code;
            if (code === 'invalid-master-password') {
                this.serverError.set('invalid-old-password');
            } else {
                this.serverError.set('other');
                console.error(err);
            }
        } finally {
            this.isLoading.set(false);
        }
    }

    cancel() {
        this.dialogRef.close(false);
    }

    onOldPasswordChange() {
        if (this.serverError() === 'invalid-old-password')
            this.serverError.set(null);
    }

    private matchNewPassword(control: FormControl): { [s: string]: boolean } | null {
        if (this.newPassword && control.value !== this.newPassword.value)
            return { passwordMismatch: true };
        return null;
    }
}
