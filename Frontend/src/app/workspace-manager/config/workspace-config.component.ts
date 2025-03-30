import { Component, computed, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subscription, filter } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { StorageUnitInputComponent } from '../../shared/storage-unit-input/storage-unit-input.component';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { WorkspacesApi } from '../../services/workspaces.api';
import { StorageSizeUtils } from '../../shared/storage-size.pipe';
import { DataStore } from '../../services/data-store.service';
import { WorkspaceContextService } from '../workspace-context.service';


//not to overflow int64
const MAX_TB_VALUE = 8388607;
const MAX_GB_VALUE = 8589934591;
const MAX_MB_VALUE = 8796093022207;

@Component({
    selector: 'app-workspace-config',
    imports: [
        MatFormFieldModule,
        MatInputModule,
        MatCheckboxModule,
        MatSlideToggle,
        ReactiveFormsModule,
        MatButtonModule,
        StorageUnitInputComponent
    ],
    templateUrl: './workspace-config.component.html',
    styleUrl: './workspace-config.component.scss'
})
export class WorkspaceConfigComponent implements OnInit, OnDestroy {
    isLoading = signal(false);
    wasSubmitted = signal(false);
    
    isLimited = new FormControl(false);
    maxSize = new FormControl(0, [Validators.required, Validators.min(0)]);
    unit = new FormControl<'MB' | 'GB' | 'TB'>('GB', [Validators.required]);
    
    formGroup: FormGroup;

    private _currentWorkspaceExternalId: string | null = null;
    private _routerSubscription: Subscription | null = null;

    private _originalMaxSizeInBytes = signal<number | null>(null);
    private _currentMaxSizeInBytes= signal<number | null>(null);

    wasMaxSizeInBytesChanged = computed(() => this._originalMaxSizeInBytes() !== this._currentMaxSizeInBytes());

    constructor(
        private _workspaceContext: WorkspaceContextService,
        private _workspacesApi: WorkspacesApi,
        private _activatedRoute: ActivatedRoute,
        private _router: Router,
        private _dataStore: DataStore) 
    { 
        this.formGroup = new FormGroup({
            isLimited: this.isLimited,
            maxSize: this.maxSize,
            unit: this.unit
        });

        this.isLimited.valueChanges.subscribe(isLimited => {
            if (isLimited) {
                this.updateMaxSizeValidators();
                this.unit.setValidators([Validators.required]);
            } else {
                this.maxSize.clearValidators();
                this.unit.clearValidators();
            }
            this.maxSize.updateValueAndValidity();
            this.unit.updateValueAndValidity();
        });

        this.unit.valueChanges.subscribe(() => {
            if (this.isLimited.value) {
                this.updateMaxSizeValidators();
                this.maxSize.updateValueAndValidity();
            }
        });
    }

    private updateMaxSizeValidators() {
        const currentUnit = this.unit.value;
        let maxValue: number;
        
        switch (currentUnit) {
            case 'TB':
                maxValue = MAX_TB_VALUE;
                break;
            case 'GB':
                maxValue = MAX_GB_VALUE;
                break;
            case 'MB':
                maxValue = MAX_MB_VALUE;
                break;
            default:
                maxValue = MAX_GB_VALUE;
        }
        
        this.maxSize.setValidators([
            Validators.required, 
            Validators.min(0),
            Validators.max(maxValue)
        ]);
    }

    async ngOnInit() {
        this.load();
                
        this._routerSubscription = this._router.events
            .pipe(filter(event => event instanceof NavigationEnd))
            .subscribe(() => this.load());
    }

    private async load() {
        try {
            this.isLoading.set(true);
            
            const workspaceExternalId = this._activatedRoute.parent?.snapshot.params['workspaceExternalId'];

            if (!workspaceExternalId)
                throw new Error('workspaceExternalId is missing');
                
            this._currentWorkspaceExternalId = workspaceExternalId;
            
            const workspace = await this
                ._workspacesApi
                .getWorkspace(workspaceExternalId);

            //we refresh current state of workspace inside the service
            //to have the most recent version there
            this._workspaceContext.workspace.set(workspace);

            this._originalMaxSizeInBytes.set(workspace.maxSizeInBytes);            
            this._currentMaxSizeInBytes.set(workspace.maxSizeInBytes);            
            this.setMaxSizeFormValues(workspace.maxSizeInBytes);
        } catch (error) {
            console.error('Failed to load workspace configuration', error);
        } finally {
            this.isLoading.set(false);
        }
    }

    private setMaxSizeFormValues(maxSizeInBytes: number | null) {
        if(maxSizeInBytes == null) {
            this.isLimited.setValue(false);
            this.maxSize.setValue(0);
            this.unit.setValue('GB');
        } else {
            const {value, unit} = StorageSizeUtils.convertToFullUnit(
                maxSizeInBytes);

            this.isLimited.setValue(true);
            this.maxSize.setValue(value);
            this.unit.setValue(unit);
            
            // Ensure validators are updated after setting values
            this.updateMaxSizeValidators();
        }
    }
    
    async onSaveConfig() {
        this.wasSubmitted.set(true);
        
        if (!this.formGroup.valid || !this._currentWorkspaceExternalId)
            return;
            
        try {
            this.isLoading.set(true);
            
            const maxSizeInBytes = this.getMaxSizeFromFormInputs();

            await this._workspacesApi.updateMaxSize(this._currentWorkspaceExternalId, {
                maxSizeInBytes: maxSizeInBytes
            });

            const workspace = await this
                ._workspacesApi
                .getWorkspace(this._currentWorkspaceExternalId);

            this._dataStore.clearWorkspaceDetails(this._currentWorkspaceExternalId);
            this._workspaceContext.workspace.set(workspace);

            this._currentMaxSizeInBytes.set(maxSizeInBytes);
            this._originalMaxSizeInBytes.set(maxSizeInBytes);
        } catch (error) {
            console.error('Failed to save workspace configuration', error);
        } finally {
            this.isLoading.set(false);
        }
    }

    onFormChanges() {
        this._currentMaxSizeInBytes.set(this.getMaxSizeFromFormInputs());
    }

    private getMaxSizeFromFormInputs() {
        const isLimitedValue = this.isLimited.value ?? false;
        const maxSizeValue = this.maxSize.value ?? 0;
        const unitValue = this.unit.value ?? 'GB';
        
        return isLimitedValue
            ? StorageSizeUtils.convertToBytes({value: maxSizeValue, unit: unitValue})
            : null
    }

    getCurrentMaxValue(): number {
        const currentUnit = this.unit.value;

        switch (currentUnit) {
            case 'TB': return MAX_TB_VALUE;
            case 'GB': return MAX_GB_VALUE;
            case 'MB': return MAX_MB_VALUE;
            default: return MAX_GB_VALUE;
        }
    }

    ngOnDestroy(): void {
        this._routerSubscription?.unsubscribe();
    }
}