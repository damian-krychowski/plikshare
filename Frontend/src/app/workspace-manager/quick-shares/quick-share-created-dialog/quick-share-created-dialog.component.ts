import { Component, Inject, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { Clipboard, ClipboardModule } from '@angular/cdk/clipboard';
import { MatSnackBar } from '@angular/material/snack-bar';

export interface QuickShareCreatedDialogData {
    name: string;
    url: string;
    accessCode: string;
}

@Component({
    selector: 'app-quick-share-created-dialog',
    imports: [
        MatButtonModule,
        ClipboardModule
    ],
    templateUrl: './quick-share-created-dialog.component.html',
    styleUrl: './quick-share-created-dialog.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class QuickShareCreatedDialogComponent {
    copied = signal(false);

    constructor(
        @Inject(MAT_DIALOG_DATA) public data: QuickShareCreatedDialogData,
        public dialogRef: MatDialogRef<QuickShareCreatedDialogComponent>,
        private _clipboard: Clipboard,
        private _snackBar: MatSnackBar
    ) {
    }

    copyLink() {
        if (this._clipboard.copy(this.data.url)) {
            this.copied.set(true);
            this._snackBar.open('Link copied to clipboard', 'Close', { duration: 2000 });
            setTimeout(() => this.copied.set(false), 2000);
        }
    }

    onClose() {
        this.dialogRef.close();
    }
}
