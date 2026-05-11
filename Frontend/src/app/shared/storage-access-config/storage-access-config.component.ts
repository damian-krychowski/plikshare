import { Component, OnInit, OnChanges, SimpleChanges, input, output } from "@angular/core";
import { MatSelectModule } from "@angular/material/select";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { FormsModule } from "@angular/forms";
import { StorageNameItem } from "../../services/storages.api";
import { UserStorageAccessMode } from "../../services/general-settings.api";

export type StorageAccessChangedEvent = {
    mode: UserStorageAccessMode;
    storageExternalIds: string[];
};

const MODE_OPTIONS: { value: UserStorageAccessMode; label: string }[] = [
    { value: 'all', label: 'All storages' },
    { value: 'allow-only', label: 'Only selected storages' },
    { value: 'allow-all-except', label: 'All except selected storages' }
];

@Component({
    selector: 'app-storage-access-config',
    standalone: true,
    imports: [
        FormsModule,
        MatSelectModule,
        MatCheckboxModule
    ],
    templateUrl: './storage-access-config.component.html',
    styleUrl: './storage-access-config.component.scss'
})
export class StorageAccessConfigComponent implements OnInit, OnChanges {
    mode = input.required<UserStorageAccessMode>();
    storageExternalIds = input.required<string[]>();
    availableStorages = input.required<StorageNameItem[]>();

    configChanged = output<StorageAccessChangedEvent>();

    selectedMode: UserStorageAccessMode = 'all';
    selectedIds = new Set<string>();

    modeOptions = MODE_OPTIONS;

    ngOnInit() {
        this.syncFromInputs();
    }

    ngOnChanges(changes: SimpleChanges) {
        if (changes['mode'] || changes['storageExternalIds']) {
            this.syncFromInputs();
        }
    }

    private syncFromInputs() {
        this.selectedMode = this.mode();
        this.selectedIds = new Set<string>(this.storageExternalIds());
    }

    onModeChange() {
        if (this.selectedMode === 'all') {
            this.selectedIds.clear();
        }
        this.emitChanges();
    }

    onStorageToggle(externalId: string, checked: boolean) {
        if (checked) {
            this.selectedIds.add(externalId);
        } else {
            this.selectedIds.delete(externalId);
        }
        this.emitChanges();
    }

    isSelected(externalId: string): boolean {
        return this.selectedIds.has(externalId);
    }

    private emitChanges() {
        this.configChanged.emit({
            mode: this.selectedMode,
            storageExternalIds: Array.from(this.selectedIds)
        });
    }
}
