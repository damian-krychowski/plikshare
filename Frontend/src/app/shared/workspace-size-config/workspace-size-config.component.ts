import { Component, OnInit, SimpleChanges, OnChanges, input, output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatSelectModule } from "@angular/material/select";
import { FormsModule } from "@angular/forms";
import { StorageSizeUtils } from "../../shared/storage-size.pipe";

// Same constants as in original component
const MAX_TB_VALUE = 8388607;
const MAX_GB_VALUE = 8589934591;
const MAX_MB_VALUE = 8796093022207;

export type WorkspaceMaxSizeInBytesChangedEvent = {
    maxSizeInBytes: number | null;
}

@Component({
    selector: 'app-workspace-size-config',
    standalone: true,
    imports: [
        MatButtonModule,
        MatSelectModule,
        FormsModule
    ],
    templateUrl: './workspace-size-config.component.html',
    styleUrl: './workspace-size-config.component.scss'
})
export class WorkspaceSizeConfigComponent implements OnInit, OnChanges {
    maxSizeInBytes = input.required<number | null>();
    configChanged = output<WorkspaceMaxSizeInBytesChangedEvent>();
    
    limitOptions = ['Limited size', 'Unlimited size'];
    selectedLimit = 'Unlimited size';

    sizeUnits = ['MB', 'GB', 'TB'];
    selectedUnit: 'MB' | 'GB' | 'TB' = 'GB';
    
    maxSizeValue: string = "0";
    hasError: boolean = false;
    errorMessage: string = '';

    // Track form validity
    formValid: boolean = true;

    ngOnInit() {
        this.initializeFromMaxSizeInBytes();
    }

    ngOnChanges(changes: SimpleChanges) {
        if (changes['maxSizeInBytes']) {
            this.initializeFromMaxSizeInBytes();
        }
    }

    private initializeFromMaxSizeInBytes() {
        const maxSizeInBytes = this.maxSizeInBytes();

        if (maxSizeInBytes === null) {
            this.selectedLimit = 'Unlimited size';
            this.maxSizeValue = "0";
            this.selectedUnit = 'GB';
        } else {
            this.selectedLimit = 'Limited size';
            const { value, unit } = StorageSizeUtils.convertToFullUnit(maxSizeInBytes);
            this.maxSizeValue = value.toString();
            this.selectedUnit = unit;
        }
        this.validateMaxSize();
    }

    onLimitChange() {
        this.validateMaxSize();
        this.emitChanges();
    }

    onSizeValueChange(event: Event) {
        const value = (event.target as HTMLInputElement).value;
        this.maxSizeValue = value;
        this.validateMaxSize();
        this.emitChanges();
    }

    onUnitChange() {
        this.validateMaxSize();
        this.emitChanges();
    }

    validateMaxSize(): boolean {
        this.hasError = false;
        this.errorMessage = '';
        
        if (this.selectedLimit === 'Unlimited size') {
            this.formValid = true;
            return true;
        }
        
        // Required validation
        if (this.maxSizeValue == null || this.maxSizeValue == "") {
            this.hasError = true;
            this.errorMessage = 'Maximum size is required';
            this.formValid = false;
            return false;
        }
        
        // Min validation
        if (parseInt(this.maxSizeValue) < 0) {
            this.hasError = true;
            this.errorMessage = 'Maximum size must be at least 0';
            this.formValid = false;
            return false;
        }
        
        // Max validation
        const maxAllowedValue = this.getCurrentMaxValue();
        if (parseInt(this.maxSizeValue) > maxAllowedValue) {
            this.hasError = true;
            this.errorMessage = `Maximum size cannot exceed ${maxAllowedValue} ${this.selectedUnit}`;
            this.formValid = false;
            return false;
        }
        
        this.formValid = true;
        return true;
    }

    getCurrentMaxValue(): number {
        switch (this.selectedUnit) {
            case 'TB': return MAX_TB_VALUE;
            case 'GB': return MAX_GB_VALUE;
            case 'MB': return MAX_MB_VALUE;
            default: return MAX_GB_VALUE;
        }
    }

    private emitChanges() {
        if (!this.formValid) {
            return;
        }
        
        const maxSizeInBytes = this.selectedLimit === 'Limited size'
            ? StorageSizeUtils.convertToBytes({value: parseInt(this.maxSizeValue), unit: this.selectedUnit})
            : null;
            
        this.configChanged.emit({ maxSizeInBytes });
    }
}