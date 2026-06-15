import { Component, computed, Inject, OnInit, Optional, Signal, signal, WritableSignal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { WorkspacesApi, AdminWorkspaceListItem } from '../../services/workspaces.api';
import { ItemSearchComponent, ItemSearchCount } from '../item-search/item-search.component';
import { StorageSizePipe } from '../storage-size.pipe';

export interface WorkspacePickerDialogData {
    /**
     * Hide workspaces where the target user is already owner or member.
     * The picker calls GET /api/workspaces/admin-list-all?excludeMemberOrOwnerExternalId=...
     * so the listing returns only actionable candidates.
     */
    excludeMemberOrOwnerExternalId?: string;

    /**
     * Optional override for the explanatory subtitle. Falls back to a generic
     * "user will be added as a member" message when not provided.
     */
    subtitle?: string;

    /**
     * Workspaces that are already accessible to the target (e.g. already granted to an
     * agent). They are still shown — dimmed, badged "Already granted" and not selectable —
     * so the picker reflects the full set rather than silently hiding entries.
     */
    alreadyGrantedExternalIds?: string[];
}

@Component({
    selector: 'app-workspace-picker',
    imports: [
        MatButtonModule,
        MatTooltipModule,
        ItemSearchComponent,
        StorageSizePipe
    ],
    templateUrl: './workspace-picker.component.html',
    styleUrls: ['./workspace-picker.component.scss']
})
export class WorkspacePickerComponent implements OnInit {
    private _allWorkspaces: AdminWorkspaceListItem[] = [];
    private _excludeMemberOrOwnerExternalId: string | undefined;
    private _alreadyGrantedExternalIds = new Set<string>();

    isLoading = signal(false);
    workspaces: WritableSignal<AdminWorkspaceListItem[]> = signal([]);

    subtitle: Signal<string>;

    searchPhrase = signal('');
    searchCount: Signal<ItemSearchCount> = computed(() => ({
        allItems: this._allWorkspaces.length,
        matchingItems: this.workspaces().length
    }));

    constructor(
        private _workspacesApi: WorkspacesApi,
        public dialogRef: MatDialogRef<WorkspacePickerComponent>,
        @Optional() @Inject(MAT_DIALOG_DATA) data: WorkspacePickerDialogData | null) {
        this._excludeMemberOrOwnerExternalId = data?.excludeMemberOrOwnerExternalId;
        this._alreadyGrantedExternalIds = new Set<string>(data?.alreadyGrantedExternalIds ?? []);
        this.subtitle = signal(data?.subtitle
            ?? 'The selected workspace will gain this user as a member. '
             + 'For full-encryption workspaces you must have your encryption password unlocked '
             + 'so the workspace key can be re-wrapped for the new member.');
    }

    async ngOnInit(): Promise<void> {
        this.isLoading.set(true);

        try {
            const response = await this._workspacesApi.getAllWorkspacesAdmin(
                this._excludeMemberOrOwnerExternalId);

            this._allWorkspaces = response.items;
            this.workspaces.set(this._allWorkspaces);
        } finally {
            this.isLoading.set(false);
        }
    }

    public isAlreadyGranted(externalId: string): boolean {
        return this._alreadyGrantedExternalIds.has(externalId);
    }

    public onWorkspacePicked(workspace: AdminWorkspaceListItem) {
        if (this.isAlreadyGranted(workspace.externalId))
            return;

        this.dialogRef.close(workspace);
    }

    public onCancel() {
        this.dialogRef.close();
    }

    performSearch(query: string) {
        const lower = query.toLowerCase();
        this.searchPhrase.set(query);
        this.workspaces.set(this._allWorkspaces.filter(w =>
            w.name.toLowerCase().includes(lower)
            || w.owner.email.toLowerCase().includes(lower)
            || w.storage.name.toLowerCase().includes(lower)));
    }
}
