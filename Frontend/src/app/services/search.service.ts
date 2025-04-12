import { Injectable, Signal, signal } from "@angular/core";
import { ReplaySubject, map } from "rxjs"; // Import BehaviorSubject
import { SearchApi, SearchExternalBoxFileItemDto, SearchExternalBoxFolderItemDto, SearchExternalBoxGroupDto, SearchExternalBoxItemDto, SearchResponseDto, SearchWorkspaceBoxItemDto, SearchWorkspaceFileItemDto, SearchWorkspaceFolderItemDto, SearchWorkspaceGroupDto, SearchWorkspaceItemDto } from "./search.api";
import { AppWorkspace } from "../shared/workspace-item/workspace-item.component";
import { AuthService } from "./auth.service";
import { AppBox } from "../shared/box-item/box-item.component";
import { AppFolderAncestor, AppFolderItem, FolderOperations } from "../shared/folder-item/folder-item.component";
import { ContentDisposition, FoldersAndFilesGetApi, FoldersAndFilesSetApi } from "./folders-and-files.api";
import { DataStore } from "./data-store.service";
import { NavigationExtras, Router } from "@angular/router";
import { AppFileItem, FileOperations } from "../shared/file-item/file-item.component";
import { AppExternalBox } from "../shared/external-box-item/external-box-item.component";
import { ExternalBoxesGetApi, ExternalBoxesSetApi } from "../external-access/external-box/external-boxes.api";
import { FileLockService } from "./file-lock.service";

export type DashboardSearchItem = WorkspaceSearchItem 
    | ExternalBoxSearchItem;

export type SearchResult = {
    phrase: string;
    isEmpty: boolean;
    dashboardItems: DashboardSearchItem[];
    workspaceGroups: WorkspaceSearchGroup[];
    externalBoxGroups: ExternalBoxSearchGroup[];
}

export type WorkspaceGroupSearchItem = WorkspaceBoxSearchItem 
    | WorkspaceFolderSearchItem 
    | WorkspaceFileSearchItem;

export type WorkspaceSearchGroup = {    
    externalId: string;
    name: string;
    permissions: {
        allowShare: boolean;
    };
    
    items: WorkspaceGroupSearchItem[];
}

export type ExternalBoxGroupSearchItem = ExternalBoxFolderSearchItem
    | ExternalBoxFileSearchItem;

export type ExternalBoxSearchGroup = {
    externalId: string;
    name: string;
    permissions: {
        allowDownload: boolean;
        allowUpload: boolean;
        allowList: boolean;
        allowDeleteFile: boolean;
        allowDeleteFolder: boolean;
        allowRenameFile: boolean;
        allowRenameFolder: boolean;
        allowMoveItems: boolean;
        allowCreateFolder: boolean;
    };

    items: ExternalBoxGroupSearchItem[];
}


export type WorkspaceSearchItem = {
    type: "workspace";
    externalId: string;
    workspace: AppWorkspace;
}

export type WorkspaceBoxSearchItem = {
    type: "workspace-box";
    externalId: string;
    workspaceExternalId: string;
    box: AppBox;
}

export type WorkspaceFolderSearchItem = {
    type: "workspace-folder";
    externalId: string;
    workspaceExternalId: string;
    
    folder: AppFolderItem;    
    operations: FolderOperations;
}

export type WorkspaceFileSearchItem = {
    type: "workspace-file";
    externalId: string;
    workspaceExternalId: string;

    file: AppFileItem;
    operations: FileOperations;
}

export type ExternalBoxSearchItem = {
    type: "external-box";
    externalId: string;
    box: AppExternalBox;
}

export type ExternalBoxFolderSearchItem = {
    type: "external-box-folder";
    externalId: string;
    boxExternalId: string;
    
    folder: AppFolderItem;    
    operations: FolderOperations;
}

export type ExternalBoxFileSearchItem = {
    type: "external-box-file";
    externalId: string;
    boxExternalId: string;

    file: AppFileItem;
    operations: FileOperations;
}

@Injectable({
    providedIn: 'root',
})
export class SearchService {
    public isSearching = signal(false);
    public searchPhrase = signal('');


    // BehaviorSubject to hold the search results
    private _searchResults = new ReplaySubject<SearchResult | null>();

