import { Component, Inject, OnDestroy, OnInit, ViewEncapsulation, WritableSignal, computed, signal } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormGroupDirective, FormsModule, NgForm, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatStepperModule } from '@angular/material/stepper';
import { AuthService } from '../../../../services/auth.service';
import { EmailProvidersApi } from '../../../../services/email-providers.api';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { Observable, Subscription } from 'rxjs';
import { AwsRegions } from '../../../../services/aws-regions';
import { RegionInputComponent } from '../../../../shared/region-input/region-input.component';
import { AppEmailProvider } from '../../../../shared/email-provider-item/email-provider-item.component';
import { ConfirmEmailProviderComponent } from '../../confirm-email-provider/confirm-email-provider.component';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';
import { TrimDirective } from '../../../../shared/trim.directive';

@Component({
    selector: 'app-create-aws-ses',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatStepperModule,
        MatAutocompleteModule,
        RegionInputComponent,
        SecureInputDirective,
        TrimDirective
    ],
    templateUrl: './create-aws-ses.component.html',
    styleUrl: './create-aws-ses.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateAwsSesComponent {
    isLoading = signal(false);
    couldNotConnect = signal(false);
    
    wasSubmitted = signal(false);
    awsRegions = AwsRegions.SES();

    name = new FormControl('', [Validators.required]);
    emailFrom = new FormControl('', [Validators.required]);
    accessKey = new FormControl('', [Validators.required]);    
    secretAccessKey = new FormControl('', [Validators.required]);    
    region = new FormControl('', [Validators.required]);    

    configFormGroup: FormGroup;

    constructor(
        public auth: AuthService,
        private _dialog: MatDialog,
        private _emailProvidersApi: EmailProvidersApi,
        public dialogRef: MatDialogRef<CreateAwsSesComponent>) {    
            
        this.configFormGroup = new FormGroup({
            name: this.name,
            emailFrom: this.emailFrom,
            accessKey: this.accessKey,
            secretAccessKey: this.secretAccessKey,
            region: this.region
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
            const result = await this._emailProvidersApi.createAwsSesEmailProvider({
                accessKey: this.accessKey.value!,
                emailFrom: this.emailFrom.value!,
                name: this.name.value!,
                region: this.region.value!,
                secretAccessKey: this.secretAccessKey.value!
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
