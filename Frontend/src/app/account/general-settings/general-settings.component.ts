import { Component, model, OnInit, signal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatRadioChange, MatRadioModule } from "@angular/material/radio";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Router } from "@angular/router";
import { AuthService } from "../../services/auth.service";
import { ApplicationSingUp, GeneralSettingsApi } from "../../services/general-settings.api";
import { Debouncer } from "../../services/debouncer";
import { DocumentUploadApi, DocumentUploadComponent } from "./document-upload/document-upload.component";
import { OptimisticOperation } from "../../services/optimistic-operation";
import { EntryPageService } from "../../services/entry-page.service";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { FormsModule } from "@angular/forms";

@Component({
    selector: 'app-general-settings',
    imports: [
        FormsModule,
        MatButtonModule,
        MatTooltipModule,
        MatFormFieldModule,
        MatInputModule,
        MatRadioModule,
        DocumentUploadComponent,
    ],
    templateUrl: './general-settings.component.html',
    styleUrl: './general-settings.component.scss'
})
export class GeneralSettingsComponent implements OnInit {       
    isLoading = signal(false);
    wasLoaded = signal(false);

    applicationSignUp = signal<ApplicationSingUp>('only-invited-users');

    termsOfServiceFileName = signal<string | null>(null);
    privacyPolicyFileName = signal<string | null>(null);

    termsOfServiceApi: DocumentUploadApi = {
        deleteFile: () => this._settingsApi.deleteTermsOfService(),
        uploadFile: (file: File) => this._settingsApi.uploadTermsOfService(file)
    };

    privacyPolicyApi: DocumentUploadApi = {
        deleteFile: () => this._settingsApi.deletePrivacyPolicy(),
        uploadFile: (file: File) => this._settingsApi.uploadPrivacyPolicy(file)
    };

    applicationName = model<string|null>(null);

    constructor(
        private _entryPage: EntryPageService,
        public auth: AuthService,
        private _router: Router,
        private _settingsApi: GeneralSettingsApi,
    ) {
    }

    async ngOnInit() {
        this.isLoading.set(true);

        try {
            const loadings = [
                this.loadAppSettings(),
            ];

            await Promise.all(loadings);
        } catch (error) {
            console.error(error);    
        } finally {
            this.isLoading.set(false);
            this.wasLoaded.set(true);
        }
    }

    private async loadAppSettings() {
        const result = await this._settingsApi.getAppSettings();

        this.applicationSignUp.set(result.applicationSignUp);
        this.termsOfServiceFileName.set(result.termsOfService);
        this.privacyPolicyFileName.set(result.privacyPolicy);
        this.applicationName.set(result.applicationName);
    }

    goToAccount() {
        this._router.navigate(['account']);
    }

    private _signUpDebouncer = new Debouncer(300);
    async onApplicationSignUpChange(value: MatRadioChange) {
        this.applicationSignUp.set(value.value);

        this._signUpDebouncer.debounce(
            async () => {
                await this._settingsApi.setApplicationSingUp({
                    value: this.applicationSignUp()
                });
                
                await this._entryPage.reload();                
            }
        );
    }

    private _nameDebouncer = new Debouncer(300);
    async onApplicationNameChange() {
        this._nameDebouncer.debounce(
            async () => {
                await this._settingsApi.setApplicationName({
                    value: this.applicationName()
                });              
            }
        );
    }

    async onTermsOfServiceUploaded(upload: {fileName: string }) {
        this.termsOfServiceFileName.set(upload.fileName);
        await this._entryPage.reload();
    }

    async onTermsOfServiceDeleted(operation: OptimisticOperation){
        const originalName = this.termsOfServiceFileName();
        this.termsOfServiceFileName.set(null);

        const result = await operation.wait();

        if(result.type === 'failure'){
            this.termsOfServiceFileName.set(originalName);
        } else {
            await this._entryPage.reload();
        }
    }

    async onPrivacyPolicyUploaded(upload: {fileName: string }) {
        this.privacyPolicyFileName.set(upload.fileName);
        await this._entryPage.reload();
    }

    async onPrivacyPolicyDeleted(operation: OptimisticOperation){
        const originalName = this.privacyPolicyFileName();
        this.privacyPolicyFileName.set(null);

        const result = await operation.wait();

        if(result.type === 'failure'){
            this.privacyPolicyFileName.set(originalName);
        } else {
            await this._entryPage.reload();
        }   
    }
}