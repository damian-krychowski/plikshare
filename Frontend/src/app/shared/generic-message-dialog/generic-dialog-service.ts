import { Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { GenericMessageDialogComponent, GenericMessageDialogData } from './generic-message-dialog.component';

@Injectable({
    providedIn: 'root'
})
export class GenericDialogService {
    constructor(private dialog: MatDialog) {}

    /**
     * Opens a generic message dialog
     * @param config Dialog configuration options
     * @returns Observable that completes when dialog closes
     */
    public openGenericMessageDialog(config: GenericMessageDialogData): Observable<boolean> {
        const dialogRef = this.dialog.open(GenericMessageDialogComponent, {
            width: '400px',
            disableClose: true,
            data: config
        });

        return dialogRef.afterClosed();
    }

    /**
     * Opens a not enough space dialog
     * @returns Observable that completes when dialog closes
     */
    public openNotEnoughSpaceDialog(): Observable<boolean> {
        return this.openGenericMessageDialog({
            title: 'Not enough space',
            message: 'This operation cannot be performed because there is not enough space available.',
            confirmButtonText: 'Ok'
        });
    }

    /**
     * Opens a max workspaces reached dialog
     * @returns Observable that completes when dialog closes
     */
    public openMaxWorkspacesReachedDialog(): Observable<boolean> {
        return this.openGenericMessageDialog({
            title: 'Maximum workspaces reached',
            message: 'You cannot create a new workspace because you have reached the maximum number of workspaces allowed.',
            confirmButtonText: 'Ok'
        });
    }
}