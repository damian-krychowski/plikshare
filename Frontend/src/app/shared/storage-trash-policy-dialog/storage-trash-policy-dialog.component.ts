import { Component, ViewEncapsulation, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { ToastrService } from 'ngx-toastr';
import { StoragesApi } from '../../services/storages.api';
import { TrashPolicyConfigChangedEvent, TrashPolicyConfigComponent } from '../trash-policy-config/trash-policy-config.component';
import { AppStorage } from '../storage-item/storage-item.component';

@Component({
    selector: 'app-storage-trash-policy-dialog',
    imports: [
        MatButtonModule,
        TrashPolicyConfigComponent
    ],
    templateUrl: './storage-trash-policy-dialog.component.html',
    styleUrl: './storage-trash-policy-dialog.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class StorageTrashPolicyDialogComponent {
    private _storagesApi = inject(StoragesApi);
    private _toastr = inject(ToastrService);

    dialogRef = inject(MatDialogRef<StorageTrashPolicyDialogComponent>);
    storage = inject<AppStorage>(MAT_DIALOG_DATA);

    async onTrashPolicyChange(event: TrashPolicyConfigChangedEvent) {
        const previous = this.storage.defaultTrashPolicy();

        // Optimistic — the config component only emits a validated policy.
        this.storage.defaultTrashPolicy.set(event.trashPolicy);

        try {
            await this._storagesApi.updateDefaultTrashPolicy(
                this.storage.externalId,
                event.trashPolicy);
        } catch (err) {
            console.error('Failed to update storage default trash policy', err);
            this.storage.defaultTrashPolicy.set(previous);
            this._toastr.error('Could not update the default trash policy.');
        }
    }

    close() {
        this.dialogRef.close();
    }
}
