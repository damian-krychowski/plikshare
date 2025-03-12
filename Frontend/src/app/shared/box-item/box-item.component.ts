import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { NavigationExtras, Router } from "@angular/router";
import { EditableTxtComponent } from "../editable-txt/editable-txt.component";
import { PrefetchDirective } from "../prefetch.directive";
import { Component, WritableSignal, computed, input, output, signal } from "@angular/core";
import { BoxesSetApi } from "../../services/boxes.api";
import { MatDialog } from "@angular/material/dialog";
import { DataStore } from "../../services/data-store.service";
import { FolderPickerComponent } from "../../workspace-manager/boxes/folder-picker/folder-picker.component";
import { toggle } from "../signal-utils";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { InAppSharing } from "../../services/in-app-sharing.service";
import { AppFolderItem } from "../folder-item/folder-item.component";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";

export type AppBox = {
    externalId: WritableSignal<string | null>;
    workspaceExternalId: string;
    name: WritableSignal<string>;
    folderPath: WritableSignal<AppBoxFolderPathSegment[]>;
    isEnabled: WritableSignal<boolean>;

    isNameEditing: WritableSignal<boolean>;
    isHighlighted: WritableSignal<boolean>;
};

export type AppBoxFolderPathSegment = {
    name: string, 
    externalId:string
}

@Component({
    selector: 'app-box-item',
    imports: [
        MatSlideToggleModule,
        EditableTxtComponent,
        ConfirmOperationDirective,
        PrefetchDirective,
        ActionButtonComponent
    ],
    templateUrl: './box-item.component.html',
    styleUrl: './box-item.component.scss'
})
export class BoxItemComponent {
    box = input.required<AppBox>();
    canOpen = input(false);
    canLocate = input(false);
    
    deleted = output<void>();

    boxName = computed(() => this.box().name());
    boxExternalId = computed(() => this.box().externalId());
    workspaceExternalId = computed(() => this.box().workspaceExternalId);

    isEnabled = computed(() => this.box().isEnabled());
    isHighlighted = observeIsHighlighted(this.box);
    isNameEditing = computed(() => this.box().isNameEditing());

    nameToDisplay = computed(() => this.box().name() + (this.box().isEnabled() ? '' : ' (disabled)'));
    folderPath = computed(() => this.buildFolderPath(this.box().folderPath()));
    
    folderExternalId = computed(() => {
        const folderPath = this.box().folderPath();

        if(!folderPath || folderPath.length == 0)
            return null;

        return folderPath[folderPath.length - 1].externalId;
    });


    areActionsVisible = signal(false);

    constructor(
        private _boxesSetApi: BoxesSetApi,
        private _router: Router,
        private _dialog: MatDialog,
        private _inAppSharing: InAppSharing,
        public dataStore: DataStore) {

        }

    async changeBoxFolder() {
        const box = this.box();

        const dialogRef = this._dialog.open(FolderPickerComponent, {
            width: '700px',
            data: {
                workspaceExternalId: box.workspaceExternalId,
            },
            maxHeight: '600px',
            position: {
                top: '100px'
            }
        });

        dialogRef.afterClosed().subscribe(async (folderToShare: AppFolderItem) => {
            if(!folderToShare)
                return;

            if(this.folderExternalId() === folderToShare.externalId)
                return;

            box.folderPath.set([
                ...folderToShare.ancestors, {
                    name: folderToShare.name(),
                    externalId: folderToShare.externalId
                }
            ]);

            const boxExternalId = box.externalId();

            if(!boxExternalId)
                return;

            await this._boxesSetApi.updateBoxFolder(
                box.workspaceExternalId,
                boxExternalId, {
                folderExternalId: folderToShare.externalId
            });
        });
    }

    buildFolderPath(folderPath: AppBoxFolderPathSegment[]) {
        if(folderPath.length == 0)
            return '';

        return folderPath
            .map(segment => segment.name)
            .join(' / ');
    }

    async changeBoxIsEnabled() {
        const boxExternalId = this.boxExternalId();

        if (!boxExternalId)
            return;
            
        await this._boxesSetApi.updateBoxIsEnabled(
            this.workspaceExternalId(),
            boxExternalId, {
            isEnabled: toggle(this.box().isEnabled)
        });
    }

    async openBox() {      
        if(this.isHighlighted())
            this.box().isHighlighted.set(false);

        const boxExternalId = this.boxExternalId();

        if(!this.canOpen() || !boxExternalId || this.isNameEditing())
            return;

        this._router.navigate([
            `workspaces/${this.workspaceExternalId()}/boxes/${boxExternalId}`
        ]);
    }

    async deleteBox() {
        const boxExternalId = this.boxExternalId();

        if (!boxExternalId)
            return;

        this.deleted.emit();

        await this
            ._boxesSetApi
            .deleteBox(
                this.workspaceExternalId(), 
                boxExternalId);
    }

    async saveBoxName(newName: string) {
        const boxExternalId = this.boxExternalId();

        if(!boxExternalId)
            return;

        this.box().name.set(newName);

        await this._boxesSetApi.updateBoxName(
            this.workspaceExternalId(),
            boxExternalId, {
            name: this.boxName()
        });            
    }

    prefetchBox() {
        const box = this.box();

        this.dataStore.prefetchBox(
            box.workspaceExternalId,
            box.externalId());
    }

    prefetchBoxes() {
        this.dataStore.prefetchBoxes(
            this.workspaceExternalId());
    }

    prefetchExternalBox() {
        const boxExternalId = this.boxExternalId();

        if(!boxExternalId)
            return;

        this.dataStore.prefetchExternalBoxDetailsAndContent(
            boxExternalId);
    }

    previewBox() {
        const boxExternalId = this.boxExternalId();

        if(!boxExternalId)
            return;

        this._router.navigate([`box/${boxExternalId}`]);
    }

    locate() {
        const boxExternalId = this.boxExternalId();

        if(!boxExternalId)
            return;

        const temporaryKey = this._inAppSharing.set(boxExternalId);

        const navigationExtras: NavigationExtras = {
            state: {
                boxToHighlight: temporaryKey
            }
        };

        this._router.navigate([`/workspaces/${this.workspaceExternalId()}/boxes`], navigationExtras);
    }    

    editName() {
        if(!this.boxExternalId())
            return;

        this.box().isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }

    toggleActions() {
        this.areActionsVisible.set(!this.areActionsVisible());
    }
}