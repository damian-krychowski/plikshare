import { Component, WritableSignal, computed, input, output, signal } from "@angular/core";
import { Router } from "@angular/router";
import { Clipboard, ClipboardModule } from "@angular/cdk/clipboard";
import { MatSnackBar } from "@angular/material/snack-bar";
import { MatTooltipModule } from "@angular/material/tooltip";
import { ActionButtonComponent } from "../../../shared/buttons/action-btn/action-btn.component";
import { ConfirmOperationDirective } from "../../../shared/operation-confirm/confirm-operation.directive";
import { EditableTxtComponent } from "../../../shared/editable-txt/editable-txt.component";
import { QuickShareMode, QuickSharesApi } from "../../../services/quick-shares.api";
import { DataStore } from "../../../services/data-store.service";

export type AppQuickShare = {
    externalId: string;
    workspaceExternalId: string;

    name: WritableSignal<string>;
    createdAt: Date;
    expiresAt: WritableSignal<Date | null>;
    hasPassword: WritableSignal<boolean>;
    maxDownloads: WritableSignal<number | null>;
    downloadsCount: WritableSignal<number>;
    mode: WritableSignal<QuickShareMode>;
    allowIndividualFileDownload: WritableSignal<boolean>;
    lastAccessedAt: WritableSignal<Date | null>;
    slug: WritableSignal<string>;
    hasSecret: boolean;
    url: WritableSignal<string | null>;

    selectedFilesCount: number;
    selectedFoldersCount: number;
    excludedFilesCount: number;
    excludedFoldersCount: number;

    isNameEditing: WritableSignal<boolean>;
}

@Component({
    selector: 'app-quick-share-item',
    imports: [
        ClipboardModule,
        MatTooltipModule,
        ActionButtonComponent,
        ConfirmOperationDirective,
        EditableTxtComponent
    ],
    templateUrl: './quick-share-item.component.html',
    styleUrl: './quick-share-item.component.scss'
})
export class QuickShareItemComponent {
    quickShare = input.required<AppQuickShare>();

    deleted = output<void>();

    name = computed(() => this.quickShare().name());
    url = computed(() => this.quickShare().url());
    mode = computed(() => this.quickShare().mode());
    hasPassword = computed(() => this.quickShare().hasPassword());
    maxDownloads = computed(() => this.quickShare().maxDownloads());
    downloadsCount = computed(() => this.quickShare().downloadsCount());
    expiresAt = computed(() => this.quickShare().expiresAt());
    lastAccessedAt = computed(() => this.quickShare().lastAccessedAt());
    isNameEditing = computed(() => this.quickShare().isNameEditing());

    selectionSummary = computed(() => {
        const qs = this.quickShare();
        const parts: string[] = [];
        if (qs.selectedFoldersCount > 0) parts.push(`${qs.selectedFoldersCount} folder(s)`);
        if (qs.selectedFilesCount > 0) parts.push(`${qs.selectedFilesCount} file(s)`);
        if (qs.excludedFilesCount + qs.excludedFoldersCount > 0) {
            parts.push(`(${qs.excludedFilesCount + qs.excludedFoldersCount} excluded)`);
        }
        return parts.length > 0 ? parts.join(', ') : 'empty';
    });

    areActionsVisible = signal(false);
    copied = signal(false);

    constructor(
        private _router: Router,
        private _api: QuickSharesApi,
        private _dataStore: DataStore,
        private _clipboard: Clipboard,
        private _snackBar: MatSnackBar
    ) {
    }

    async saveName(newName: string) {
        const qs = this.quickShare();
        qs.name.set(newName);

        await this._api.updateQuickShareName(
            qs.workspaceExternalId,
            qs.externalId,
            { name: newName });
    }

    copyLink() {
        const url = this.url();
        if (!url) return;

        if (this._clipboard.copy(url)) {
            this.copied.set(true);
            this._snackBar.open('Link copied to clipboard', 'Close', { duration: 2000 });
            setTimeout(() => this.copied.set(false), 2000);
        }
    }

    async deleteShare() {
        const qs = this.quickShare();

        await this._api.deleteQuickShare(
            qs.workspaceExternalId,
            qs.externalId);

        this._dataStore.invalidateQuickShares(qs.workspaceExternalId);
        this.deleted.emit();
    }

    editName() {
        this.quickShare().isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }

    openDetails() {
        const qs = this.quickShare();
        if (this.isNameEditing()) return;

        this._router.navigate([
            `workspaces/${qs.workspaceExternalId}/quick-shares/${qs.externalId}`
        ]);
    }

    toggleActions() {
        this.areActionsVisible.update(v => !v);
    }
}
