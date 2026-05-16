import { Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
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
        private _dataStore: DataStore,
        private _toastr: ToastrService
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
            defaultName: args.defaultName,
            appUrl: window.location.origin
        };

        // Loop so that on slug-taken we can reopen the dialog with the user's choices.
        let pending: CreateQuickShareRequest | null = null;

        while (true) {
            const dialogRef = this._dialog.open(CreateQuickShareDialogComponent, {
                width: '32rem',
                maxHeight: '90vh',
                data
            });

            pending = (await firstValueFrom(dialogRef.afterClosed())) ?? null;
            if (!pending) return null;

            try {
                const response = await this._api.createQuickShare(args.workspaceExternalId, pending);
                this._dataStore.invalidateQuickShares(args.workspaceExternalId);

                const createdData: QuickShareCreatedDialogData = {
                    name: pending.name,
                    url: response.url,
                    slug: response.slug
                };

                this._dialog.open(QuickShareCreatedDialogComponent, {
                    width: '32rem',
                    maxHeight: '90vh',
                    data: createdData,
                    disableClose: true
                });

                return response;
            } catch (error) {
                if (error instanceof HttpErrorResponse && error.status === 409) {
                    this._toastr.error('This URL is already taken. Pick a different one.');
                    // Pre-fill dialog with user's last choices on reopen
                    Object.assign(data, { defaultName: pending.name });
                    continue;
                }

                console.error(error);
                this._toastr.error('Failed to create quick share');
                return null;
            }
        }
    }
}
