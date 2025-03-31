import { Component, Inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

export interface GenericMessageDialogData {
    title: string;
    message: string;
    confirmButtonText?: string;
    showCancelButton?: boolean;
    cancelButtonText?: string;
    isDanger?: boolean;
}

@Component({
    selector: 'app-generic-message-dialog',
    imports: [
        MatButtonModule
    ],
    templateUrl: './generic-message-dialog.component.html',
    styleUrl: './generic-message-dialog.component.scss'
})
export class GenericMessageDialogComponent {
    constructor(
        @Inject(MAT_DIALOG_DATA) public data: GenericMessageDialogData,
        public dialogRef: MatDialogRef<GenericMessageDialogComponent>) {
    }

    public onConfirm() {
        this.dialogRef.close(true);
    }

    public onCancel() {
        this.dialogRef.close(false);
    }
}
