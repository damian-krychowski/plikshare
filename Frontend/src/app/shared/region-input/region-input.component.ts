import { Component, computed, forwardRef, input, OnDestroy, OnInit, signal, WritableSignal } from "@angular/core";
import { ControlValueAccessor, FormBuilder, FormsModule, NG_VALUE_ACCESSOR, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatAutocompleteModule } from "@angular/material/autocomplete";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatStepperModule } from "@angular/material/stepper";
import { Subscription } from "rxjs";

@Component({
    selector: 'app-region-input',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatStepperModule,
        MatAutocompleteModule
    ],
    providers: [
        {
            provide: NG_VALUE_ACCESSOR,
            useExisting: forwardRef(() => RegionInputComponent),
            multi: true
        }
    ],
    templateUrl: './region-input.component.html',
    styleUrl: './region-input.component.scss'
})
export class RegionInputComponent implements OnInit, OnDestroy, ControlValueAccessor {
    regions = input.required<string[]>();
    wasSubmitted = input(false);

    regionControl = this.fb.control('', [Validators.required]);

    filteredRegions = computed(() => {
        const filter = this.regionsFilter().toLowerCase();
        return this.regions().filter(region => region.includes(filter));
    });

    regionsFilter: WritableSignal<string> = signal('');

    private _subscription: Subscription | null = null;

    constructor(private fb: FormBuilder) { }

    ngOnInit() {
        this._subscription = this.regionControl.valueChanges.subscribe({
            next: (value) => this.regionsFilter.set(value ?? '')
        });
    }

    ngOnDestroy(): void {
        this._subscription?.unsubscribe();
    }

    // ControlValueAccessor methods
    writeValue(value: any): void {
        if (value !== undefined) {
            this.regionControl.setValue(value);
        }
    }

    registerOnChange(fn: any): void {
        this.regionControl.valueChanges.subscribe(fn);
    }

    registerOnTouched(fn: any): void {
        this.onTouched = fn;
    }

    setDisabledState?(isDisabled: boolean): void {
        isDisabled ? this.regionControl.disable() : this.regionControl.enable();
    }

    private onTouched: () => void = () => { };
}