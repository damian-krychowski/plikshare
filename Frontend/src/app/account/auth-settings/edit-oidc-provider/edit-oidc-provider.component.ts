import { Component, Inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AuthProvidersApi } from '../../../services/auth-providers.api';
import { AppAuthProvider } from '../../../shared/auth-provider-item/auth-provider-item.component';
import { SecureInputDirective } from '../../../shared/secure-input.directive';
import { TrimDirective } from '../../../shared/trim.directive';

export interface EditOidcProviderDialogData {
    authProvider: AppAuthProvider;
}

@Component({
    selector: 'app-edit-oidc-provider',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective,
        TrimDirective
    ],
    templateUrl: './edit-oidc-provider.component.html',
    styleUrl: './edit-oidc-provider.component.scss'
})
export class EditOidcProviderComponent {
    isLoading = signal(false);
    wasSubmitted = signal(false);

    testFailed = signal(false);
    testError = signal('');

    redirectUri = `${window.location.origin}/api/auth/sso/callback`;

    name = new FormControl('', [Validators.required]);
    clientId = new FormControl('', [Validators.required]);
    clientSecret = new FormControl('', [Validators.required]);
    issuerUrl = new FormControl('', [Validators.required]);

    configFormGroup: FormGroup;

    private _authProvider: AppAuthProvider;

    constructor(
        private _authProvidersApi: AuthProvidersApi,
        public dialogRef: MatDialogRef<EditOidcProviderComponent>,
        @Inject(MAT_DIALOG_DATA) data: EditOidcProviderDialogData) {

        this._authProvider = data.authProvider;

        this.name.setValue(this._authProvider.name());
        this.clientId.setValue(this._authProvider.clientId());
        this.issuerUrl.setValue(this._authProvider.issuerUrl());

        this.configFormGroup = new FormGroup({
            name: this.name,
            clientId: this.clientId,
            clientSecret: this.clientSecret,
            issuerUrl: this.issuerUrl
        });
    }

    cancel() {
        this.dialogRef.close();
    }

    async onSubmit() {
        this.wasSubmitted.set(true);

        if (!this.configFormGroup.valid) {
            return;
        }

        this.isLoading.set(true);
        this.testFailed.set(false);
        this.testError.set('');

        try {
            // Step 1: Test configuration
            const testResult = await this._authProvidersApi.testConfiguration({
                issuerUrl: this.issuerUrl.value!,
                clientId: this.clientId.value!,
                clientSecret: this.clientSecret.value!
            });

            if (testResult.code !== 'ok') {
                this.testFailed.set(true);
                this.testError.set(testResult.details);
                return;
            }

            // Step 2: Save
            await this._authProvidersApi.updateAuthProvider(
                this._authProvider.externalId(), {
                name: this.name.value!,
                clientId: this.clientId.value!,
                clientSecret: this.clientSecret.value!,
                issuerUrl: this.issuerUrl.value!
            });

            this._authProvider.name.set(this.name.value!);
            this._authProvider.clientId.set(this.clientId.value!);
            this._authProvider.issuerUrl.set(this.issuerUrl.value!);

            this.dialogRef.close(true);
        } catch (e: any) {
            if (e.error?.code === 'auth-provider-name-not-unique') {
                this.name.setErrors({
                    notUnique: true
                });
            } else {
                console.error(e);
            }
        } finally {
            this.isLoading.set(false);
        }
    }
}