    // Publicly accessible observable for the search results
    public readonly searchResults$ = this._searchResults.asObservable();

    constructor(
        private _searchApi: SearchApi, 
        private _foldersAndfilesGetApi: FoldersAndFilesGetApi,
        private _foldersAndFilesSetApi: FoldersAndFilesSetApi,
        private _externalBoxesSetApi: ExternalBoxesSetApi,        
        private _externalBoxesGetApi: ExternalBoxesGetApi,
        private _fileLockService: FileLockService,        
        private _dataStore: DataStore,
        private _router: Router) {
    }

    public async performSearch(query: string) {
        if((this.isSearching() && query === this.searchPhrase()))
            return;

        if(query === '') {
            this._searchResults.next(null)
            return;
        }

        try {
            this.isSearching.set(true);
            this.searchPhrase.set(query);

            const result = await this._searchApi.search({
                phrase: query,
                workspaceExternalIds: [],
                boxExternalIds: []
            });
    
            const searchResult = this.mapSearchResult(
                query, 
                result);

            this._searchResults.next(searchResult);            
        } catch (error) {
            console.error("Error while searching", error);
        } finally {
            this.isSearching.set(false);
        }
    }

    public clearSearchResults() {
        this.searchPhrase.set('');

        this._searchResults.next({
            phrase: '',
            isEmpty: true,
            dashboardItems: [],
            workspaceGroups: [],
            externalBoxGroups: []
        });
    }

    private mapSearchResult(query: string, result: SearchResponseDto): SearchResult {        
        const dashboardItems: DashboardSearchItem[] = [];
        
        const workspaceGroups = this.prepareWorkspaceSearchGroups(
            result.workspaceGroups);

        const externalBoxGroups = this.prepareExternalBoxSearchGroups(
            result.externalBoxGroups);

        for (const item of result.workspaces) {
            const workspace = this.mapWorkspace(item);
            dashboardItems.push(workspace);
        }

        for (const item of result.workspaceFolders) {
            const workspaceFolder = this.mapWorkspaceFolder(item);
            this.addToWorkspaceGroup(workspaceGroups, workspaceFolder);
        }

        for (const item of result.workspaceFiles) {
            const workspaceFile = this.mapWorkspaceFile(item);       
            this.addToWorkspaceGroup(workspaceGroups, workspaceFile);
        }

        for (const item of result.workspaceBoxes) {
            const workspaceBox = this.mapWorkspaceBox(item);    
            this.addToWorkspaceGroup(workspaceGroups, workspaceBox);
        }

        for (const item of result.externalBoxes) {
            const externalBox = this.mapExternalBox(item);
            dashboardItems.push(externalBox);
        }

        for (const item of result.externalBoxFolders) {
            const externalBoxFolder = this.mapExternalBoxFolder(item);    
            this.addToExternalBoxGroup(externalBoxGroups, externalBoxFolder);
        }

        for (const item of result.externalBoxFiles) {
            const externalBoxFile = this.mapExternalBoxFile(item);
            this.addToExternalBoxGroup(externalBoxGroups, externalBoxFile);
        }

        return {
            phrase: query,
            isEmpty: dashboardItems.length == 0 && workspaceGroups.length == 0 && externalBoxGroups.length == 0,
            dashboardItems: dashboardItems,
            workspaceGroups: workspaceGroups,
            externalBoxGroups: externalBoxGroups
        };
    }

    private prepareWorkspaceSearchGroups(workspaceGroups: SearchWorkspaceGroupDto[]): WorkspaceSearchGroup[] {
        return workspaceGroups.map(group => ({
            externalId: group.externalId,
            name: group.name,
            permissions: {
                allowShare: group.allowShare
            },

            items: []
        }));
    }

    private prepareExternalBoxSearchGroups(externalBoxGroups: SearchExternalBoxGroupDto[]): ExternalBoxSearchGroup[] {
        return externalBoxGroups.map(group => ({
            externalId: group.externalId,
            name: group.name,
            permissions: {
                allowDownload: group.allowDownload,
                allowUpload: group.allowUpload,
                allowList: group.allowList,
                allowDeleteFile: group.allowDeleteFile,
                allowDeleteFolder: group.allowDeleteFolder,
                allowRenameFile: group.allowRenameFile,
                allowRenameFolder: group.allowRenameFolder,
                allowMoveItems: group.allowMoveItems,
                allowCreateFolder: group.allowCreateFolder
            },
            items: []
        }));
    }

