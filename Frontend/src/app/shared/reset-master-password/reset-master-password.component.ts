import { Component, ViewEncapsulation, computed, signal } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { UserEncryptionPasswordApi } from '../../services/user-encryption-password.api';
import { AuthService } from '../../services/auth.service';
import { SecureInputDirective } from '../secure-input.directive';

@Component({
    selector: 'app-reset-encryption-password',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective
    ],
    templateUrl: './reset-master-password.component.html',
    styleUrl: './reset-master-password.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class ResetEncryptionPasswordComponent {
    isLoading = signal(false);
    serverError = signal<'malformed' | 'invalid' | 'other' | null>(null);

    recoveryCode = new FormControl('', [Validators.required]);
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

    wordCount = computed(() => {
        const raw = this.recoveryCode.value ?? '';
        return raw.trim().split(/\s+/).filter(w => w.length > 0).length;
    });

    constructor(
        private _encryptionApi: UserEncryptionPasswordApi,
        private _auth: AuthService,
        public dialogRef: MatDialogRef<ResetEncryptionPasswordComponent, boolean>) {

        this.formGroup = new FormGroup({
            recoveryCode: this.recoveryCode,
            newPassword: this.newPassword,
            confirmNewPassword: this.confirmNewPassword
        });

        this.newPassword.valueChanges.subscribe(() => this.confirmNewPassword.updateValueAndValidity());
    }

    async onSubmit() {
        if (!this.formGroup.valid)
            return;

        this.serverError.set(null);
        this.isLoading.set(true);

        try {
            await this._encryptionApi.reset({
                recoveryCode: this.recoveryCode.value!,
                newPassword: this.newPassword.value!
            });

            this._auth.notifyEncryptionUnlocked();
            this.dialogRef.close(true);
        } catch (err: any) {
            const code = err?.error?.code;
            if (code === 'malformed-recovery-code') {
                this.serverError.set('malformed');
            } else if (code === 'invalid-recovery-code') {
                this.serverError.set('invalid');
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

    private matchNewPassword(control: FormControl): { [s: string]: boolean } | null {
        if (this.newPassword && control.value !== this.newPassword.value)
            return { passwordMismatch: true };
        return null;
    }
}
