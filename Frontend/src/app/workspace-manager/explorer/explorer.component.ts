import { Component, OnDestroy, OnInit, signal, WritableSignal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, Navigation, NavigationEnd, NavigationExtras, Router, RouterModule } from '@angular/router';
import { FilesExplorerApi, FilesExplorerComponent, ItemToHighlight } from '../../files-explorer/files-explorer.component';
import { FoldersAndFilesGetApi, FoldersAndFilesSetApi } from '../../services/folders-and-files.api';
import { BulkInitiateFileUploadRequest, UploadsApi } from '../../services/uploads.api';
import { Subscription, filter } from 'rxjs';
import { FileUploadApi } from '../../services/file-upload-manager/file-upload-manager';
import { InAppSharing } from '../../services/in-app-sharing.service';
import { DataStore } from '../../services/data-store.service';
import { WorkspaceFilesExplorerApi } from '../../services/workspace-files-explorer-api';
import { AppFolderItem } from '../../shared/folder-item/folder-item.component';
import { AppFileItem } from '../../shared/file-item/file-item.component';
import { WorkspaceContextService } from '../workspace-context.service';
import { CookieUtils } from '../../shared/cookies';
import { FileLockService } from '../../services/file-lock.service';

@Component({
    selector: 'app-explorer',
    imports: [
        RouterModule,
        MatButtonModule,
        FilesExplorerComponent
    ],
    templateUrl: './explorer.component.html',
    styleUrl: './explorer.component.scss'
})
export class ExplorerComponent implements OnInit, OnDestroy {
    currentFolderExternalId: WritableSignal<string | null> = signal(null);    
    currentFileExternalIdInPreview: WritableSignal<string | null> = signal(null);
    itemToHighlight: WritableSignal<ItemToHighlight | null> = signal(null);
    filesApi: WritableSignal<FilesExplorerApi | null> = signal(null);
    uploadsApi: WritableSignal<FileUploadApi | null> = signal(null);

    private _workspaceExternalId: string | null = null;

    constructor(
        private _fileUploadApi: UploadsApi,
        private _activatedRoute: ActivatedRoute,
        private _router: Router,
        private _inAppSharing: InAppSharing,
        private _setApi: FoldersAndFilesSetApi,
        private _getApi: FoldersAndFilesGetApi,
        private _dataStore: DataStore,
        private _fileLockService: FileLockService,
        public context: WorkspaceContextService,
    ) { 

    }

    private _routerSubscription: Subscription | null = null;

    ngOnInit(): void {
        this.handleNavigationChange(this._router.lastSuccessfulNavigation);

        this._routerSubscription = this._router.events
            .pipe(filter(event => event instanceof NavigationEnd))
            .subscribe(() => {
                const navigation = this._router.getCurrentNavigation();
                this.handleNavigationChange(navigation);
            });
    }

    private handleNavigationChange(navigation: Navigation | null) {
        this.load();
        this.tryConsumeNavigationState(navigation);
    }

    private load() {
        const folderExternalId = this._activatedRoute.snapshot.params['folderExternalId'] || null;
        const workspaceExternalId = this._activatedRoute.parent?.snapshot.params['workspaceExternalId'] || null;
        const fileExternalId = this._activatedRoute.snapshot.queryParams['fileId'] || null;

        if(fileExternalId != this.currentFileExternalIdInPreview()) {
            this.currentFileExternalIdInPreview.set(fileExternalId);
        }

        if(folderExternalId == this.currentFolderExternalId() && workspaceExternalId == this._workspaceExternalId){
            return;
        }

        this._workspaceExternalId = workspaceExternalId;
        this.currentFolderExternalId.set(folderExternalId);

        this.filesApi.set(new WorkspaceFilesExplorerApi(
            this._setApi,
            this._getApi,
            this._dataStore,
            this._fileLockService,
            workspaceExternalId));


        this.uploadsApi.set(this.getFileUploadApi(
            workspaceExternalId));
    }

    private tryConsumeNavigationState(navigation: Navigation | null) {
        if(!navigation || !navigation.extras)
            return;

        this.tryHighlighFolder(navigation.extras);
        this.tryHighlightFile(navigation.extras); 
    }

