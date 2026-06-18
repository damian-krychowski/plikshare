import { Component, Inject, OnInit, Optional, signal, WritableSignal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { FormsModule } from '@angular/forms';
import { WorkspacesApi, AdminWorkspaceListItem } from '../../../services/workspaces.api';
import { AgentsApi, WorkspaceBoxItem } from '../../../services/agents.api';

export interface GrantBoxAccessDialogData {
    alreadyGrantedBoxExternalIds?: string[];
}

export interface GrantBoxAccessResult {
    boxExternalId: string;
    boxName: string;
}

@Component({
    selector: 'app-grant-box-access-dialog',
    imports: [
        FormsModule,
        MatButtonModule,
        MatFormFieldModule,
        MatSelectModule
    ],
    templateUrl: './grant-box-access-dialog.component.html',
    styleUrl: './grant-box-access-dialog.component.scss'
})
export class GrantBoxAccessDialogComponent implements OnInit {
    isLoadingWorkspaces = signal(false);
    isLoadingBoxes = signal(false);

    workspaces: WritableSignal<AdminWorkspaceListItem[]> = signal([]);
    boxes: WritableSignal<WorkspaceBoxItem[]> = signal([]);

    selectedWorkspaceExternalId = signal<string | null>(null);
    selectedBoxExternalId = signal<string | null>(null);

    private _alreadyGrantedBoxExternalIds: Set<string>;

    constructor(
        public dialogRef: MatDialogRef<GrantBoxAccessDialogComponent>,
        private _workspacesApi: WorkspacesApi,
        private _agentsApi: AgentsApi,
        @Optional() @Inject(MAT_DIALOG_DATA) data: GrantBoxAccessDialogData | null) {
        this._alreadyGrantedBoxExternalIds = new Set<string>(data?.alreadyGrantedBoxExternalIds ?? []);
    }

    isBoxAlreadyGranted(externalId: string): boolean {
        return this._alreadyGrantedBoxExternalIds.has(externalId);
    }

    async ngOnInit(): Promise<void> {
        this.isLoadingWorkspaces.set(true);

        try {
            const response = await this._workspacesApi.getAllWorkspacesAdmin();
            this.workspaces.set(response.items);
        } finally {
            this.isLoadingWorkspaces.set(false);
        }
    }

    async onWorkspaceChange(workspaceExternalId: string) {
        this.selectedWorkspaceExternalId.set(workspaceExternalId);
        this.selectedBoxExternalId.set(null);
        this.boxes.set([]);

        this.isLoadingBoxes.set(true);

        try {
            const response = await this._agentsApi.listWorkspaceBoxes(workspaceExternalId);
            this.boxes.set(response.items);
        } finally {
            this.isLoadingBoxes.set(false);
        }
    }

    onGrant() {
        const boxExternalId = this.selectedBoxExternalId();

        if (!boxExternalId)
            return;

        const box = this.boxes().find(b => b.externalId === boxExternalId);

        if (!box)
            return;

        const result: GrantBoxAccessResult = {
            boxExternalId: box.externalId,
            boxName: box.name
        };

        this.dialogRef.close(result);
    }

    onCancel() {
        this.dialogRef.close();
    }
}
