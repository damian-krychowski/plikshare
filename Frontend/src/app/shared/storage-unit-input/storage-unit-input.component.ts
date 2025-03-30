import { Component, forwardRef, input, output } from "@angular/core";
import { ControlValueAccessor, FormBuilder, FormsModule, NG_VALUE_ACCESSOR, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatSelectModule } from "@angular/material/select";

@Component({
    selector: 'app-storage-unit-input',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatSelectModule,
        ReactiveFormsModule,
    ],
    providers: [
        {
            provide: NG_VALUE_ACCESSOR,
            useExisting: forwardRef(() => StorageUnitInputComponent),
            multi: true
        }
    ],
    templateUrl: './storage-unit-input.component.html',
    styleUrl: './storage-unit-input.component.scss'
})
export class StorageUnitInputComponent implements ControlValueAccessor {
    wasSubmitted = input(false);
    
    // Predefined list of storage units
    readonly storageUnits: string[] = ['MB', 'GB', 'TB'];

    storageUnitControl = this.fb.control('', [Validators.required]);
    changed = output();

    constructor(private fb: FormBuilder) { }

    // ControlValueAccessor methods
    writeValue(value: any): void {
        if (value !== undefined) {
            this.storageUnitControl.setValue(value);
        }
    }

    registerOnChange(fn: any): void {
        this.storageUnitControl.valueChanges.subscribe(fn);
    }

    registerOnTouched(fn: any): void {
        this.onTouched = fn;
    }

    setDisabledState?(isDisabled: boolean): void {
        isDisabled ? this.storageUnitControl.disable() : this.storageUnitControl.enable();
    }

    private onTouched: () => void = () => { };
}