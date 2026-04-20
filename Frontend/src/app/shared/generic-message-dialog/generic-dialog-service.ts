import { Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { GenericMessageDialogComponent, GenericMessageDialogData } from './generic-message-dialog.component';

@Injectable({
    providedIn: 'root'
})
export class GenericDialogService {
    constructor(private dialog: MatDialog) {}

    public openGenericMessageDialog(config: GenericMessageDialogData): Observable<boolean> {
        const dialogRef = this.dialog.open(GenericMessageDialogComponent, {
            width: '400px',
            disableClose: true,
            data: config
        });

        return dialogRef.afterClosed();
    }

    public openNotEnoughSpaceDialog(): Observable<boolean> {
        return this.openGenericMessageDialog({
            title: 'Not enough space',
            message: 'This operation cannot be performed because there is not enough space available.',
            confirmButtonText: 'Ok'
        });
    }

    public openMaxWorkspacesReachedDialog(): Observable<boolean> {
        return this.openGenericMessageDialog({
            title: 'Maximum workspaces reached',
            message: 'You cannot create a new workspace because you have reached the maximum number of workspaces allowed.',
            confirmButtonText: 'Ok'
        });
    }

    
    public openMaxTeamMembersReachedDialog(): Observable<boolean> {
        return this.openGenericMessageDialog({
            title: 'Maximum team members reached',
            message: 'You cannot invite more team members because you have reached the maximum allowed number for your workspace.',
            confirmButtonText: 'Ok'
        });
    }

    public openPendingKeyGrantDialog(): Observable<boolean> {
        return this.openGenericMessageDialog({
            title: 'Waiting for workspace owner approval',
            message: 'You were invited to this encrypted workspace but the owner has not yet granted you an encryption key. The owner has been notified — you will be able to access the workspace once they approve your request.',
            confirmButtonText: 'Ok'
        });
    }

    public openEncryptionAccessGrantedDialog(memberEmail: string): Observable<boolean> {
        return this.openGenericMessageDialog({
            title: 'Encryption access granted',
            message: `Encryption access was granted to ${memberEmail}. They have been notified by email and can now enter the workspace.`,
            confirmButtonText: 'Ok'
        });
    }
}