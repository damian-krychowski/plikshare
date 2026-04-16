import { Component, ViewEncapsulation, signal } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { UserEncryptionPasswordApi } from '../../services/user-encryption-password.api';
import { AuthService } from '../../services/auth.service';
import { SecureInputDirective } from '../secure-input.directive';

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

    encryptionPassword = new FormControl('', [Validators.required]);

    formGroup: FormGroup;

    constructor(
        private _encryptionApi: UserEncryptionPasswordApi,
        private _auth: AuthService,
        public dialogRef: MatDialogRef<UnlockFullEncryptionComponent, boolean>) {

        this.formGroup = new FormGroup({
            encryptionPassword: this.encryptionPassword
        });
    }

    async onUnlock() {
        if (!this.formGroup.valid)
            return;

        this.isWrongPassword.set(false);
        this.isSomethingWentWrong.set(false);

        try {
            this.isLoading.set(true);

            await this._encryptionApi.unlock({
                encryptionPassword: this.encryptionPassword.value!
            });

            this._auth.notifyEncryptionUnlocked();

            this.dialogRef.close(true);
        } catch (err: any) {
            if (err.status === 400 && err.error?.code === 'invalid-encryption-password') {
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
