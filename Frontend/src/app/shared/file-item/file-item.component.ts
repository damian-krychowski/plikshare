import { signal, Component, WritableSignal, Signal, input, computed, output, OnInit, OnDestroy } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatTooltipModule } from "@angular/material/tooltip";
import { CtrlClickDirective } from "../ctrl-click.directive";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { EditableTxtComponent } from "../editable-txt/editable-txt.component";
import { PrefetchDirective } from "../prefetch.directive";
import { toggle } from "../signal-utils";
import { StorageSizePipe } from "../storage-size.pipe";
import { FileIconPipe } from "../../files-explorer/file-icon-pipe/file-icon.pipe";
import { InAppSharing } from "../../services/in-app-sharing.service";
import { NavigationExtras } from "@angular/router";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { FileLockService } from "../../services/file-lock.service";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";
import { ContentDisposition } from "../../services/folders-and-files.api";

export type AppFileItem = {
    type: 'file';

    externalId: string;

    folderExternalId: string | null; //todo is that needed? -simplify
    folderPath: {name: string, externalId: string}[] | null;

    name: WritableSignal<string>;
    extension: string;
    sizeInBytes: number;
    wasUploadedByUser: boolean;
    isLocked: WritableSignal<boolean>;

    isNameEditing: WritableSignal<boolean>;
    isSelected: WritableSignal<boolean>;
    isCut: WritableSignal<boolean>;
    isHighlighted: WritableSignal<boolean>;
}

export interface FileOperations {
    saveFileNameFunc: (fileExternalId: string, newName: string) => Promise<void>;
    deleteFileFunc: (fileExternalId: string) => Promise<void>;
    getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => Promise<{downloadPreSignedUrl: string}>;
    openFolderFunc: (folderExternalId: string | null, navigationExtras: NavigationExtras | null) => void;
    prefetchFolderFunc: (folderExternalId: string | null) => void;
}

@Component({
    selector: 'app-file-item',
    imports: [
        FormsModule,
        MatCheckboxModule,
        MatTooltipModule,
        StorageSizePipe,
        EditableTxtComponent,
        ConfirmOperationDirective,
        PrefetchDirective,
        CtrlClickDirective,
        FileIconPipe,
        ActionButtonComponent
    ],
    templateUrl: './file-item.component.html',
    styleUrl: './file-item.component.scss'
})
export class FileItemComponent implements OnInit, OnDestroy {
    toggle = toggle;
    
    operations = input.required<FileOperations>();
    file = input.required<AppFileItem>();
    
    canOpen = input(false);
    canSelect = input(true); //this by default is enabled
    canLocate = input(false);
    showPath = input(false);
    hideActions = input(false);

    allowDownload = input(false);
    allowMoveItems = input(false);
    allowRename = input(false);
    allowDelete = input(false);

    deleted = output<void>();
    previewed = output<void>();

    filePath = computed(() => {
        if(!this.showPath())
            return '';

        return this.buildFolderPath(this.file());
    });

    isHighlighted = observeIsHighlighted(this.file);
    
    canEditFileName = computed(() => this.file().wasUploadedByUser || this.allowRename());
    canDeleteFile = computed(() => this.file().wasUploadedByUser || this.allowDelete());
    canToggleActions = computed(() => this.file().wasUploadedByUser || this.allowDelete() || this.allowRename() || this.canLocate() || this.allowDownload());
    
    
    areActionsVisible = signal(false);
    canPreview = computed(() => AppFileItems.canPreview(this.file(), this.allowDownload(), this.canOpen()));
    
    isSelectCheckboxVisible = computed(() => this.canSelect() && (
        this.allowMoveItems() 
        || this.allowDownload() 
        || this.allowDelete()
        || this.file().wasUploadedByUser));

    constructor(
        private _inAppSharing: InAppSharing,
        private _fileLockService: FileLockService        
    ){}
   
    ngOnInit(): void {
        this._fileLockService.subscribeToLockStatus(this.file());
    }
    
    ngOnDestroy(): void {
        this._fileLockService.unsubscribe(this.file().externalId);
    }

    buildFolderPath(file: AppFileItem) {
        const path = file.folderPath;

        if(!path || path.length == 0)
            return '';

        return path
            .map(p => p.name)
            .join(' / ');
    }

    async saveFileName(newName: string) {
        const file = this.file();
        file.name.set(newName);
        
        await this.operations().saveFileNameFunc(
            file.externalId, 
            newName);
    }

    async deleteFile() {
        const file = this.file();

        if(file.isLocked())
            return;

        this.deleted.emit();

        await this.operations().deleteFileFunc(
            file.externalId);
    }

    async downloadFile() {
        const file = this.file();

        if(file.isLocked())
            return;

        const response = await this.operations().getDownloadLink(
            file.externalId,
            "attachment");

        const link = document.createElement('a');
        link.href = response.downloadPreSignedUrl;
        link.download = `${file.name()}${file.extension}`;
        link.click();
        link.remove();
    }

    locate() {        
        const file = this.file();

        if(file.isLocked())
            return;

        const temporaryKey = this._inAppSharing.set(
            file.externalId);

        const navigationExtras: NavigationExtras = {
            state: {
                fileToHighlight: temporaryKey
            }
        };

        this.operations().openFolderFunc(
            file.folderExternalId,
            navigationExtras);
    }    
    
    editName(){
        const file = this.file();

        if(file.isLocked())
            return;

        file.isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }
        
    toggleActions() {
        this.areActionsVisible.set(!this.areActionsVisible());
    }

    toggleSelection(){
        this.file().isSelected.update(value => !value);
        this.areActionsVisible.set(false);
    }

    showPreview() {
        const file = this.file();

        if(file.isLocked())
            return;

        this.previewed.emit();
    }
}

export class AppFileItems {
    public static canPreview(item: AppFileItem, allowDownload: boolean, canOpen: boolean = true) {
        return  canOpen && allowDownload && !item.isLocked();
    }
    
    public static canEdit(item: AppFileItem, allowFileEdit: boolean, canOpen: boolean = true) {
        return  canOpen && allowFileEdit && !item.isLocked() && item.extension === '.md';
    }
}