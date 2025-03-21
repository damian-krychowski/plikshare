import { CommonModule } from '@angular/common';
import { Component, OnInit, signal, WritableSignal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { TopBarComponent } from '../shared/top-bar/top-bar.component';
import { FooterComponent } from '../shared/footer/footer.component';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthApi } from '../../services/auth.api';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { SecureInputDirective } from '../../shared/secure-input.directive';
import { EntryPageService } from '../../services/entry-page.service';
import { AntiforgeryApi } from '../../services/antiforgery.api';

type ViewState = 'sign-in' | '2fa-required' | 'forgot-password' | 'forgot-password-confirmation' | 'use-recovery-code';

@Component({
    selector: 'app-sign-in-page',
    imports: [
        CommonModule,
        FormsModule,
        RouterLink,
        TopBarComponent,
        FooterComponent,
        ReactiveFormsModule,
        MatButtonModule,
        MatCheckboxModule,
        MatFormFieldModule,
        SecureInputDirective
    ],
    templateUrl: './sign-in-page.component.html',
    styleUrl: './sign-in-page.component.scss'
})
export class SignInPageComponent implements OnInit {
    isLoading = signal(false);
    showLoginError = signal(false);
    show2FaCodeError = signal(false);
    showRecoveryCodeError = signal(false);
    viewState: WritableSignal<ViewState> = signal('sign-in');
    

    email = new FormControl('', [Validators.required, Validators.email]);
    password = new FormControl('', [Validators.required]);
    rememberMe = new FormControl(false);

    signInForm: FormGroup;
    wasSignInSubmitted = false;
    
    forgotPasswordEmail = new FormControl('', [Validators.required, Validators.email]);
    forgotPasswordForm: FormGroup;
    wasForgotPasswordSubmitted = false;

    confirm2FaCodeForm: FormGroup;
    wasConfirm2FaSubmitted = false;
    oneTimeCode = new FormControl('', [Validators.required]);
    rememberDevice = new FormControl(false);

    recoveryCodeForm: FormGroup;
    wasRecoveryCodeSubmitted = false;
    recoveryCode = new FormControl('', [Validators.required]);

    constructor(
        public entryPage: EntryPageService,
        private _authApi: AuthApi,
        private _antiforgeryApi: AntiforgeryApi,
        private _authService: AuthService,
        private router: Router) {

        this.signInForm = new FormGroup({
            email: this.email,
            password: this.password,
            rememberMe: this.rememberMe
        });

        this.forgotPasswordForm = new FormGroup({
            email: this.forgotPasswordEmail
        });

        this.confirm2FaCodeForm = new FormGroup({
            oneTimeCode: this.oneTimeCode,
            rememberDevice: this.rememberDevice
        });

        this.recoveryCodeForm = new FormGroup({
            recoveryCode: this.recoveryCode,
        });
    }

    async ngOnInit(): Promise<void> {
        if(await this._authService.isAuthenticatedAsync()) {
            this.router.navigate(['workspaces']);
        }
    }

    async onSignIn() {
        this.wasSignInSubmitted = true;

        if (!this.signInForm.valid)
            return;

        try {
            this.isLoading.set(true);

            const result = await this._authApi.singIn({
                email: this.email.value!,
                password: this.password.value!,
                rememberMe: this.rememberMe.value ?? false
            });

            if(result.code == 'signed-in'){                
                await this._authService.initiateSession();
                await this.router.navigate(['workspaces']);
            }
            else if(result.code == '2fa-required') {
                this.viewState.set('2fa-required');
            }
            else if(result.code =='sign-in-failed') {
                this.showLoginError.set(true);
            }
        } catch (err: any) {
            console.error(err);
        } finally {
            this.isLoading.set(false);
        }
    }

    async onForgotPassword() {
        this.wasForgotPasswordSubmitted = true;

        if (!this.forgotPasswordForm.valid)
            return;

        try {
            this.isLoading.set(true);

            await this._authApi.forgotPassword({
                email: this.forgotPasswordEmail.value!
            });

            this.viewState.set('forgot-password-confirmation');
        } catch (err: any) {
            console.error(err);
        } finally {
            this.isLoading.set(false);
        }
    }

    async onConfirm2FaCode() {
        this.wasConfirm2FaSubmitted = true;

        if (!this.confirm2FaCodeForm.valid)
            return;

        try {
            this.isLoading.set(true);

            const result = await this._authApi.singIn2Fa({
                verificationCode: this.oneTimeCode.value!,
                rememberMe: this.rememberMe.value ?? false,
                rememberDevice: this.rememberDevice.value ?? false
            });

            if(result.code == 'signed-in'){
                await this._authService.initiateSession();
                await this.router.navigate(['workspaces']);
            }
            else if(result.code == 'invalid-verification-code') {
                this.show2FaCodeError.set(true);
            }
            else if(result.code =='sign-in-failed') {
                this.showLoginError.set(true);
            }
        } catch (err: any) {
            console.error(err);
        } finally {
            this.isLoading.set(false);
        }
    }

    async onConfirmRecoveryCode() {
        this.wasRecoveryCodeSubmitted = true;

        if (!this.recoveryCodeForm.valid)
            return;

        try {
            this.isLoading.set(true);

            const result = await this._authApi.singInRecoveryCode({
                recoveryCode: this.recoveryCode.value!
            });

            if(result.code == 'signed-in'){
                await this._authService.initiateSession();
                await this.router.navigate(['workspaces']);
            }
            else if(result.code == 'invalid-recovery-code') {
                this.showRecoveryCodeError.set(true);
            }
            else if(result.code =='sign-in-failed') {
                this.showLoginError.set(true);
            }
        } catch (err: any) {
            console.error(err);
        } finally {
            this.isLoading.set(false);
        }
    }

    public goToForgotPassword() {
        this.viewState.set('forgot-password');
    }

    public goToSignIn() {
        this.viewState.set('sign-in');
    }
    
    public goToUseRecoveryCode() {
        this.viewState.set('use-recovery-code');
    }
}