    private addToWorkspaceGroup(
        groups: WorkspaceSearchGroup[],
        item: WorkspaceGroupSearchItem) {
                
        const group = this.getWorkspaceGroup(
            groups, 
            item.workspaceExternalId);

        group.items.push(item);
    }

    private getWorkspaceGroup(
        groups: WorkspaceSearchGroup[],
        externalId: string): WorkspaceSearchGroup {
            
        const existingGroup = groups.find(g => g.externalId === externalId);

        if(!existingGroup) {
            throw new Error("Workspace group not found: " + externalId);
        }

        return existingGroup;
    }

    private addToExternalBoxGroup(
        groups: ExternalBoxSearchGroup[],
        item: ExternalBoxGroupSearchItem) {
                
        const group = this.getExternalBoxGroup(
            groups, 
            item.boxExternalId);

        group.items.push(item);
    }

    private getExternalBoxGroup(
        groups: ExternalBoxSearchGroup[],
        externalId: string): ExternalBoxSearchGroup {
            
        const existingGroup = groups.find(g => g.externalId === externalId);

        if(!existingGroup) {
            throw new Error("External box group not found: " + externalId);
        }

        return existingGroup;
    }

    private mapExternalBox(item: SearchExternalBoxItemDto): ExternalBoxSearchItem {
        const box: AppExternalBox = {
            type: 'app-external-box',
            boxExternalId: signal(item.externalId),
            boxName: signal(item.name),
            owner: signal({
                email: signal(item.ownerEmail),
                externalId: item.ownerExternalId,
            }),
            isHighlighted: signal(false),
            permissions: signal({
                allowCreateFolder: signal(item.allowCreateFolder),
                allowDeleteFile: signal(item.allowDeleteFile),
                allowDeleteFolder: signal(item.allowDeleteFolder),
                allowDownload: signal(item.allowDownload),
                allowList: signal(item.allowList),
                allowMoveItems: signal(item.allowMoveItems),
                allowRenameFile: signal(item.allowRenameFile),
                allowRenameFolder: signal(item.allowRenameFolder),
                allowUpload: signal(item.allowUpload)
            }),
            workspace: signal(undefined)
        };

        return {
            type: "external-box",
            externalId: item.externalId,
            box: box
        };
    }

    private mapWorkspaceFile(item: SearchWorkspaceFileItemDto): WorkspaceFileSearchItem {
        const folderPath = item.folderPath;
        const folderExternalId = !folderPath || folderPath.length == 0
            ? null
            : folderPath[folderPath.length - 1].externalId;
        
        const file: AppFileItem = {
            type: 'file',

            externalId: item.externalId,
            folderExternalId: folderExternalId,
            name: signal(item.name),
            extension: item.extension,
            sizeInBytes: item.sizeInBytes,
            wasUploadedByUser: true,
            folderPath: folderPath,
            isLocked: signal(false), //todo: should not be hardcoded

            isNameEditing: signal(false),
            isSelected: signal(false),
            isCut: signal(false),
            isHighlighted: signal(false)
        };

        return {
            type: "workspace-file",
            externalId: item.externalId,
            workspaceExternalId: item.workspaceExternalId,
            file: file,
            operations: this.getWorkspaceOperations(
                item.workspaceExternalId)
        };
    }

    private mapExternalBoxFile(item: SearchExternalBoxFileItemDto): ExternalBoxFileSearchItem {
        const folderPath = item.folderPath;        
        const folderExternalId = !folderPath || folderPath.length == 0
            ? null
            : folderPath[folderPath.length - 1].externalId;

        const file: AppFileItem = {
            type: 'file',

            externalId: item.externalId,
            folderExternalId: folderExternalId,
            name: signal(item.name),
            extension: item.extension,
            sizeInBytes: item.sizeInBytes,
            wasUploadedByUser: item.wasUploadedByUser,
            folderPath: folderPath,
            isLocked: signal(false), //todo: should not be hardcoded

            isNameEditing: signal(false),
            isSelected: signal(false),
            isCut: signal(false),
            isHighlighted: signal(false)
        };

        return {
            type: "external-box-file",
            externalId: item.externalId,
            boxExternalId: item.boxExternalId,
            file: file,
            operations: this.getExternalBoxOperations(
                item.boxExternalId)
        };
    }

