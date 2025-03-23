import { Component, computed, model, OnInit, Signal, signal, WritableSignal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatRadioChange, MatRadioModule } from "@angular/material/radio";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Router } from "@angular/router";
import { AuthService } from "../../services/auth.service";
import { ApplicationSingUp, GeneralSettingsApi, SignUpCheckboxDto } from "../../services/general-settings.api";
import { Debouncer } from "../../services/debouncer";
import { DocumentUploadApi, DocumentUploadComponent } from "./document-upload/document-upload.component";
import { OptimisticOperation } from "../../services/optimistic-operation";
import { EntryPageService } from "../../services/entry-page.service";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { FormsModule } from "@angular/forms";
import { ActionButtonComponent } from "../../shared/buttons/action-btn/action-btn.component";
import { insertItem, pushItems, removeItem, toggle } from "../../shared/signal-utils";
import { MatSlideToggle } from "@angular/material/slide-toggle";
import { ConfirmOperationDirective } from "../../shared/operation-confirm/confirm-operation.directive";
import { MatCheckboxModule } from "@angular/material/checkbox";

type SignUpCheckbox = {
    id: WritableSignal<number | null>;
    text: WritableSignal<string>;
    savedText: WritableSignal<string | null>;

    isRequired: WritableSignal<boolean>;
    savedIsRequired: WritableSignal<boolean>;
    
    isSaving: WritableSignal<boolean>;
    isChanged: Signal<boolean>;
}

@Component({
    selector: 'app-general-settings',
    imports: [
        FormsModule,
        MatButtonModule,
        MatCheckboxModule,
        MatSlideToggle,
        MatTooltipModule,
        MatFormFieldModule,
        MatInputModule,
        MatRadioModule,
        DocumentUploadComponent,
        ActionButtonComponent,
        ConfirmOperationDirective
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

    signUpCheckboxes = signal<SignUpCheckbox[]>([]);

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
        this.signUpCheckboxes.set(result
            .signUpCheckboxes
            .map(chk => this.mapSignUpCheckboxDto(chk))
        );
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

    public onAddSignUpCheckobox(){
        const textSignal = signal('');
        const savedTextSignal = signal(null);
        const isRequiredSignal = signal(true);
        const savedIsRequiredSignal = signal(true);

        pushItems(this.signUpCheckboxes, { 
            id: signal(null), 
            text: textSignal,
            savedText: savedTextSignal,             
            isRequired: isRequiredSignal,
            savedIsRequired: savedIsRequiredSignal,
            isSaving: signal(false),
            isChanged: computed(() => textSignal() !== savedTextSignal() || isRequiredSignal() !== savedIsRequiredSignal())
        });
    }

    private mapSignUpCheckboxDto(dto: SignUpCheckboxDto): SignUpCheckbox {
        const textSignal = signal(dto.text);
        const savedTextSignal = signal(dto.text);
        const isRequiredSignal = signal(dto.isRequired);
        const savedIsRequiredSignal = signal(dto.isRequired);

        return { 
            id: signal(dto.id), 
            text: textSignal,
            savedText: savedTextSignal,             
            isRequired: isRequiredSignal,
            savedIsRequired: savedIsRequiredSignal,
            isSaving: signal(false),
            isChanged: computed(() => textSignal() !== savedTextSignal() || isRequiredSignal() !== savedIsRequiredSignal())
        };
    }

    public changeSignUpCheckboxIsRequired(checkbox: SignUpCheckbox){
        toggle(checkbox.isRequired);
    }

    public async saveSignUpCheckbox(signUpCheckbox: SignUpCheckbox) {
        signUpCheckbox.isSaving.set(true);

        try {
            const isRequired = signUpCheckbox.isRequired();
            const text = signUpCheckbox.text();

            const response = await this._settingsApi.createOrUpdateSignUpCheckbox({
                id: signUpCheckbox.id(),
                isRequired: isRequired,
                text: text
            });

            signUpCheckbox.id.set(response.newId);
            signUpCheckbox.savedIsRequired.set(isRequired);
            signUpCheckbox.savedText.set(text);

            await this._entryPage.reload();
        } catch (error) {
            console.error(error);
        } finally {
            signUpCheckbox.isSaving.set(false);
        }
    }

    public async deleteSignUpCheckbox(signUpCheckbox: SignUpCheckbox) {
        const id = signUpCheckbox.id();

        if(!id) {
            removeItem(this.signUpCheckboxes, signUpCheckbox);
            return;
        }

        signUpCheckbox.isSaving.set(true);
        let index = 0;

        try {
            await this._settingsApi.deleteSignUpCheckobx(id);

            const result = removeItem(this.signUpCheckboxes, signUpCheckbox);
            index = result.index;

            await this._entryPage.reload();
        } catch (error) {
            console.error(error);
            insertItem(this.signUpCheckboxes, signUpCheckbox, index)
        } finally {
            signUpCheckbox.isSaving.set(false);
        }
    }
}