    private tryHighlighFolder(extras: NavigationExtras) {
        if(!extras.state || !extras.state['folderToHighlight'])
            return;

        const folderToHighlightKey = extras
            .state['folderToHighlight'] as string;

        const folderExternalId = this
            ._inAppSharing
            .pop(folderToHighlightKey) as string;

        if(folderExternalId) {
            this.itemToHighlight.set({
                type: 'folder',
                externalId: folderExternalId
            });
        }
    }

    private tryHighlightFile(extras: NavigationExtras) {
        if(!extras.state || !extras.state['fileToHighlight'])
            return;

        const fileToHighlightKey = extras
            .state['fileToHighlight'] as string;

        const fileExternalId = this
            ._inAppSharing
            .pop(fileToHighlightKey) as string;

        if(fileExternalId) {
            this.itemToHighlight.set({
                type: 'file',
                externalId: fileExternalId
            });
        }
    }

    ngOnDestroy(): void {
        this._routerSubscription?.unsubscribe();
    }

    public onFolderSelected(folder: AppFolderItem | null) {
        if(this._workspaceExternalId == null) {
            throw new Error('Workspace is not set');
        }

        const folderExternalId = folder?.externalId ?? null;

        if(folderExternalId == this.currentFolderExternalId()){
            return;
        }

        this.currentFolderExternalId.set(folderExternalId);

        this.setRoute(folderExternalId, undefined);
    }

    public onWorkspaceSizeUpdated(newWorkspaceSizeInBytes: number) {
        this.context.updateWorkspaceSize(newWorkspaceSizeInBytes);
    }

    public onFilePreviewed(file: AppFileItem | null) {
        if(this._workspaceExternalId == null) {
            throw new Error('Workspace is not set');
        }
    
        // Handle null file (preview closed)
        if(file === null) {
            this.currentFileExternalIdInPreview.set(null);
            this.setRoute(this.currentFolderExternalId(), undefined);
        } else {
            if(file.externalId == this.currentFileExternalIdInPreview()) {
                return;
            }
        
            this.currentFileExternalIdInPreview.set(file.externalId);        
            this.setRoute(file.folderExternalId, { fileId: file.externalId });
        }
    }

    private setRoute(folderExternalId: string | null, queryParams: any | undefined) {
        if(folderExternalId == null){
            this._router.navigate([`workspaces/${this._workspaceExternalId}/explorer`], {
                replaceUrl: true,
                queryParams
            });
        } else {
            this._router.navigate([`workspaces/${this._workspaceExternalId}/explorer/${folderExternalId}`], {
                replaceUrl: true,
                queryParams
            });
        }
    }

    private getFileUploadApi(workspaceExternalId: string): FileUploadApi {
        return {
            bulkInitiateUpload: (request: BulkInitiateFileUploadRequest) => this._fileUploadApi.bulkInitiateUpload(
                    workspaceExternalId, 
                    request),

            getUploadDetails: (uploadExternalId: string) => this._fileUploadApi.getUploadDetails(
                workspaceExternalId, 
                uploadExternalId),

            initiatePartUpload: (uploadExternalId: string, partNumber: number) => this._fileUploadApi.initiatePartUpload(
                workspaceExternalId, 
                uploadExternalId, 
                partNumber),

            completePartUpload: (uploadExternalId: string, partNumber: number, request: {eTag: string}) => this._fileUploadApi.completePartUpload(
                workspaceExternalId, 
                uploadExternalId, 
                partNumber, 
                request),

            completeUpload: (uploadExternalId: string) => this._fileUploadApi.completeUpload(
                workspaceExternalId,
                uploadExternalId),

            abort: async (uploadExternalId: string) => {
                const result = await this.filesApi()!.bulkDelete([],[],[uploadExternalId])

                if(result.newWorkspaceSizeInBytes != null){
                    this.context.updateWorkspaceSize(result.newWorkspaceSizeInBytes);
                }
            }
        }
    }

    onBoxCreated(folder: AppFolderItem){
        if(this._workspaceExternalId == null) {
            throw new Error('Workspace is not set');
        }

        const temporaryKey = this._inAppSharing.set(folder);

        const navigationExtras: NavigationExtras = {
            state: {
                folderToCreateBoxFrom: temporaryKey
            }
        };

        this._router.navigate(
            [`/workspaces/${this._workspaceExternalId}/boxes`], 
            navigationExtras);
    }
}
