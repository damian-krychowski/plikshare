import { Component, Signal, WritableSignal, computed, input, output, signal } from "@angular/core";
import { MatTooltipModule } from "@angular/material/tooltip";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { EditableTxtComponent } from "../editable-txt/editable-txt.component";
import { PrefetchDirective } from "../prefetch.directive";
import { CtrlClickDirective } from "../ctrl-click.directive";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { InAppSharing } from "../../services/in-app-sharing.service";
import { NavigationExtras } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { ActionButtonComponent, CountdownTime } from "../buttons/action-btn/action-btn.component";
import { TimeService } from "../../services/time.service";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";

type PermissionState = {
    isOn: boolean;
    timeLeft: CountdownTime | null;
}

export type AppFolderItem = {
    type: 'folder';

    externalId: string;
    name: WritableSignal<string>;
    ancestors: AppFolderAncestor[];
    
    isNameEditing: WritableSignal<boolean>;
    isSelected: WritableSignal<boolean>;
    isCut: WritableSignal<boolean>;
    isHighlighted: WritableSignal<boolean>;

    wasCreatedByUser: boolean;
    createdAt: Date | null;
}

export type AppFolderAncestor = {
    externalId: string;
    name: string;
}

export interface FolderOperations {
    saveFolderNameFunc: (folderExternalId: string | null, newName: string) => Promise<void>;
    prefetchFolderFunc: (folderExternalId: string | null) => void;
    openFolderFunc: (folderExternalId: string | null, navigationExtras: NavigationExtras | null) => void;
    deleteFolderFunc: (folderExternalId: string | null) => Promise<void>;
}

const TIME_TO_RENAME_FOLDER_WITHOUT_PERMISSION_MS = 5 * 60 * 1000;

@Component({
    selector: 'app-folder-item',
    imports: [
        FormsModule,
        MatCheckboxModule,
        MatTooltipModule,
        EditableTxtComponent,
        ConfirmOperationDirective,
        PrefetchDirective,
        CtrlClickDirective,
        ActionButtonComponent
    ],
    templateUrl: './folder-item.component.html',
    styleUrl: './folder-item.component.scss'
})
export class FolderItemComponent{    
    operations = input.required<FolderOperations>();
    folder = input.required<AppFolderItem>();
    
    canOpen = input(false);
    canLocate = input(false);
    showPath = input(false);
    hideActions = input(false);

    allowShare = input(false);
    allowMoveItems = input(false);
    allowRename = input(false);
    allowDelete = input(false);
    allowDownload = input(false);

    deleted = output<void>();
    boxCreated = output<void>();

    folderName = computed(() => this.folder().name());
    folderPath = computed(() => this.buildFolderPath(this.folder()));
    areActionsVisible = signal(false);

    isNameEditing = computed(() => this.folder().isNameEditing());
    isCut = computed(() => this.folder().isCut());
    isSelected = computed(() => this.folder().isSelected());
    isHighlighted = observeIsHighlighted(this.folder);
    canToggleActions = computed(() => this.allowDelete() || this.allowShare() || this.allowRename() || this.canLocate());

    canEditName: Signal<PermissionState> = computed(() => {
        if (this.allowRename()) {
            return {
                isOn: true,
                timeLeft: null
            };
        }

        const wasCreatedByUser = this.folder().wasCreatedByUser;

        if(!wasCreatedByUser)
            return {
                isOn: false,
                timeLeft: null
            };
    
        const createdAt = this.folder().createdAt;

        if(createdAt == null)
            return {
                isOn: false,
                timeLeft: null
            };

        const currentTime = this._time.currentTime();
        const createdAtTime = createdAt.getTime();
        const elapsedTime = currentTime - createdAtTime;

        if(elapsedTime > TIME_TO_RENAME_FOLDER_WITHOUT_PERMISSION_MS)
            return {
                isOn: false,
                timeLeft: null
            };

        const timeLeft = TIME_TO_RENAME_FOLDER_WITHOUT_PERMISSION_MS - elapsedTime;

        return {
            isOn: true,
            timeLeft: {
                left: timeLeft > TIME_TO_RENAME_FOLDER_WITHOUT_PERMISSION_MS ? TIME_TO_RENAME_FOLDER_WITHOUT_PERMISSION_MS : timeLeft,
                total: TIME_TO_RENAME_FOLDER_WITHOUT_PERMISSION_MS
            }
        };
    });

    folderExternalId = computed(() => this.folder().externalId);
    
    parentFolderExternalId = computed(() => {
        const ancestors = this.folder().ancestors;

        if(!ancestors || ancestors.length == 0)
            return null;

        return ancestors[ancestors.length - 1].externalId;
    });

    constructor(
        private _inAppSharing: InAppSharing,
        private _time: TimeService
    ) {}


    buildFolderPath(folder: AppFolderItem) {
        const ancestors = folder.ancestors;

        return ancestors.map(a => a.name).join("/");
    }

    async saveFolderName(newName: string) {
        this.folder().name.set(newName);
        
        await this.operations().saveFolderNameFunc(
            this.folderExternalId(), 
            newName);
    }

    async deleteFolder() {
        this.deleted.emit();

        await this.operations().deleteFolderFunc(
            this.folderExternalId());
    }

    createBox() {
        this.boxCreated.emit();
    }

    openFolder() {
        if(!this.canOpen() || this.isNameEditing()) 
            return;

        this.operations().openFolderFunc(
            this.folderExternalId(),
            null);
    }


    locate() {
        if(this.isNameEditing())
            return;

        const temporaryKey = this._inAppSharing.set(            
            this.folderExternalId());

        const navigationExtras: NavigationExtras = {
            state: {
                folderToHighlight: temporaryKey
            }
        };

        this.operations().openFolderFunc(
            this.parentFolderExternalId(), 
            navigationExtras);
    }

    editName() {
        this.folder().isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }
    
    toggleActions() {
        this.areActionsVisible.set(!this.areActionsVisible());
    }

    toggleSelection() {
        this.folder().isSelected.update(value => !value);
        this.areActionsVisible.set(false);
    }
}