    private mapWorkspaceFolder(item: SearchWorkspaceFolderItemDto): WorkspaceFolderSearchItem {
        const folder: AppFolderItem = {
            type: 'folder',
            externalId: item.externalId,
            name: signal(item.name),
            ancestors: item.ancestors,
            isNameEditing: signal(false),
            isSelected: signal(false),
            isCut: signal(false),
            isHighlighted: signal(false),
            wasCreatedByUser: false,
            createdAt: null
        };

        return {
            type: "workspace-folder",
            externalId: item.externalId,
            workspaceExternalId: item.workspaceExternalId,

            folder: folder,
            operations: this.getWorkspaceOperations(
                item.workspaceExternalId)
        };
    }

    private mapExternalBoxFolder(item: SearchExternalBoxFolderItemDto): ExternalBoxFolderSearchItem {
        const folder: AppFolderItem = {
            type: 'folder',
            externalId: item.externalId,
            name: signal(item.name),
            ancestors: item.ancestors,
            isNameEditing: signal(false),
            isSelected: signal(false),
            isCut: signal(false),
            isHighlighted: signal(false),
            wasCreatedByUser: false,
            createdAt: null
        };

        return {
            type: "external-box-folder",
            externalId: item.externalId,
            boxExternalId: item.boxExternalId,

            folder: folder,
            operations: this.getExternalBoxOperations(
                item.boxExternalId)
        };
    }

    private mapWorkspaceBox(item: SearchWorkspaceBoxItemDto): WorkspaceBoxSearchItem {

        const box: AppBox = {
            externalId: signal(item.externalId),
            name: signal(item.name),
            workspaceExternalId: item.workspaceExternalId,

            folderPath: signal(item.folderPath),

            isEnabled: signal(item.isEnabled),
            isNameEditing: signal(false),
            isHighlighted: signal(false)
        };

        return {
            type: "workspace-box",
            workspaceExternalId: item.workspaceExternalId,
            externalId: item.externalId,
            box: box
        };
    }

    private mapWorkspace(item: SearchWorkspaceItemDto): WorkspaceSearchItem {
        const workspace: AppWorkspace = {
            type: 'app-workspace',
            externalId: signal(item.externalId),
            name: signal(item.name),
            currentSizeInBytes: signal(item.currentSizeInBytes),        
            maxSizeInBytes: signal(item.maxSizeInBytes == -1 ? null : item.maxSizeInBytes),    
            owner: signal({
                email: signal(item.ownerEmail),
                externalId: item.ownerExternalId,
            }),
            wasUserInvited: signal(!item.isOwnedByUser),
            isUsedByIntegration: item.isUsedByIntegration,
            isBucketCreated: signal(item.isBucketCreated),
            permissions: {
                allowShare: item.allowShare,
            },
            storageName: signal(null),
            isNameEditing: signal(false),
            isHighlighted: signal(false)
        };

        return {
            type: "workspace",
            externalId: item.externalId,
            workspace: workspace
        };
    }

