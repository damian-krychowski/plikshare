import { Component, Inject, ModelSignal, OnInit, ViewEncapsulation, model, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatStepperModule } from '@angular/material/stepper';
import { AuthService } from '../../../services/auth.service';
import { EmailProvidersApi } from '../../../services/email-providers.api';
import { Countdown } from '../../../services/countdown';
import { AppEmailProvider } from '../../../shared/email-provider-item/email-provider-item.component';
import { OptimisticOperation } from '../../../services/optimistic-operation';

@Component({
    selector: 'app-confirm-email-provider',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatStepperModule
    ],
    templateUrl: './confirm-email-provider.component.html',
    styleUrl: './confirm-email-provider.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class ConfirmEmailProviderComponent {
    isLoading = signal(false);
    wasSubmitted = signal(false);
    wasResendRequested = signal(false);
    emailSentTo = signal(this.auth.userEmail());

    verificationCode = new FormControl('', [Validators.required]);
    email = new FormControl(this.auth.userEmail(), [Validators.required, Validators.email]);

    confirmationFormGroup: FormGroup;
    resendFormGroup: FormGroup;
    countdown = new Countdown(30);


    constructor(
        public auth: AuthService,
        private _emailProvidersApi: EmailProvidersApi,
        public dialogRef: MatDialogRef<ConfirmEmailProviderComponent>,
        @Inject(MAT_DIALOG_DATA) public data: {
            emailProvider: AppEmailProvider
        }) {    
 
        this.confirmationFormGroup = new FormGroup({
            verificationCode: this.verificationCode
        });

        this.resendFormGroup = new FormGroup({
            email: this.email
        });

        this.countdown.clear();
    }

    cancel() {
        this.dialogRef.close();
    }

    async onConfirmConfiguration() {
        this.wasSubmitted.set(true);

        if(this.confirmationFormGroup.invalid)
            return;

        this.isLoading.set(true);

        try {
            await this._emailProvidersApi.confirm(
                this.data.emailProvider.externalId(),{
                    confirmationCode: this.verificationCode.value!
                }
            );

            this.data.emailProvider.isConfirmed.set(true);
            this.dialogRef.close(true);
        } catch (err: any) {
            if(err.error.code === 'email-provider-wrong-confirmation-code') {
                this.verificationCode.setErrors({
                    wrongCode: true
                });
            } else if (err.error.code === 'email-provider-is-already-confirmed') {
                this.data.emailProvider.isConfirmed.set(true);
                this.dialogRef.close(true);
            } else {
                console.error(err)            
            }
        } finally {
            this.isLoading.set(false);
        }
    }

    async resendConfirmationEmail() {
        this.wasResendRequested.set(true);

        if(this.resendFormGroup.invalid)
            return;

        if(this.countdown.secondsLeft() > 0)
            return;

        const sendTo = this.email.value!;
        this.emailSentTo.set(sendTo);

        this.isLoading.set(true);

        try {
            await this._emailProvidersApi.resendConfirmationEmail(
                this.data.emailProvider.externalId(),{
                    emailTo: sendTo
                }
            );
            
            this.countdown.start();
        } catch (err) {
            console.error(err)
        } finally {
            this.isLoading.set(false);
        }
    }
}
