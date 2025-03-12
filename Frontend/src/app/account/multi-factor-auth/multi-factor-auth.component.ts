import { FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { Router } from "@angular/router";
import { Component, OnInit, ViewEncapsulation, WritableSignal, computed, signal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { AuthService } from "../../services/auth.service";
import { DataStore } from "../../services/data-store.service";
import { ClipboardModule, Clipboard} from '@angular/cdk/clipboard'; 
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { QRCodeComponent } from "./qr-code.component";
import { AccountApi } from "../../services/account.api";
import { MatSnackBar } from "@angular/material/snack-bar";

type ViewState = 'mfa-disabled' | 'mfa-enabled' | 'confirm-mfa-disable';

@Component({
    selector: 'app-multi-factor-auth',
    imports: [
        MatButtonModule,
        MatTooltipModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        ClipboardModule,
        QRCodeComponent
    ],
    templateUrl: './multi-factor-auth.component.html',
    styleUrl: './multi-factor-auth.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class MultiFactorAuthComponent implements OnInit {       
    isLoading = signal(false);

    viewState: WritableSignal<ViewState> = signal('mfa-disabled');
    isWrongTOTPCode = signal(false);
    recoveryCodes: WritableSignal<string[]> = signal([]);
    recoveryCodesLeft: WritableSignal<number | null> = signal(null);
    recoveryCodesAsString = computed(() => this.recoveryCodes().join('\n'));

    qrCodeUri: WritableSignal<string|null> = signal(null);
    private _secret: string | null = null;

    hasAnyRecoveryCodes = computed(() => this.recoveryCodes().length > 0);

    oneTimeCode = new FormControl('', [Validators.required]);    
    formGroup: FormGroup;

    constructor(
        private _clipboard: Clipboard,
        private _snackBar: MatSnackBar,
        public auth: AuthService,
        private _router: Router,
        public dataStore: DataStore,
        private _accountApi: AccountApi
    ) {
        this.formGroup = new FormGroup({
            oneTimeCode: this.oneTimeCode
        });
    }

    async ngOnInit() {
        await this.initializeMfa();
    }

    private async initializeMfa() {
        try {
            this.isLoading.set(true);
            const mfaStatus = await this._accountApi.get2FaStatus();

            this.recoveryCodesLeft.set(mfaStatus.recoveryCodesLeft);

            if(!mfaStatus.isEnabled) {                
                this.qrCodeUri.set(mfaStatus.qrCodeUri!);
                this._secret = this.extractSecret(mfaStatus.qrCodeUri!);
            } else {
                this.viewState.set('mfa-enabled');
            }
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    private extractSecret(url: string): string | null {
        const secretMatch = url.match(/secret=([^&]+)/);
        return secretMatch ? secretMatch[1] : null;
    }

    async onMfaEnabled() {
        if (this.formGroup.valid) {
            try {
                const result = await this._accountApi.enable2Fa({
                    verificationCode: this.oneTimeCode.value!
                })

                if(result.code === 'enabled') {
                    this.recoveryCodes.set(result.recoveryCodes);
                    this.viewState.set('mfa-enabled');
                } else if(result.code === 'invalid-verification-code') {
                    this.isWrongTOTPCode.set(true);
                } else if(result.code === 'failed') {
                    //todo handle
                }
            } catch (err: any) {
                console.error(err); 
            } finally {
                this.isLoading.set(false);
            }
        }
    }

    async disableMfa() {
        try {
            const result = await this._accountApi.disable2Fa();

            if(result.code === 'disabled') {
                this.viewState.set('mfa-disabled');
                await this.initializeMfa();
            } else if(result.code === 'failed') {
                //todo handle
            }
        } catch (err: any) {
            console.error(err);
        }
    }

    async generateNewRecoveryCodes() {
        try {
            this.isLoading.set(true);
            const response = await this._accountApi.generateRecoveryCodes();

            this.recoveryCodesLeft.set(response.recoveryCodes.length);
            this.recoveryCodes.set(response.recoveryCodes);
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    goToAccount() {
        this._router.navigate(['account']);
    }

    copySecret() {
        if(this._secret) {
            this._clipboard.copy(this._secret)
            this._snackBar.open('Secret copied to clipboard', 'Close', {
                duration: 2000,
            });
        }
    }

    copyCodes() {
        if(this.recoveryCodesAsString) {
            this._clipboard.copy(this.recoveryCodesAsString())
            this._snackBar.open('Codes copied to clipboard', 'Close', {
                duration: 2000,
            });
        }
    }
}