import { Component, OnDestroy, OnInit, signal, WritableSignal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { BulkInitiateFileUploadRequest, UploadsApi } from '../../services/uploads.api';
import { FileUploadApi, FileUploadManager } from '../../services/file-upload-manager/file-upload-manager';
import { Subscription } from 'rxjs';
import { DataStore } from '../../services/data-store.service';
import { FoldersAndFilesSetApi } from '../../services/folders-and-files.api';
import { AppUploadListItem, UploadListItemComponent } from './upload-list-item/upload-list-item.component';
import { removeItems } from '../../shared/signal-utils';
import { CookieUtils } from '../../shared/cookies';
import { WorkspaceContextService } from '../workspace-context.service';

@Component({
    selector: 'app-uploads',
    imports: [
        UploadListItemComponent
    ],
    templateUrl: './uploads.component.html',
    styleUrl: './uploads.component.scss'
})
export class UploadsComponent implements OnInit, OnDestroy {

    isLoading = signal(false);
    uploads: WritableSignal<AppUploadListItem[]> = signal([]);
    fileUploadApi: WritableSignal<FileUploadApi | null> = signal(null);

    private _uploadCompletedSubscription: Subscription | null = null;
    public workspaceExternalId: string | null = null;

    private get _workspaceExternalId() {
        if(!this.workspaceExternalId) {
            throw new Error('Workspace external id is not set.');
        }

        return this.workspaceExternalId;
    }

    constructor(
        private _foldersAndFilesSetApi: FoldersAndFilesSetApi,
        private _uploadsApi: UploadsApi,
        public fileUploadManager: FileUploadManager,
        private _activatedRoute: ActivatedRoute,
        private _dataStore: DataStore,
        private _workspaceContext: WorkspaceContextService
    ) {

    }

    async ngOnInit() {
        this.workspaceExternalId = this._activatedRoute.parent?.snapshot.params['workspaceExternalId'] || null;

        this._uploadCompletedSubscription = this
            .fileUploadManager
            .uploadCompleted
            .subscribe(async (upload) => {
                this.uploads.update(values => values.filter(u => u.externalId() !== upload.uploadExternalId));
            });

        this.fileUploadApi.set(
            this.getFileUploadApi());

        await this.loadUploads();        
    }


    ngOnDestroy(): void {
        this._uploadCompletedSubscription?.unsubscribe();
    }

    private async loadUploads() {
        try {
            this.isLoading.set(true);

            const response = await this._dataStore.getUploadList(
                this._workspaceExternalId);

            this.uploads.set(response
                .items            
                .map(upload => {
                    const item: AppUploadListItem = {
                        externalId: signal(upload.externalId),
                        fileName: signal(upload.fileName),
                        fileExtension: signal(upload.fileExtension),
                        fileContentType: signal(upload.fileContentType),
                        fileSizeInBytes: signal(upload.fileSizeInBytes),
    
                        folderName: signal(upload.folderName),
                        folderExternalId: signal(upload.folderExternalId),
                        folderPath: signal(upload.folderPath),
    
                        alreadyUploadedPartNumbers: signal(upload.alreadyUploadedPartNumbers)
                    };

                    return item;
                }));
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    async onUploadAborted(upload: AppUploadListItem) {
        removeItems(this.uploads, upload);

        const result = await this._foldersAndFilesSetApi.bulkDelete({
            workspaceExternalId: this._workspaceExternalId,
            fileUploadExternalIds: [upload.externalId()],
            folderExternalIds: [],
            fileExternalIds: []
        });

        if(result.newWorkspaceSizeInBytes != null){
            this._workspaceContext.updateWorkspaceSize(result.newWorkspaceSizeInBytes);
        }
    }    

    private getFileUploadApi(): FileUploadApi {
        return {
            bulkInitiateUpload: (request: BulkInitiateFileUploadRequest) => {
                return this._uploadsApi.bulkInitiateUpload(this._workspaceExternalId, request);
            },

            getUploadDetails: (uploadExternalId: string) => {
                return this._uploadsApi.getUploadDetails(this._workspaceExternalId, uploadExternalId);
            },

            initiatePartUpload: (uploadExternalId: string, partNumber: number) => {
                return this._uploadsApi.initiatePartUpload(this._workspaceExternalId, uploadExternalId, partNumber);
            },

            completePartUpload: (uploadExternalId: string, partNumber: number, request: {eTag: string}) => {
                return this._uploadsApi.completePartUpload(this._workspaceExternalId, uploadExternalId, partNumber, request);
            },

            completeUpload: (uploadExternalId: string) => {
                return this._uploadsApi.completeUpload(this._workspaceExternalId, uploadExternalId);
            },

            abort: async (uploadExternalId: string) => {
                await this._foldersAndFilesSetApi.bulkDelete({
                    workspaceExternalId: this._workspaceExternalId,
                    fileUploadExternalIds: [uploadExternalId],
                    folderExternalIds: [],
                    fileExternalIds: []
                });
            }
        }
    }
}
