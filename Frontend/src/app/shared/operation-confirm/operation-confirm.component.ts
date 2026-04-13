import { Component, Inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

@Component({
    selector: 'app-operation-confirm',
    imports: [
        MatButtonModule
    ],
    templateUrl: './operation-confirm.component.html',
    styleUrl: './operation-confirm.component.scss'
})
export class OperationConfirmComponent {
    public item: string | null = null;
    public verb: string = 'delete';
    public isDanger: boolean = false;
    public subtitle: string = 'This operation cannot be reverted.';

    constructor(
        public dialogRef: MatDialogRef<OperationConfirmComponent>,
        @Inject(MAT_DIALOG_DATA) public data: {
            item: string,
            verb: string,
            isDanger: boolean,
            subtitle?: string
        }) {

        this.item = data.item;
        this.verb = data.verb;
        this.isDanger = data.isDanger;

        if (data.subtitle) {
            this.subtitle = data.subtitle;
        }
    }

    public onConfirm() {
        this.dialogRef.close(true);
    }

    public onCancel() {
        this.dialogRef.close(false);
    }
}
