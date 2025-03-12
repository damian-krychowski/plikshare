import { Component, Inject, OnInit, ViewEncapsulation, signal } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormGroupDirective, FormsModule, NgForm, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../services/auth.service';
import { SecureInputDirective } from '../../shared/secure-input.directive';

@Component({
    selector: 'app-change-password',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective
    ],
    templateUrl: './change-password.component.html',
    styleUrl: './change-password.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class ChangePasswordComponent implements OnInit{
    isWrongPassword = signal(false);
    isAttemptLimitExceeded = signal(false);
    isSomethingWentWrong = signal(false);

    isLoading = signal(false);

    username= new FormControl('');
    oldPassword = new FormControl('', [Validators.required]);    
    newPassword = new FormControl('', [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/(?=.*[0-9])/), // At least one number
        Validators.pattern(/(?=.*[A-Z])/), // At least one uppercase letter
        Validators.pattern(/(?=.*[a-z])/), // At least one lowercase letter
        Validators.pattern(/(?=.*[!@#$%^&*])/), // At least one special character
    ]);    
    confirmNewPassword = new FormControl('', [Validators.required, this.matchNewPassword.bind(this)]);

    formGroup: FormGroup;
      
    constructor(
        private _auth: AuthService,
        public dialogRef: MatDialogRef<ChangePasswordComponent>) {    
            
        this.formGroup = new FormGroup({
            username: this.username,
            oldPassword: this.oldPassword,
            newPassword: this.newPassword,
            confirmNewPassword: this.confirmNewPassword
        });
    }

    async ngOnInit() {
        const email = await this._auth.getUserEmail();
        this.username.setValue(email);
    }

    matchNewPassword(control: FormControl): { [s: string]: boolean } | null {
        if (this.newPassword && control.value !== this.newPassword.value) {
            return { 'passwordMismatch': true };
        }
        return null;
    }

    async onPasswordChanged() {
        if (this.formGroup.valid) {
            try {
                this.isLoading.set(true);

                const result = await this._auth.changePassword({
                    oldPassword: this.oldPassword.value!,
                    newPassword: this.newPassword.value!                   
                });

                if(result.code === 'success') {
                    this.dialogRef.close();
                } else if(result.code === 'password-mismatch') {
                    this.isWrongPassword.set(true);
                } else {
                    this.isSomethingWentWrong.set(true);
                }
            } catch (err: any) {
                this.isSomethingWentWrong.set(true);
                console.error(err);
            } finally {
                this.isLoading.set(false);
            }
        }
    }

    onOldPasswordChange() {
        this.isWrongPassword.set(false);
    }

    cancel() {
        this.dialogRef.close();
    }
}
