import { Component, computed, Inject, signal, WritableSignal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { AppStorage, StorageItemComponent } from '../storage-item/storage-item.component';
import { ItemButtonComponent } from '../buttons/item-btn/item-btn.component';
import { Router } from '@angular/router';

@Component({
    selector: 'app-storage-picker',
    imports: [
        MatButtonModule,
        StorageItemComponent,
        ItemButtonComponent
    ],
    templateUrl: './storage-picker.component.html',
    styleUrls: ['./storage-picker.component.scss']
})
export class StoragePickerComponent {
    storages: WritableSignal<AppStorage[]> = signal([]);

    hasAnyStorage = computed(() => this.storages().length > 0);

    constructor(
        private _router: Router,
        public dialogRef: MatDialogRef<StoragePickerComponent>,
        @Inject(MAT_DIALOG_DATA) public data: {
            storages: AppStorage[],
            noStoragesMessage?: string
        }) {    
        this.storages.set(data.storages);
    }

    public onStoragePicked(storage: AppStorage) {
        this.dialogRef.close(storage);
    }

    public onCancel() {
        this.dialogRef.close();
    }

    public onConfigureStorage() {
        this._router.navigate(['/settings/storage']);
        this.dialogRef.close();
    }
}
