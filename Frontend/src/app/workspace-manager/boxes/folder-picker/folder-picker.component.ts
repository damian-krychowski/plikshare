import { Component, Inject, computed, signal, ViewEncapsulation, WritableSignal } from '@angular/core';
import { FilesExplorerApi, FilesExplorerComponent } from '../../../files-explorer/files-explorer.component';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FoldersAndFilesGetApi, FoldersAndFilesSetApi } from '../../../services/folders-and-files.api';
import { DataStore } from '../../../services/data-store.service';
import { WorkspaceFilesExplorerApi } from '../../../services/workspace-files-explorer-api';
import { MatButtonModule } from '@angular/material/button';
import { AppFolderItem } from '../../../shared/folder-item/folder-item.component';
import { FileLockService } from '../../../services/file-lock.service';

@Component({
    selector: 'app-folder-picker',
    imports: [
        FilesExplorerComponent,
        MatButtonModule
    ],
    templateUrl: './folder-picker.component.html',
    styleUrl: './folder-picker.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class FolderPickerComponent {
    currentFolderExternalId: WritableSignal<string | null> = signal(null);

    // The folder currently open in the embedded explorer — the share target.
    // null = workspace root, which cannot back a box, so confirming is disabled.
    selectedFolder = signal<AppFolderItem | null>(null);

    filesApi: WritableSignal<FilesExplorerApi>;
    uploadsApi = signal(null);

    targetFolderLabel = computed(() => {
        const folder = this.selectedFolder();

        if (!folder)
            return '';

        return [...folder.ancestors.map(a => a.name), folder.name()].join(' / ');
    });

    constructor(
        _setApi: FoldersAndFilesSetApi,
        _getApi: FoldersAndFilesGetApi,
        _dataStore: DataStore,
        _fileLockService: FileLockService,
        public dialogRef: MatDialogRef<FolderPickerComponent>,
        @Inject(MAT_DIALOG_DATA) public data: {workspaceExternalId: string}) {

        this.filesApi = signal(new WorkspaceFilesExplorerApi(
            _setApi,
            _getApi,
            _dataStore,
            _fileLockService,
            data.workspaceExternalId
        ));
    }

    public onFolderSelected(folder: AppFolderItem | null) {
        this.selectedFolder.set(folder);
    }

    public onConfirm() {
        const folder = this.selectedFolder();

        if (!folder)
            return;

        this.dialogRef.close(folder);
    }

    public onCancel() {
        this.dialogRef.close();
    }
}
