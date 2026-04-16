import { Component, ViewEncapsulation } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
    selector: 'app-confirm-lock-encryption',
    imports: [MatButtonModule],
    encapsulation: ViewEncapsulation.None,
    styleUrl: './confirm-lock-encryption.component.scss',
    template: `
        <div class="questionaire">
            <div class="questionaire__title">
                Lock encryption?
            </div>

            <div class="questionaire__info">
                You will no longer be able to access fully encrypted workspaces until you unlock again
                by entering your encryption password.
            </div>

            <div class="questionaire__actions">
                <button type="button" class="questionaire__btn mr-1" mat-flat-button
                    (click)="dialogRef.close(false)">
                    Cancel
                </button>

                <button type="button" class="questionaire__btn questionaire__btn--danger" mat-flat-button
                    (click)="dialogRef.close(true)">
                    Lock
                </button>
            </div>
        </div>
    `
})
export class ConfirmLockEncryptionComponent {
    constructor(public dialogRef: MatDialogRef<ConfirmLockEncryptionComponent, boolean>) {}
}
