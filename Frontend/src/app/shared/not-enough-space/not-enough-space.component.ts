import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogRef } from '@angular/material/dialog';

@Component({
    selector: 'app-not-enough-space',
    imports: [
        MatButtonModule
    ],
    templateUrl: './not-enough-space.component.html',
    styleUrl: './not-enough-space.component.scss'
})
export class NotEnoughSpaceComponent {
    public item: string | null = null;
    public verb: string = 'delete';
    public isDanger: boolean = false;

    constructor(
        public dialogRef: MatDialogRef<NotEnoughSpaceComponent>) {
    }

    public onConfirm() {
        this.dialogRef.close(true);
    }

    public onCancel() {
        this.dialogRef.close(false);
    }
}
