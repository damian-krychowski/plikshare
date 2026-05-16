import { Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { CreateQuickShareDialogComponent, CreateQuickShareDialogData } from './create-quick-share-dialog/create-quick-share-dialog.component';
import { QuickShareCreatedDialogComponent, QuickShareCreatedDialogData } from './quick-share-created-dialog/quick-share-created-dialog.component';
import { CreateQuickShareRequest, CreateQuickShareResponse, QuickSharesApi } from '../../services/quick-shares.api';
import { DataStore } from '../../services/data-store.service';

@Injectable({
    providedIn: 'root'
})
export class QuickShareCreationService {
    constructor(
        private _dialog: MatDialog,
        private _api: QuickSharesApi,
        private _dataStore: DataStore
    ) {
    }

    public async openCreateDialog(args: {
        workspaceExternalId: string;
        selectedFiles: string[];
        selectedFolders: string[];
        excludedFiles: string[];
        excludedFolders: string[];
        defaultName: string;
    }): Promise<CreateQuickShareResponse | null> {
        const data: CreateQuickShareDialogData = {
            selectedFiles: args.selectedFiles,
            selectedFolders: args.selectedFolders,
            excludedFiles: args.excludedFiles,
            excludedFolders: args.excludedFolders,
            defaultName: args.defaultName
        };

        const dialogRef = this._dialog.open(CreateQuickShareDialogComponent, {
            width: '32rem',
            maxHeight: '90vh',
            data
        });

        const request: CreateQuickShareRequest | null | undefined = await firstValueFrom(dialogRef.afterClosed());

        if (!request) return null;

        const response = await this._api.createQuickShare(args.workspaceExternalId, request);

        this._dataStore.invalidateQuickShares(args.workspaceExternalId);

        const createdData: QuickShareCreatedDialogData = {
            name: request.name,
            url: response.url,
            accessCode: response.accessCode
        };

        this._dialog.open(QuickShareCreatedDialogComponent, {
            width: '32rem',
            maxHeight: '90vh',
            data: createdData,
            disableClose: true
        });

        return response;
    }
}
