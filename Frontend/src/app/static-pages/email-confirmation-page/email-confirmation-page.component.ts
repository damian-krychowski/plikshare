import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, WritableSignal, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TopBarComponent } from '../shared/top-bar/top-bar.component';
import { FooterComponent } from '../shared/footer/footer.component';
import { AuthApi, EmailConfirmationResultCode } from '../../services/auth.api';
import { Subscription } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';

@Component({
    selector: 'app-email-confirmation-page',
    imports: [
        CommonModule,
        TopBarComponent,
        FooterComponent,
        MatButtonModule
    ],
    templateUrl: './email-confirmation-page.component.html',
    styleUrl: './email-confirmation-page.component.scss'
})
export class EmailConfirmationPageComponent implements OnInit, OnDestroy {    
    isLoading = signal(true);
    confirmationResultCode: WritableSignal<EmailConfirmationResultCode | null> = signal(null);
    private _subscription: Subscription | null = null;
    
    constructor(
        private _authApi: AuthApi,
        private _router: Router,
        private _activatedRoute: ActivatedRoute) {
    }
    

    async ngOnInit(): Promise<void> {
        this._subscription = this._activatedRoute.queryParams.subscribe(async (params) => {
            const userId = params['userId'];
            const code = params['code'];
            
            if (userId && code) {
                await this.confirmEmail(userId, code);
            }
        });
    }

    private async confirmEmail(userId: string, code: string) {
        this.isLoading.set(true);
        try {
            const response = await this._authApi.confirmEmail({ 
                userExternalId: userId, 
                code 
            });

            this.confirmationResultCode.set(response.code);
        } catch (err: any) {
            console.error(err);
        } finally {
            this.isLoading.set(false);
        }
    }

    ngOnDestroy(): void {
        this._subscription?.unsubscribe();
    }

    public goToLoginPage() {
        this._router.navigate(['/sign-in']);
    }
}
