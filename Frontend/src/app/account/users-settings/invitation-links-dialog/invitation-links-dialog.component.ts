import { Component, Inject, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { ClipboardModule, Clipboard } from '@angular/cdk/clipboard';
import { MatSnackBar } from '@angular/material/snack-bar';

export interface InvitationLinkRow {
    email: string;
    invitationLink: string;
}

export interface InvitationLinksDialogData {
    links: InvitationLinkRow[];
}

@Component({
    selector: 'app-invitation-links-dialog',
    imports: [
        MatButtonModule,
        MatCheckboxModule,
        ClipboardModule
    ],
    templateUrl: './invitation-links-dialog.component.html',
    styleUrl: './invitation-links-dialog.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class InvitationLinksDialogComponent {
    hasSaved = signal(false);
    copiedEmail = signal<string | null>(null);

    constructor(
        @Inject(MAT_DIALOG_DATA) public data: InvitationLinksDialogData,
        public dialogRef: MatDialogRef<InvitationLinksDialogComponent>,
        private _clipboard: Clipboard,
        private _snackBar: MatSnackBar) {
    }

    copyLink(row: InvitationLinkRow) {
        if (this._clipboard.copy(row.invitationLink)) {
            this.copiedEmail.set(row.email);
            setTimeout(() => {
                if (this.copiedEmail() === row.email) {
                    this.copiedEmail.set(null);
                }
            }, 2000);

            this._snackBar.open(`Link for ${row.email} copied to clipboard`, 'Close', {
                duration: 2000
            });
        }
    }

    copyAll() {
        const text = this.data.links
            .map(r => `${r.email}: ${r.invitationLink}`)
            .join('\n');

        if (this._clipboard.copy(text)) {
            this._snackBar.open('All invitation links copied to clipboard', 'Close', {
                duration: 2000
            });
        }
    }

    onDone() {
        if (this.hasSaved()) {
            this.dialogRef.close(true);
        }
    }
}
