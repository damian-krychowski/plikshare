import { Component, OnInit, SimpleChanges, OnChanges, input, output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatSelectModule } from "@angular/material/select";
import { FormsModule } from "@angular/forms";

// Maximum number of workspaces allowed
const MAX_WORKSPACE_VALUE = 1000;

export type WorkspaceMaxNumberChangedEvent = {
    maxNumber: number | null;
}

@Component({
    selector: 'app-max-workspace-number-config',
    standalone: true,
    imports: [
        MatButtonModule,
        MatSelectModule,
        FormsModule,
    ],
    templateUrl: './max-workspace-number-config.component.html',
    styleUrl: './max-workspace-number-config.component.scss'
})
export class MaxWorkspaceNumberConfigComponent implements OnInit, OnChanges {
    maxNumber = input.required<number | null>();
    configChanged = output<WorkspaceMaxNumberChangedEvent>();
    
    limitOptions = ['Limited number', 'Unlimited number'];
    selectedLimit = 'Unlimited number';
    
    maxNumberValue: string = "0";
    hasError: boolean = false;
    errorMessage: string = '';

    formValid: boolean = true;

    ngOnInit() {
        this.initializeFromMaxNumber();
    }

    ngOnChanges(changes: SimpleChanges) {
        if (changes['maxNumber']) {
            this.initializeFromMaxNumber();
        }
    }

    private initializeFromMaxNumber() {
        const maxNumber = this.maxNumber();

        if (maxNumber === null) {
            this.selectedLimit = 'Unlimited number';
            this.maxNumberValue = "0";
        } else {
            this.selectedLimit = 'Limited number';
            this.maxNumberValue = maxNumber.toString();
        }
        this.validateMaxNumber();
    }

    onLimitChange() {
        this.validateMaxNumber();
        this.emitChanges();
    }

    onNumberValueChange(event: Event) {
        const value = (event.target as HTMLInputElement).value;
        this.maxNumberValue = value;
        this.validateMaxNumber();
        this.emitChanges();
    }

    validateMaxNumber(): boolean {
        this.hasError = false;
        this.errorMessage = '';
        
        if (this.selectedLimit === 'Unlimited number') {
            this.formValid = true;
            return true;
        }
        
        // Required validation
        if (this.maxNumberValue == null || this.maxNumberValue == "") {
            this.hasError = true;
            this.errorMessage = 'Maximum number is required';
            this.formValid = false;
            return false;
        }
        
        // Integer validation
        const numberValue = parseInt(this.maxNumberValue);
        if (isNaN(numberValue) || numberValue.toString() !== this.maxNumberValue) {
            this.hasError = true;
            this.errorMessage = 'Please enter a valid integer';
            this.formValid = false;
            return false;
        }
        
        // Min validation
        if (numberValue < 0) {
            this.hasError = true;
            this.errorMessage = 'Maximum number cannot be negative';
            this.formValid = false;
            return false;
        }
        
        // Max validation
        if (numberValue > MAX_WORKSPACE_VALUE) {
            this.hasError = true;
            this.errorMessage = `Maximum number cannot exceed ${MAX_WORKSPACE_VALUE}`;
            this.formValid = false;
            return false;
        }
        
        this.formValid = true;
        return true;
    }

    private emitChanges() {
        if (!this.formValid) {
            return;
        }
        
        const maxNumber = this.selectedLimit === 'Limited number'
            ? parseInt(this.maxNumberValue)
            : null;
            
        this.configChanged.emit({ maxNumber });
    }
}