import { CommonModule } from '@angular/common';
import { Component, OnInit, WritableSignal, computed, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink, RouterOutlet } from '@angular/router';
import { TopBarComponent } from '../shared/top-bar/top-bar.component';
import { FooterComponent } from '../shared/footer/footer.component';
import { AuthApi } from '../../services/auth.api';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { Subscription, takeWhile, timer } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { Countdown } from '../../services/countdown';
import { SecureInputDirective } from '../../shared/secure-input.directive';
import { EntryPageService } from '../../services/entry-page.service';

type ViewState = 'sign-up' | 'confirm-email';


@Component({
    selector: 'app-sign-up-page',
    imports: [
        CommonModule,
        RouterLink,
        TopBarComponent,
        FooterComponent,
        ReactiveFormsModule,
        MatButtonModule,
        MatCheckboxModule,
        MatFormFieldModule,
        SecureInputDirective
    ],
    templateUrl: './sign-up-page.component.html',
    styleUrl: './sign-up-page.component.scss'
})
export class SignUpPageComponent implements OnInit {
    viewState: WritableSignal<ViewState> = signal('sign-up');
    isLoading = signal(false);
    isInvitationRequired = signal(false);

    password = new FormControl('', [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/(?=.*[0-9])/), // At least one number
        Validators.pattern(/(?=.*[A-Z])/), // At least one uppercase letter
        Validators.pattern(/(?=.*[a-z])/), // At least one lowercase letter
        Validators.pattern(/(?=.*[!@#$%^&*])/), // At least one special character
    ]);
    confirmedPassword = new FormControl('', [Validators.required]);
    acceptTerms = new FormControl(false, [Validators.required]);
    username = new FormControl('', [Validators.required, Validators.email]);

    formGroup: FormGroup;
    wasSubmitted = false;

    countdown = new Countdown(60);

    private _subscription: Subscription| null = null;
    private _invitationCode: string | null = null;

    constructor(
        public entryPage: EntryPageService,
        private _authService: AuthService,
        private _authApi: AuthApi,
        private _activatedRoute: ActivatedRoute,
        private router: Router) {

        this.formGroup = new FormGroup({
            username: this.username,
            password: this.password,
            confirmedPassword: this.confirmedPassword,
            acceptTerms: this.acceptTerms
        });
    }

    async ngOnInit(): Promise<void> {
        if(await this._authService.isAuthenticatedAsync()) {
            this.router.navigate(['workspaces']);
        }

        this._subscription = this._activatedRoute.queryParams.subscribe(async (params) => {
            this._invitationCode = params['invitationCode'];
        });
    }

    async onSignUp() {
        this.wasSubmitted = true;

        if (!this.formGroup.valid)
            return;

        try {
            this.isLoading.set(true);

            const result = await this._authApi.signUp({
                email: this.username.value!,
                password: this.password.value!,
                invitationCode: this._invitationCode
            });

            if (result.code == 'confirmation-email-sent') {
                this.countdown.start();
                this.viewState.set('confirm-email');
            } else if(result.code === 'invitation-required') {
                this.isInvitationRequired.set(true);
            } else if(result.code === 'signed-up-and-signed-in'){
                this._authService.initiateSession();
                await this.router.navigate(['workspaces']);
            }
        } catch (err: any) {
            console.error(err);
        } finally {
            this.isLoading.set(false);
        }
    }

    async resendConfirmationEmail() {
        try {
            this.isLoading.set(true);

            const result = await this._authApi.resendConfirmationLink({
                email: this.username.value!,
            });

            this.countdown.start();
        } catch (err: any) {
            console.error(err);
        } finally {
            this.isLoading.set(false);
        }
    }

    public goBackFromConfirmationEmail() {
        this.viewState.set('sign-up');
    }
}


