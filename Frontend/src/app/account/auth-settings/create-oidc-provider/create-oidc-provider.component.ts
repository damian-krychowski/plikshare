import { Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../../services/auth.service';
import { AuthProvidersApi } from '../../../services/auth-providers.api';
import { AppAuthProvider } from '../../../shared/auth-provider-item/auth-provider-item.component';
import { SecureInputDirective } from '../../../shared/secure-input.directive';
import { TrimDirective } from '../../../shared/trim.directive';
import { OidcProviderPreset, OIDC_PROVIDER_PRESETS } from '../oidc-provider-presets';

export interface CreateOidcProviderDialogData {
    preset: OidcProviderPreset;
}

@Component({
    selector: 'app-create-oidc-provider',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective,
        TrimDirective
    ],
    templateUrl: './create-oidc-provider.component.html',
    styleUrl: './create-oidc-provider.component.scss'
})
export class CreateOidcProviderComponent {
    private _dialogData: CreateOidcProviderDialogData = inject(MAT_DIALOG_DATA);

    isLoading = signal(false);
    wasSubmitted = signal(false);

    testFailed = signal(false);
    testError = signal('');

    redirectUri = `${window.location.origin}/api/auth/sso/callback`;
    preset: OidcProviderPreset;
    instructions: string[];

    name = new FormControl('', [Validators.required]);
    clientId = new FormControl('', [Validators.required]);
    clientSecret = new FormControl('', [Validators.required]);
    issuerUrl = new FormControl('', [Validators.required]);

    configFormGroup: FormGroup;

    constructor(
        public auth: AuthService,
        private _authProvidersApi: AuthProvidersApi,
        public dialogRef: MatDialogRef<CreateOidcProviderComponent>) {

        this.preset = this._dialogData?.preset ?? OIDC_PROVIDER_PRESETS['custom'];
        this.instructions = this.preset.instructions.map(
            i => i.replace(/\{redirectUri\}/g, this.redirectUri)
        );

        this.configFormGroup = new FormGroup({
            name: this.name,
            clientId: this.clientId,
            clientSecret: this.clientSecret,
            issuerUrl: this.issuerUrl
        });

        if (this.preset.name) {
            this.name.setValue(this.preset.name !== 'Custom OIDC' ? this.preset.name : '');
        }

        if (this.preset.issuerUrl) {
            this.issuerUrl.setValue(this.preset.issuerUrl);
        }
    }

    cancel() {
        this.dialogRef.close();
    }

    async onSubmitConfiguration() {
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

            // Step 2: Create provider
            const result = await this._authProvidersApi.createOidcAuthProvider({
                name: this.name.value!,
                clientId: this.clientId.value!,
                clientSecret: this.clientSecret.value!,
                issuerUrl: this.issuerUrl.value!
            });

            const authProvider: AppAuthProvider = {
                externalId: signal(result.externalId),
                name: signal(this.name.value!),
                type: signal('oidc'),
                isActive: signal(false),
                clientId: signal(this.clientId.value!),
                issuerUrl: signal(this.issuerUrl.value!),
                isHighlighted: signal(false),
                isNameEditing: signal(false)
            };

            this.dialogRef.close(authProvider);
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
