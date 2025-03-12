import { Component, ViewEncapsulation, signal } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatStepperModule } from '@angular/material/stepper';
import { AuthService } from '../../../../services/auth.service';
import { EmailProvidersApi, SmtpSslMode } from '../../../../services/email-providers.api';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { AwsRegions } from '../../../../services/aws-regions';
import { AppEmailProvider } from '../../../../shared/email-provider-item/email-provider-item.component';
import { ConfirmEmailProviderComponent } from '../../confirm-email-provider/confirm-email-provider.component';
import { SecureInputDirective } from '../../../../shared/secure-input.directive';
import { TrimDirective } from '../../../../shared/trim.directive';
import { MatSelectModule } from '@angular/material/select';

type SslModeOption = {
    value: SmtpSslMode,
    name: string;
}

@Component({
    selector: 'app-create-smtp',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatStepperModule,
        MatAutocompleteModule,
        SecureInputDirective,
        TrimDirective,
        MatSelectModule
    ],
    templateUrl: './create-smtp.component.html',
    styleUrl: './create-smtp.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateSmtpComponent {
    private _defaultSslMode: SmtpSslMode = 'sslOnConnect';

    isLoading = signal(false);
    couldNotConnect = signal(false);
    connectionError = signal('');
    
    wasSubmitted = signal(false);
    awsRegions = AwsRegions.SES();

    name = new FormControl('', [Validators.required]);
    emailFrom = new FormControl('', [Validators.required]);
    smtpHostname = new FormControl('', [Validators.required]);
    smtpPort = new FormControl('', [Validators.required, Validators.min(0), Validators.max(65535)]);
    sslMode = new FormControl(this._defaultSslMode, [Validators.required])
    username = new FormControl('', [Validators.required]);
    password = new FormControl('', [Validators.required]);

    configFormGroup: FormGroup;
    sslModes: SslModeOption[] = [
        { value: 'none', name: 'None'},
        { value: 'auto', name: 'Auto'},
        { value: 'sslOnConnect', name: "SSL On Connect"},
        { value: 'startTls', name: "Start TLS"},
        { value: 'startTlsWhenAvailable', name: "Start TLS When Available"}
    ]

    constructor(
        public auth: AuthService,
        private _dialog: MatDialog,
        private _emailProvidersApi: EmailProvidersApi,
        public dialogRef: MatDialogRef<CreateSmtpComponent>) {    
            
        this.configFormGroup = new FormGroup({
            name: this.name,
            emailFrom: this.emailFrom,
            smtpHostname: this.smtpHostname,
            smtpPort: this.smtpPort,
            sslMode: this.sslMode,
            username: this.username,
            password: this.password
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
            const result = await this._emailProvidersApi.createSmtpEmailProvider({
                emailFrom: this.emailFrom.value!,
                name: this.name.value!,
                hostname: this.smtpHostname.value!,
                port: Number.parseInt(this.smtpPort.value!),
                sslMode: this.getSslMode(this.sslMode.value!),
                username: this.username.value!,
                password: this.password.value!
            });
           
            const emailProvider: AppEmailProvider = {
                emailFrom: signal(this.emailFrom.value!),
                externalId: signal(result.externalId),
                isActive: signal(false),
                isConfirmed: signal(false),
                isHighlighted: signal(false),
                isNameEditing: signal(false),
                name: signal(this.name.value!),
                type: signal('smtp')
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
                this.connectionError.set(e.error.innerError);
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

    private getSslMode(value: string): SmtpSslMode {
        return value as SmtpSslMode;
    }
}
