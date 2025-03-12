import { Component, OnInit, ViewEncapsulation, WritableSignal, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatRadioModule } from '@angular/material/radio';
import { Router } from '@angular/router';
import { RegionInputComponent } from '../../../../../shared/region-input/region-input.component';
import { SecureInputDirective } from '../../../../../shared/secure-input.directive';
import { IntegrationsApi } from '../../../../../services/integrations.api';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { GetStorageItem, StoragesApi } from '../../../../../services/storages.api';
import { MatDialog } from '@angular/material/dialog';
import { StoragePickerComponent } from '../../../../../shared/storage-picker/storage-picker.component';
import { AppStorage, StorageItemComponent } from '../../../../../shared/storage-item/storage-item.component';
import { ChatGptApi } from '../../../../../services/chat-gpt.api';

@Component({
    selector: 'app-create-chatgpt',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        SecureInputDirective,
        MatRadioModule,
        MatProgressSpinnerModule,
        StorageItemComponent
    ],
    templateUrl: './create-chatgpt.component.html',
    styleUrl: './create-chatgpt.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateChatGptComponent implements OnInit {
    isLoading = signal(false);
    isConfigurationTested = signal(false);

    name = new FormControl('', [Validators.required]);
    apiKey = new FormControl('', [Validators.required]);    

    formGroup: FormGroup;
    wasSubmitted = signal(false);

    invalidApiKey = signal<string | null>(null);

    haiku = signal<string | null>(null);
    
    private _storages: GetStorageItem[] = [];
    selectedStorage: WritableSignal<AppStorage | null> = signal(null);
      

    constructor(
        private _storagesApi: StoragesApi,
        private _integrationsApi: IntegrationsApi,
        private _router: Router,
        private _dialog: MatDialog,
        private _chatGptApi: ChatGptApi) {    
            
        this.formGroup = new FormGroup({
            name: this.name,
            apiKey: this.apiKey,
        });
    }

    async ngOnInit() {        
        await Promise.all([
            this.loadStorages()
        ]);
    }

    private async loadStorages() {
        try {
            const result = await this._storagesApi.getStorages();
            this._storages = result.items;
        } catch (error) {
            console.error('Failed to load test image:', error);
        }
    }

    async onConfigurationSubmit() {
        if(this.isConfigurationTested())
            await this.onCreate();
        else
            await this.testConfiguration();
    }

    private async onCreate() {
        this.wasSubmitted.set(true);
        
        if(!this.formGroup.valid)
            return;

        var storage = this.selectedStorage();

        if(!storage)
            return;

        try {
            this.isLoading.set(true);

            await this._integrationsApi.createIntegration({
                $type: 'openai-chatgpt',
                name: this.name.value!,
                apiKey: this.apiKey.value!,
                storageExternalId: storage.externalId
            });

            this.goToIntegrations();
        } catch (err: any) {
            if (err.error.code === 'integration-name-not-unique') {
                this.name.setErrors({
                    notUnique: true
                });
            } else {
                console.error(err);
            }
        } finally {
            this.isLoading.set(false);
        }
    }

    goToIntegrations() {
        this._router.navigate(['settings/integrations']);
    }

    private async testConfiguration() {
        this.wasSubmitted.set(true);

        if(!this.formGroup.valid)
            return;
        
        var storage = this.selectedStorage();

        if(!storage)
            return;

        try {
            this.isLoading.set(true);

            const result = await this._chatGptApi.testConfiguration({
                apiKey: this.apiKey.value!
            });

            this.haiku.set(result.haiku);
            this.isConfigurationTested.set(true);                    
            this.invalidApiKey.set(null);         
        } catch (e: any) {
            if(e.error.code === 'openai-chatgpt-invalid-api-key') {
                this.invalidApiKey.set(e.error.message);
            } else {
                console.error(e);
            }            
        } finally {
            this.isLoading.set(false);
        }
    }

    pickStorage() {
        const awsS3Storages = this
            ._storages
            .map((s) => {
                const storage: AppStorage = {
                    externalId: s.externalId,
                    name: signal(s.name),
                    type: s.$type,
                    details: '',
                    encryptionType: s.encryptionType,
                    isHighlighted: signal(false),
                    isNameEditing: signal(false),
                    workspacesCount: s.workspacesCount,
                };

                return storage;
            });

        const dialogRef = this._dialog.open(StoragePickerComponent, {
            width: '500px',
            data: {
                storages: awsS3Storages,
                noStoragesMessage: `No storage was found.`
            },
            position: {
                top: '100px'
            }
        });

        dialogRef
            .afterClosed()
            .subscribe(async (storage: AppStorage) => this.selectedStorage.set(storage));  
    }
}
