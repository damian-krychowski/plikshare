import { Component, OnDestroy, OnInit, ViewEncapsulation, WritableSignal, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatRadioModule } from '@angular/material/radio';
import { Router } from '@angular/router';
import { AwsRegions } from '../../../../../services/aws-regions';
import { RegionInputComponent } from '../../../../../shared/region-input/region-input.component';
import { SecureInputDirective } from '../../../../../shared/secure-input.directive';
import { IntegrationsApi } from '../../../../../services/integrations.api';
import { TextractApi } from '../../../../../services/textract.api';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { GetAwsS3StorageItem, GetStorageItem, StoragesApi } from '../../../../../services/storages.api';
import { MatDialog } from '@angular/material/dialog';
import { StoragePickerComponent } from '../../../../../shared/storage-picker/storage-picker.component';
import { AppStorage, StorageItemComponent } from '../../../../../shared/storage-item/storage-item.component';

@Component({
    selector: 'app-create-aws-textract',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        RegionInputComponent,
        SecureInputDirective,
        MatRadioModule,
        MatProgressSpinnerModule,
        StorageItemComponent
    ],
    templateUrl: './create-aws-textract.component.html',
    styleUrl: './create-aws-textract.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateAwsTextractComponent implements OnInit, OnDestroy {
    testImageUrl: WritableSignal<string | null> = signal(null);

    isLoading = signal(false);
    isConfigurationTested = signal(false);

    awsRegions = AwsRegions.S3();

    name = new FormControl('', [Validators.required]);
    accessKey = new FormControl('', [Validators.required]);    
    secretAccessKey = new FormControl('', [Validators.required]);    
    region = new FormControl('', [Validators.required]);    

    formGroup: FormGroup;
    wasSubmitted = signal(false);

    textractAccessWasDenied = signal<string | null>(null);
    textractInvalidSecretAccessKey = signal<string | null>(null);
    textractUnrecognizedAccessKey = signal<string | null>(null);    
    s3AccessWasDenied = signal<string | null>(null);

    detectedLines = signal<string[]>([]);
    
    private _storages: GetStorageItem[] = [];
    selectedStorage: WritableSignal<AppStorage | null> = signal(null);
      

    constructor(
        private _storagesApi: StoragesApi,
        private _integrationsApi: IntegrationsApi,
        private _textractApi: TextractApi,
        private _router: Router,
        private _dialog: MatDialog) {    
            
        this.formGroup = new FormGroup({
            name: this.name,
            accessKey: this.accessKey,
            secretAccessKey: this.secretAccessKey,
            region: this.region,
        });
    }

    async ngOnInit() {        
        await Promise.all([
            this.loadTestImage(), 
            this.loadStorages()
        ]);
    }

    ngOnDestroy(): void {
        const testImage = this.testImageUrl();

        if(testImage) {
            URL.revokeObjectURL(testImage);
        }
    }

    private async loadTestImage() {
        try {
            const imageBlob = await this._textractApi.getTestImage();
            const objectUrl = URL.createObjectURL(imageBlob);
            this.testImageUrl.set(objectUrl);
        } catch (error) {
            console.error('Failed to load test image:', error);
        }
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
                $type: 'aws-textract',
                name: this.name.value!,
                accessKey: this.accessKey.value!,
                secretAccessKey: this.secretAccessKey.value!,
                region: this.region.value!,
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

            const result = await this._textractApi.testConfiguration({
                accessKey: this.accessKey.value!,
                secretAccessKey: this.secretAccessKey.value!,
                region: this.region.value!,
                storageExternalId: storage.externalId
            });

            this.detectedLines.set(result.detectedLines);
            this.isConfigurationTested.set(true);    
            
            this.textractAccessWasDenied.set(null);      
            this.s3AccessWasDenied.set(null);         
            this.textractInvalidSecretAccessKey.set(null);         
            this.textractUnrecognizedAccessKey.set(null);         
        } catch (e: any) {
            if(e.error.code === 'aws-textract-access-denied') {
                this.textractAccessWasDenied.set(e.error.message);
            } else if(e.error.code === 'aws-s3-access-denied') {
                this.s3AccessWasDenied.set(e.error.message);
            } else if(e.error.code === 'aws-textract-invalid-secret-access-key') {
                this.textractInvalidSecretAccessKey.set(e.error.message);
            } else if(e.error.code === 'aws-textract-unrecognized-access-key') {
                this.textractUnrecognizedAccessKey.set(e.error.message);
            }else {
                console.error(e);
            }
            
        } finally {
            this.isLoading.set(false);
        }
    }

    pickStorage() {
        if(!this.region.value) {
            this.region.markAsTouched();
            return;
        }

        const awsS3Storages = this
            ._storages
            .filter(s => s.$type == 'aws-s3' && s.region === this.region.value && s.encryptionType == 'none')
            .map(s => s as GetAwsS3StorageItem)
            .map((s: GetAwsS3StorageItem) => {
                const storage: AppStorage = {
                    externalId: s.externalId,
                    name: signal(s.name),
                    type: 'aws-s3',
                    details: `Region: ${s.region}`,
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
                noStoragesMessage: `No Aws S3 storage in '${this.region.value}' region was found.`
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
