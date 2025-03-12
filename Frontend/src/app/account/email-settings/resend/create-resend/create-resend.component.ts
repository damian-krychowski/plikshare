import { Component, ViewEncapsulation, signal } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatStepperModule } from '@angular/material/stepper';
import { AuthService } from '../../../../services/auth.service';
import { EmailProvidersApi } from '../../../../services/email-providers.api';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { AwsRegions } from '../../../../services/aws-regions';
import { AppEmailProvider } from '../../../../shared/email-provider-item/email-provider-item.component';
import { ConfirmEmailProviderComponent } from '../../confirm-email-provider/confirm-email-provider.component';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';
import { TrimDirective } from '../../../../shared/trim.directive';

@Component({
    selector: 'app-create-resend',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatStepperModule,
        MatAutocompleteModule,
        SecureInputDirective,
        TrimDirective
    ],
    templateUrl: './create-resend.component.html',
    styleUrl: './create-resend.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateResendComponent {
    isLoading = signal(false);
    couldNotConnect = signal(false);
    
    wasSubmitted = signal(false);
    awsRegions = AwsRegions.SES();

    name = new FormControl('', [Validators.required]);
    emailFrom = new FormControl('', [Validators.required]);
    apiKey = new FormControl('', [Validators.required]);    

    configFormGroup: FormGroup;

    constructor(
        public auth: AuthService,
        private _dialog: MatDialog,
        private _emailProvidersApi: EmailProvidersApi,
        public dialogRef: MatDialogRef<CreateResendComponent>) {    
            
        this.configFormGroup = new FormGroup({
            name: this.name,
            emailFrom: this.emailFrom,
            apiKey: this.apiKey
        });
    }

    cancel() {
        this.dialogRef.close();
    }

    async onSubmitConfiguration() {
        this.wasSubmitted.set(true);

        if(!this.configFormGroup.valid)
            return;

        this.isLoading.set(true);

        try {
            const result = await this._emailProvidersApi.createResendEmailProvider({
                emailFrom: this.emailFrom.value!,
                name: this.name.value!,
                apiKey: this.apiKey.value!
            });

            const emailProvider: AppEmailProvider = {
                emailFrom: signal(this.emailFrom.value!),
                externalId: signal(result.externalId),
                isActive: signal(false),
                isConfirmed: signal(false),
                isHighlighted: signal(false),
                isNameEditing: signal(false),
                name: signal(this.name.value!),
                type: signal('resend')
            };

            this._dialog.open(ConfirmEmailProviderComponent, {
                width: '500px',
                position: {
                    top: '100px'
                },
                data: {
                    emailProvider: emailProvider
                }
            });

            this.dialogRef.close(emailProvider);
        } catch (e: any) {
            if(e.error.code == 'email-provider-failure'){
                this.couldNotConnect.set(true);
            } else if (e.error.code === 'email-provider-name-is-not-unique') {
                this.name.setErrors({
                    notUnique: true
                });
            }else {
                console.error(e);
            }
        } finally {
            this.isLoading.set(false);
        }
    }
}
