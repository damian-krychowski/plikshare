import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, WritableSignal, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TopBarComponent } from '../shared/top-bar/top-bar.component';
import { FooterComponent } from '../shared/footer/footer.component';
import { AuthApi, PasswordResetResultCode } from '../../services/auth.api';
import { Subscription } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';

@Component({
    selector: 'app-reset-password-page',
    imports: [
        CommonModule,
        TopBarComponent,
        FooterComponent,
        ReactiveFormsModule,
        MatButtonModule,
        MatCheckboxModule,
        MatFormFieldModule,
    ],
    templateUrl: './reset-password-page.component.html',
    styleUrl: './reset-password-page.component.scss'
})
export class ResetPasswordPageComponent implements OnInit, OnDestroy {    
    isLoading = signal(false);
    resultCode: WritableSignal<PasswordResetResultCode | null> = signal(null);
        
    private _userId: string | null = null;
    private _code: string | null = null;
    
    password = new FormControl('', [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/(?=.*[0-9])/), // At least one number
        Validators.pattern(/(?=.*[A-Z])/), // At least one uppercase letter
        Validators.pattern(/(?=.*[a-z])/), // At least one lowercase letter
        Validators.pattern(/(?=.*[!@#$%^&*])/), // At least one special character
    ]);
    confirmedPassword = new FormControl('', [Validators.required]);

    formGroup: FormGroup;
    wasSubmitted = false;

    private _subscription: Subscription | null = null;
    
    constructor(
        private _authApi: AuthApi,
        private _router: Router,
        private _activatedRoute: ActivatedRoute) {

        this.formGroup = new FormGroup({
            password: this.password,
            confirmedPassword: this.confirmedPassword
        });
    }
    
    async ngOnInit(): Promise<void> {
        this._subscription = this._activatedRoute.queryParams.subscribe(async (params) => {
            this._userId = params['userId'];
            this._code = params['code'];
        });
    }

    ngOnDestroy(): void {
        this._subscription?.unsubscribe();
    }

    public async onResetPassword() {
        if(this._userId == null || this._code == null)
            return;

        this.isLoading.set(true);

        try {
            const response = await this._authApi.resetPassword({ 
                userExternalId: this._userId, 
                code: this._code,
                newPassword: this.password.value!
            });

            this.resultCode.set(response.code);
        } catch (err: any) {
            console.error(err);
        } finally {
            this.isLoading.set(false);
        }
    }

    public goToLoginPage() {
        this._router.navigate(['/sign-in']);
    }
}
