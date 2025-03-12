import { Component, input, signal } from "@angular/core";
import { FileTreeViewComponent, LoadFolderNodeRequest } from "../shared/file-tree-view/file-tree-view.component";
import { FilesExplorerApi } from "../files-explorer/files-explorer.component";
import { AppFolderItem } from "../shared/folder-item/folder-item.component";
import { mapGetFolderResponseToItems } from "../services/folders-and-files.api";
import { AppFileItem } from "../shared/file-item/file-item.component";

export type ItemsToProcessSelection = {
    selectedFolderExternalIds: string[];
    selectedFileExternalIds: string[];

    excludedFolderExternalIds: string[];
    excludedFileExternalIds: string[];
}

@Component({
    selector: 'app-files-processor',
    imports: [
        FileTreeViewComponent
    ],
    templateUrl: './files-processor.component.html',
    styleUrl: './files-processor.component.scss'
})
export class FilesProcessorComponent {
    filesApi = input.required<FilesExplorerApi>();

    explorerTreeItems = signal<(AppFolderItem | AppFileItem)[]>([]);

    addItemsToProcess(selection: ItemsToProcessSelection) {
        
    }

    onFolderTreePrefetchRequested(folder: AppFolderItem) {
        this.filesApi().prefetchFolder(folder.externalId);
    }

    async onFolderTreeLoadRequested(request: LoadFolderNodeRequest) {
        const folderResponse = await this.filesApi().getFolder(
            request.folder.externalId);

        const { selectedFolder, subfolders, files, uploads } = mapGetFolderResponseToItems(
            null,
            folderResponse);
        
        request.folderLoadedCallback(
            [...subfolders, ...files]);
    }
}