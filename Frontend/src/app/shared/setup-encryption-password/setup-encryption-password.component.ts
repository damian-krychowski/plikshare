import { Component, Inject, Optional, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { UserEncryptionPasswordApi } from '../../services/user-encryption-password.api';
import { SecureInputDirective } from '../secure-input.directive';
import { RecoveryCodeDialogService } from '../recovery-code-display/recovery-code-dialog.service';
import { AuthService } from '../../services/auth.service';

export type SetupEncryptionPasswordDialogData = {
    invitationCode: string | null;
    mode: 'post-invitation-signup' | 'account';
};

@Component({
    selector: 'app-setup-encryption-password',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective
    ],
    templateUrl: './setup-encryption-password.component.html',
    styleUrl: './setup-encryption-password.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class SetupEncryptionPasswordComponent {
    isLoading = signal(false);
    isSomethingWentWrong = signal(false);

    password = new FormControl('', [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/(?=.*[0-9])/),
        Validators.pattern(/(?=.*[A-Z])/),
        Validators.pattern(/(?=.*[a-z])/),
        Validators.pattern(/(?=.*[!@#$%^&*])/)
    ]);
    confirmPassword = new FormControl('', [Validators.required]);

    formGroup: FormGroup;

    readonly mode: 'post-invitation-signup' | 'account';
    readonly isPostInvitationSignup: boolean;
    private readonly _invitationCode: string | null;

    constructor(
        private _encryptionApi: UserEncryptionPasswordApi,
        private _recoveryCodeDialog: RecoveryCodeDialogService,
        private _auth: AuthService,
        public dialogRef: MatDialogRef<SetupEncryptionPasswordComponent, boolean>,
        @Optional() @Inject(MAT_DIALOG_DATA) data: SetupEncryptionPasswordDialogData | null) {

        this._invitationCode = data?.invitationCode ?? null;
        this.mode = data?.mode ?? 'account';
        this.isPostInvitationSignup = this.mode === 'post-invitation-signup';

        this.formGroup = new FormGroup({
            password: this.password,
            confirmPassword: this.confirmPassword
        });

        this.confirmPassword.addValidators(
            (control) => this.matchPassword(control as FormControl));
    }

    async onSetup() {
        if (!this.formGroup.valid)
            return;

        this.isSomethingWentWrong.set(false);

        try {
            this.isLoading.set(true);

            const response = await this._encryptionApi.setup({
                encryptionPassword: this.password.value!,
                invitationCode: this._invitationCode
            });

            this._auth.notifyEncryptionUnlocked();

            this.dialogRef.close(true);

            await this._auth.initiateSession();

            await this._recoveryCodeDialog.show({
                recoveryCode: response.recoveryCode,
                title: 'Save your recovery code',
                warning: 'If you forget your encryption password, this code is the only way to reset it and regain access to your encrypted workspaces.',
                dangerNotice: 'Store it somewhere secure — password manager, offline note, safe. Without this code, forgetting your password means you will need a storage administrator to re-grant you access to each storage individually.',
                fileHeader: 'PlikShare encryption password recovery code',
                fileWarning: 'If you forget your encryption password, this code is the ONLY way to reset it. It will not be shown again. Store it securely.',
                fileName: 'plikshare-encryption-recovery.txt'
            });

        } catch (err: any) {
            if (err.status === 409 && err.error?.code === 'user-encryption-already-configured') {
                this.dialogRef.close(false);
            } else {
                this.isSomethingWentWrong.set(true);
                console.error(err);
            }
        } finally {
            this.isLoading.set(false);
        }
    }

    private matchPassword(control: FormControl): { [s: string]: boolean } | null {
        if (this.password && control.value !== this.password.value) {
            return { 'passwordMismatch': true };
        }
        return null;
    }

    cancel() {
        this.dialogRef.close(false);
    }
}
