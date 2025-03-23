import { CommonModule } from '@angular/common';
import { Component, NgZone, OnDestroy, OnInit, WritableSignal, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TopBarComponent } from '../shared/top-bar/top-bar.component';
import { FooterComponent } from '../shared/footer/footer.component';
import { AuthApi } from '../../services/auth.api';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { Subscription } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { Countdown } from '../../services/countdown';
import { SecureInputDirective } from '../../shared/secure-input.directive';
import { EntryPageService } from '../../services/entry-page.service';
import { SignUpCheckboxDto } from '../../services/general-settings.api';

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
export class SignUpPageComponent implements OnInit, OnDestroy {
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
    checkboxes: FormArray<FormControl<boolean | null>>;

    countdown = new Countdown(60);

    private _subscriptions: Subscription[] = [];
    private _invitationCode: string | null = null;

    constructor(
        public entryPage: EntryPageService,
        private _formBuilder: FormBuilder,
        private _authService: AuthService,
        private _authApi: AuthApi,
        private _activatedRoute: ActivatedRoute,
        private _router: Router) {

        this.checkboxes = this._formBuilder.array<FormControl<boolean | null>>([]);

        this.formGroup = new FormGroup({
            username: this.username,
            password: this.password,
            confirmedPassword: this.confirmedPassword,
            acceptTerms: this.acceptTerms,
            checkboxes: this.checkboxes
        });
    }

    async ngOnInit(): Promise<void> {
        this.isLoading.set(true);

        if(await this._authService.isAuthenticatedAsync()) {
            this._router.navigate(['workspaces']);
        }

        const routeSub = this._activatedRoute.queryParams.subscribe(async (params) => {
            this._invitationCode = params['invitationCode'];
        });
        this._subscriptions.push(routeSub);

        const loadedSub = this.entryPage.loaded$.subscribe((result) => {
            if(!result)
                return;

            if (result.success) {
                this.initCheckboxes();
            } else {
                console.error('Failed to load entry page data', result.error);
            }

            this.isLoading.set(false);     
        });
        this._subscriptions.push(loadedSub);
    }

    ngOnDestroy(): void {
        this._subscriptions.forEach(sub => sub.unsubscribe());
        this._subscriptions = [];
    }

    private initCheckboxes(): void {    
        this.checkboxes.clear();

        this.entryPage.signUpCheckboxes().forEach(checkbox => {
            const validator = checkbox.isRequired 
                ? Validators.requiredTrue 
                : null;

            this.checkboxes.push(new FormControl(false, validator));
        });
    }

    getSelectedCheckboxIds(): number[] {
        const selectedIds: number[] = [];

        this.entryPage.signUpCheckboxes().forEach((checkbox, index) => {
            if (this.checkboxes.at(index).value === true) {
                selectedIds.push(checkbox.id);
            }
        });

        return selectedIds;
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
                invitationCode: this._invitationCode || null,
                selectedCheckboxIds: this.getSelectedCheckboxIds()
            });

            if (result.code == 'confirmation-email-sent') {
                this.countdown.start();
                this.viewState.set('confirm-email');
            } else if(result.code === 'invitation-required') {
                this.isInvitationRequired.set(true);
            } else if(result.code === 'signed-up-and-signed-in'){
                this._authService.initiateSession();
                await this._router.navigate(['workspaces']);
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