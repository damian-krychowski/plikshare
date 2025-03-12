import { Component, computed, input, output, signal, Signal, WritableSignal } from "@angular/core";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { AppStorageEncryptionType, AppStorageType, StoragesApi } from "../../services/storages.api";
import { EditableTxtComponent } from "../editable-txt/editable-txt.component";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";

export type AppStorage = {
    externalId: string;
    name: WritableSignal<string>;
    type: AppStorageType;
    encryptionType: AppStorageEncryptionType;
    details: string | null;
    workspacesCount: number;

    isNameEditing: WritableSignal<boolean>;
    isHighlighted: WritableSignal<boolean>;
}

@Component({
    selector: 'app-storage-item',
    imports: [
        ConfirmOperationDirective,
        EditableTxtComponent,
        ActionButtonComponent
    ],
    templateUrl: './storage-item.component.html',
    styleUrl: './storage-item.component.scss'
})
export class StorageItemComponent {
    storage = input.required<AppStorage>();
    pickerMode = input(false);
    
    edited = output<void>();
    deleted = output<void>();
    clicked = output<void>();
    
    isHighlighted = observeIsHighlighted(this.storage);
    isNameEditing = computed(() => this.storage().isNameEditing());
    

    areActionsVisible = signal(false);

    constructor(
        private _storagesApi: StoragesApi
    ) { }

    async editStorage() {
        if(!this.storage)
            return;

        this.edited.emit();
    }

    async deleteStorage() {
        const storage = this.storage();

        this.deleted.emit();

        await this._storagesApi.deleteStorage(
            storage.externalId);
    }

    async saveStorageName(newName: string) {
        const storage = this.storage();
        const oldName = storage.name();
        this.storage().name.set(newName);
        
        try {
            await this._storagesApi.updateName(
                storage.externalId, {
                name: newName
            });            
        } catch (err: any) {
            if(err.error?.code == 'storage-name-is-not-unique') {
                this.storage().name.set(oldName);
            } else {
                console.error(err);
            }
        }
    }

    editStorageName() {
        this.storage().isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }

    editStorageDetails() {
        this.edited.emit();
    }

    toggleActions() {
        this.areActionsVisible.update(value => !value);
    }

    onClicked() {
        this.clicked.emit();
    }
}