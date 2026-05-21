import { Component, OnInit, OnChanges, SimpleChanges, input, output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatSelectModule } from "@angular/material/select";
import { FormsModule } from "@angular/forms";
import { TrashPolicyDto } from "../../services/workspaces.api";

export type TrashPolicyConfigChangedEvent = {
    trashPolicy: TrashPolicyDto;
};

// Mirrors PlikShare.Trash.TrashPolicy on the backend — retention is always stored in days.
const MIN_RETENTION_DAYS = 1;
const MAX_RETENTION_DAYS = 3650;

type TrashMode = 'disabled' | 'auto-delete' | 'forever';
type RetentionUnit = 'days' | 'weeks' | 'months';

// How many days a single unit represents — used to convert the UI value to/from the
// day-based value the backend stores.
const DAYS_PER_UNIT: Record<RetentionUnit, number> = {
    days: 1,
    weeks: 7,
    months: 30
};

@Component({
    selector: 'app-trash-policy-config',
    standalone: true,
    imports: [
        MatButtonModule,
        MatSelectModule,
        FormsModule
    ],
    templateUrl: './trash-policy-config.component.html',
    styleUrl: './trash-policy-config.component.scss'
})
export class TrashPolicyConfigComponent implements OnInit, OnChanges {
    trashPolicy = input.required<TrashPolicyDto>();
    configChanged = output<TrashPolicyConfigChangedEvent>();

    modeOptions: { value: TrashMode, label: string }[] = [
        { value: 'disabled', label: 'Trash disabled' },
        { value: 'auto-delete', label: 'Auto-delete after' },
        { value: 'forever', label: 'Keep in trash forever' }
    ];
    selectedMode: TrashMode = 'disabled';

    unitOptions: { value: RetentionUnit, label: string }[] = [
        { value: 'days', label: 'Days' },
        { value: 'weeks', label: 'Weeks' },
        { value: 'months', label: 'Months' }
    ];

    retentionValue: string = "30";
    retentionUnit: RetentionUnit = 'days';

    hasError: boolean = false;
    errorMessage: string = '';

    formValid: boolean = true;

    ngOnInit() {
        this.initializeFromTrashPolicy();
    }

    ngOnChanges(changes: SimpleChanges) {
        if (changes['trashPolicy']) {
            this.initializeFromTrashPolicy();
        }
    }

    private initializeFromTrashPolicy() {
        const policy = this.trashPolicy();

        if (!policy.enabled) {
            this.selectedMode = 'disabled';
            this.retentionValue = "30";
            this.retentionUnit = 'days';
        } else if (policy.retentionDays === null) {
            this.selectedMode = 'forever';
            this.retentionValue = "30";
            this.retentionUnit = 'days';
        } else {
            this.selectedMode = 'auto-delete';
            const { value, unit } = this.splitRetentionDays(policy.retentionDays);
            this.retentionValue = value.toString();
            this.retentionUnit = unit;
        }

        this.validateRetention();
    }

    // Picks the coarsest unit that divides the day count evenly, so e.g. 30 days shows
    // as "1 month" and 14 days as "2 weeks" rather than always falling back to days.
    private splitRetentionDays(days: number): { value: number, unit: RetentionUnit } {
        if (days % DAYS_PER_UNIT.months === 0)
            return { value: days / DAYS_PER_UNIT.months, unit: 'months' };

        if (days % DAYS_PER_UNIT.weeks === 0)
            return { value: days / DAYS_PER_UNIT.weeks, unit: 'weeks' };

        return { value: days, unit: 'days' };
    }

    onModeChange() {
        this.validateRetention();
        this.emitChanges();
    }

    onUnitChange() {
        this.validateRetention();
        this.emitChanges();
    }

    onRetentionValueChange(event: Event) {
        this.retentionValue = (event.target as HTMLInputElement).value;
        this.validateRetention();
        this.emitChanges();
    }

    validateRetention(): boolean {
        this.hasError = false;
        this.errorMessage = '';

        // The value only matters when auto-delete is the selected mode.
        if (this.selectedMode !== 'auto-delete') {
            this.formValid = true;
            return true;
        }

        const amount = parseInt(this.retentionValue);

        if (this.retentionValue == null || this.retentionValue === "" || isNaN(amount)) {
            this.hasError = true;
            this.errorMessage = 'Value is required';
            this.formValid = false;
            return false;
        }

        const days = amount * DAYS_PER_UNIT[this.retentionUnit];

        if (days < MIN_RETENTION_DAYS || days > MAX_RETENTION_DAYS) {
            this.hasError = true;
            this.errorMessage = `Must be between ${MIN_RETENTION_DAYS} and ${MAX_RETENTION_DAYS} days`;
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

        let trashPolicy: TrashPolicyDto;

        if (this.selectedMode === 'disabled') {
            trashPolicy = { enabled: false, retentionDays: null };
        } else if (this.selectedMode === 'forever') {
            trashPolicy = { enabled: true, retentionDays: null };
        } else {
            const days = parseInt(this.retentionValue) * DAYS_PER_UNIT[this.retentionUnit];
            trashPolicy = { enabled: true, retentionDays: days };
        }

        this.configChanged.emit({ trashPolicy });
    }
}
