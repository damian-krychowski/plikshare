import { Component, Inject, ViewEncapsulation, WritableSignal, computed, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatRadioChange, MatRadioModule } from '@angular/material/radio';
import { FilesExplorerApi, FilesExplorerComponent } from '../../../files-explorer/files-explorer.component';
import { FoldersAndFilesGetApi, FoldersAndFilesSetApi } from '../../../services/folders-and-files.api';
import { DataStore } from '../../../services/data-store.service';
import { FileLockService } from '../../../services/file-lock.service';
import { WorkspaceFilesExplorerApi } from '../../../services/workspace-files-explorer-api';
import { AppFolderItem } from '../../../shared/folder-item/folder-item.component';
import { RestoreMode } from '../../../services/trash.api';

export type RestoreFromTrashDialogData = {
    count: number;
    workspaceExternalId: string;
};

export type RestoreFromTrashDialogResult = {
    mode: RestoreMode;

    // The destination folder for 'chosen-folder'; null means the workspace root.
    // Always null for 'original-path'.
    targetFolderExternalId: string | null;
};

/**
 * Restore flow with a destination choice: put files back where they were ('original-path')
 * or into a single folder the user navigates to in the embedded explorer ('chosen-folder').
 */
@Component({
    selector: 'app-restore-from-trash-dialog',
    standalone: true,
    imports: [
        MatButtonModule,
        MatRadioModule,
        FilesExplorerComponent
    ],
    templateUrl: './restore-from-trash-dialog.component.html',
    styleUrl: './restore-from-trash-dialog.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class RestoreFromTrashDialogComponent {
    mode = signal<RestoreMode>('original-path');

    // The folder currently open in the embedded explorer — the 'chosen-folder' target.
    // null = workspace root.
    selectedFolder = signal<AppFolderItem | null>(null);

    filesApi: WritableSignal<FilesExplorerApi>;
    uploadsApi = signal(null);
    currentFolderExternalId = signal<string | null>(null);

    targetFolderLabel = computed(() => {
        const folder = this.selectedFolder();

        if (!folder)
            return 'Workspace root';

        return [...folder.ancestors.map(a => a.name), folder.name()].join(' / ');
    });

    constructor(
        setApi: FoldersAndFilesSetApi,
        getApi: FoldersAndFilesGetApi,
        dataStore: DataStore,
        fileLockService: FileLockService,
        public dialogRef: MatDialogRef<RestoreFromTrashDialogComponent, RestoreFromTrashDialogResult>,
        @Inject(MAT_DIALOG_DATA) public data: RestoreFromTrashDialogData)
    {
        this.filesApi = signal(new WorkspaceFilesExplorerApi(
            setApi,
            getApi,
            dataStore,
            fileLockService,
            data.workspaceExternalId));
    }

    onModeChange(event: MatRadioChange) {
        this.mode.set(event.value as RestoreMode);
    }

    onFolderSelected(folder: AppFolderItem | null) {
        this.selectedFolder.set(folder);
    }

    restore() {
        this.dialogRef.close({
            mode: this.mode(),
            targetFolderExternalId: this.mode() === 'chosen-folder'
                ? (this.selectedFolder()?.externalId ?? null)
                : null
        });
    }

    cancel() {
        this.dialogRef.close();
    }
}
