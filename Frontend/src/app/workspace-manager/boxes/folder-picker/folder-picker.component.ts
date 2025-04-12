import { Component, Inject, signal, ViewEncapsulation, WritableSignal } from '@angular/core';
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

    filesApi: WritableSignal<FilesExplorerApi>;
    uploadsApi = signal(null);

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

    public onBoxCreated(folder: AppFolderItem) {
        this.dialogRef.close(folder);
    }

    public onCancel() {
        this.dialogRef.close();
    }
}
