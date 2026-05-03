import { Component, ViewEncapsulation, computed, forwardRef, signal } from '@angular/core';
import { AbstractControl, ControlValueAccessor, NG_VALIDATORS, NG_VALUE_ACCESSOR, ReactiveFormsModule, ValidationErrors, Validator } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatRadioChange, MatRadioModule } from '@angular/material/radio';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { AppStorageEncryptionType } from '../../services/storages.api';
import { AuthService } from '../../services/auth.service';
import { SetupEncryptionPasswordComponent } from '../setup-encryption-password/setup-encryption-password.component';

@Component({
    selector: 'app-encryption-type-selector',
    standalone: true,
    imports: [ReactiveFormsModule, MatRadioModule, MatButtonModule],
    templateUrl: './encryption-type-selector.component.html',
    styleUrl: './encryption-type-selector.component.scss',
    encapsulation: ViewEncapsulation.None,
    providers: [
        {
            provide: NG_VALUE_ACCESSOR,
            useExisting: forwardRef(() => EncryptionTypeSelectorComponent),
            multi: true
        },
        {
            provide: NG_VALIDATORS,
            useExisting: forwardRef(() => EncryptionTypeSelectorComponent),
            multi: true
        }
    ]
})
export class EncryptionTypeSelectorComponent implements ControlValueAccessor, Validator {
    selected = signal<AppStorageEncryptionType>('none');
    isDisabled = signal(false);

    needsEncryptionSetup = computed(() =>
        this.selected() === 'full' && !this.auth.isEncryptionConfigured());

    private _onChange: (value: AppStorageEncryptionType) => void = () => {};
    private _onTouched: () => void = () => {};
    private _onValidatorChange: () => void = () => {};

    constructor(
        public auth: AuthService,
        private _dialog: MatDialog) {}

    writeValue(value: AppStorageEncryptionType | null): void {
        this.selected.set(value ?? 'none');
    }

    registerOnChange(fn: (value: AppStorageEncryptionType) => void): void {
        this._onChange = fn;
    }

    registerOnTouched(fn: () => void): void {
        this._onTouched = fn;
    }

    setDisabledState(isDisabled: boolean): void {
        this.isDisabled.set(isDisabled);
    }

    onSelectionChange(event: MatRadioChange) {
        const value = event.value as AppStorageEncryptionType;
        this.selected.set(value);
        this._onChange(value);
        this._onTouched();
        this._onValidatorChange();
    }

    validate(_: AbstractControl): ValidationErrors | null {
        return this.needsEncryptionSetup()
            ? { encryptionPasswordNotSetUp: true }
            : null;
    }

    registerOnValidatorChange(fn: () => void): void {
        this._onValidatorChange = fn;
    }

    async openSetupEncryptionPassword() {
        const ref = this._dialog.open(SetupEncryptionPasswordComponent, {
            width: '500px',
            position: { top: '100px' },
            disableClose: true
        });

        await firstValueFrom(ref.afterClosed());
        // Re-run the validator: after the password is set, the "full" selection
        // becomes valid without the user having to interact with the radio again.
        this._onValidatorChange();
    }
}