    private getWorkspaceOperations(workspaceExternalId: string): FolderOperations & FileOperations {
        return {
            saveFolderNameFunc: async (folderExternalId: string | null, newName: string) => {
                if(folderExternalId == null)
                    return;
                
                await this._foldersAndFilesSetApi.updateFolderName(
                    workspaceExternalId, 
                    folderExternalId, {
                        name: newName
                    }
                );
            },

            prefetchFolderFunc: (folderExternalId: string | null) => {
                if(folderExternalId == null)
                    this._dataStore.prefetchWorkspaceTopFolders(workspaceExternalId);
                else 
                    this._dataStore.prefetchWorkspaceFolder(
                        workspaceExternalId, 
                        folderExternalId);
            },

            openFolderFunc: (folderExternalId: string | null, navitagionExtras: NavigationExtras | null) => {
                if(folderExternalId == null && navitagionExtras == null)
                    return;
    
                if(folderExternalId == null && navitagionExtras != null)
                    this._router.navigate([`/workspaces/${workspaceExternalId}/`], navitagionExtras);
                else if(navitagionExtras == null)
                    this._router.navigate([`/workspaces/${workspaceExternalId}/explorer/${folderExternalId}`]);
                else
                    this._router.navigate([`/workspaces/${workspaceExternalId}/explorer/${folderExternalId}`], navitagionExtras);
            },

            deleteFolderFunc: async (folderExternalId: string | null) => {
                if(folderExternalId == null)
                    return;

                await this._foldersAndFilesSetApi.bulkDelete({
                    workspaceExternalId: workspaceExternalId, 
                    fileExternalIds: [],
                    folderExternalIds: [folderExternalId],
                    fileUploadExternalIds: []
                });
            },

            saveFileNameFunc: async (fileExternalId: string, newName: string) => this._foldersAndFilesSetApi.updateFileName(
                workspaceExternalId, 
                fileExternalId, {
                    name: newName
                }
            ),

            deleteFileFunc: async (fileExternalId: string) => {
                await this._foldersAndFilesSetApi.bulkDelete({
                    workspaceExternalId: workspaceExternalId, 
                    fileExternalIds: [fileExternalId],
                    folderExternalIds: [],
                    fileUploadExternalIds: []
                });
            },

            getDownloadLink: async (fileExternalId: string, contentDisposition: ContentDisposition) => this._foldersAndfilesGetApi.getDownloadLink(
                workspaceExternalId, 
                fileExternalId,
                contentDisposition),
                
            subscribeToLockStatus: (file: AppFileItem) => this._fileLockService.subscribeToLockStatus(file),
            unsubscribeFromLockStatus: (fileExternalId: string) => this._fileLockService.unsubscribe(fileExternalId)
        }
    }

    private getExternalBoxOperations(boxExternalId: string): FolderOperations & FileOperations {
        return {
            saveFolderNameFunc: async (folderExternalId: string | null, newName: string) => {
                if(folderExternalId == null)
                    return;
                
                await this._externalBoxesSetApi.updateFolderName(
                    boxExternalId, 
                    folderExternalId, {
                        name: newName
                    }
                );
            },

            prefetchFolderFunc: (folderExternalId: string | null) => {
                if(folderExternalId == null)
                    this._dataStore.prefetchExternalBoxDetailsAndContent(
                        boxExternalId);
                else 
                    this._dataStore.prefetchExternalBoxFolder(
                        boxExternalId, 
                        folderExternalId);
            },

            openFolderFunc: (folderExternalId: string | null, navitagionExtras: NavigationExtras | null) => {
                if(folderExternalId == null && navitagionExtras == null)
                    return;
            
                if(folderExternalId == null && navitagionExtras != null)
                    this._router.navigate([`/box/${boxExternalId}/`], navitagionExtras);
                else if(navitagionExtras == null)
                    this._router.navigate([`/box/${boxExternalId}/${folderExternalId}`]);
                else
                    this._router.navigate([`/box/${boxExternalId}/${folderExternalId}`], navitagionExtras);
            },

            deleteFolderFunc: async (folderExternalId: string | null) => {
                if(folderExternalId == null)
                    return;

                await this._externalBoxesSetApi.bulkDelete({
                    boxExternalId: boxExternalId,
                    fileExternalIds: [],
                    folderExternalIds: [folderExternalId],
                    fileUploadExternalIds: []
                });
            },

            saveFileNameFunc: (fileExternalId: string, newName: string) => this._externalBoxesSetApi.updateFileName(
                boxExternalId,
                fileExternalId, {
                    name: newName
                }
            ),

            deleteFileFunc: async (fileExternalId: string) => {
                await this._externalBoxesSetApi.bulkDelete({
                    boxExternalId: boxExternalId,
                    fileExternalIds: [fileExternalId],
                    folderExternalIds: [],
                    fileUploadExternalIds: []
                });
            },

            getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => this._externalBoxesGetApi.getDownloadLink(
                boxExternalId, 
                fileExternalId,
                contentDisposition
            ),

            subscribeToLockStatus: (file: AppFileItem) => this._fileLockService.subscribeToLockStatus(file),
            unsubscribeFromLockStatus: (fileExternalId: string) => this._fileLockService.unsubscribe(fileExternalId)
        }
    }
}
