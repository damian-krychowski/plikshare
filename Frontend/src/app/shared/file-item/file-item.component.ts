import { signal, Component, WritableSignal, input, computed, output, OnInit, OnDestroy, HostListener } from "@angular/core";
import { DatePipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatTooltipModule } from "@angular/material/tooltip";
import { CtrlClickDirective } from "../ctrl-click.directive";
import { EditableTxtComponent } from "../editable-txt/editable-txt.component";
import { PrefetchDirective } from "../prefetch.directive";
import { toggle } from "../signal-utils";
import { StorageSizePipe } from "../storage-size.pipe";
import { FileIconPipe } from "../../files-explorer/file-icon-pipe/file-icon.pipe";
import { InAppSharing } from "../../services/in-app-sharing.service";
import { NavigationExtras } from "@angular/router";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";
import { ContentDisposition } from "../../services/folders-and-files.api";
import { DragStateService } from "../../services/drag-state.service";

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
    createdAt: Date | null;
    position: WritableSignal<number>;

    // Set only by the folder-listing mapper (mapFileDtosToItems); other construction sites
    // leave it undefined (no thumbnails surfaced there).
    hasMiniThumbnail?: boolean;

    isNameEditing: WritableSignal<boolean>;
    isSelected: WritableSignal<boolean>;
    isCut: WritableSignal<boolean>;
    isHighlighted: WritableSignal<boolean>;
}

export type AppFilePermissions = {
    allowMoveItems: boolean;
    allowRename: boolean;
    allowDelete: boolean;
    allowDownload: boolean;
}

export interface FileOperations {
    saveFileNameFunc: (fileExternalId: string, newName: string) => Promise<void>;
    deleteFileFunc: (fileExternalId: string) => Promise<void>;
    getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => Promise<{downloadPreSignedUrl: string}>;
    // Stable, cookie-authenticated URL of a file's Mini thumbnail for use as an <img src>.
    // Optional — only providers that surface a workspace context (the routed explorer) supply it.
    getThumbnailUrl?: (fileExternalId: string) => string;
    openFolderFunc: (folderExternalId: string | null, navigationExtras: NavigationExtras | null) => void;
    prefetchFolderFunc: (folderExternalId: string | null) => void;
    subscribeToLockStatus: (file: AppFileItem) => void;
    unsubscribeFromLockStatus: (fileExternalId: string) => void;
}

@Component({
    selector: 'app-file-item',
    imports: [
        FormsModule,
        MatCheckboxModule,
        MatTooltipModule,
        StorageSizePipe,
        EditableTxtComponent,
        PrefetchDirective,
        CtrlClickDirective,
        FileIconPipe,
        ActionButtonComponent,
        DatePipe
    ],
    templateUrl: './file-item.component.html',
    styleUrl: './file-item.component.scss'
})
export class FileItemComponent implements OnInit, OnDestroy {
    toggle = toggle;

    operations = input.required<FileOperations>();
    file = input.required<AppFileItem>();

    canSelect = input(true); //this by default is enabled
    canLocate = input(false);
    showPath = input(false);
    hideActions = input(false);
    hideDownloadAction = input(false);
    hideSelectCheckbox = input(false);
    canReorder = input(false);
    showThumbnails = input(false);
    highlightPhrase = input<string>('');

    permissions = input<AppFilePermissions>({
        allowDelete: false,
        allowDownload: false,
        allowMoveItems: false,
        allowRename: false
    });

    deleted = output<void>();
    previewed = output<void>();
    selectionToggled = output<void>();
    shiftClicked = output<void>();

    filePath = computed(() => {
        if(!this.showPath())
            return '';

        return this.buildFolderPath(this.file());
    });

    isHighlighted = observeIsHighlighted(this.file);

    // URLs whose <img> failed to load (404 race after a delete, transient error). Once failed we
    // fall back to the file-type icon. Keyed by URL so a virtualized row reused for another file
    // is unaffected.
    private _failedThumbnailUrls = signal<ReadonlySet<string>>(new Set<string>());

    miniThumbnailUrl = computed(() => {
        if (!this.showThumbnails())
            return null;

        const file = this.file();

        if (!file.hasMiniThumbnail)
            return null;

        const url = this.operations().getThumbnailUrl?.(file.externalId) ?? null;

        if (!url || this._failedThumbnailUrls().has(url))
            return null;

        return url;
    });

    canEditFileName = computed(() => this.file().wasUploadedByUser || this.permissions().allowRename);
    canDeleteFile = computed(() => this.file().wasUploadedByUser || this.permissions().allowDelete);

    canPreview = computed(() => AppFileItems.canPreview(this.file(), this.permissions().allowDownload));

    isSelectCheckboxVisible = computed(() => {
        if (!this.canSelect())
            return false;

        if (this.hideSelectCheckbox())
            return false;

        if (this.file().wasUploadedByUser)
            return true;

        const permissions = this.permissions();

        return permissions.allowMoveItems
            || permissions.allowDownload
            || permissions.allowDelete;
    });

    constructor(
        private _inAppSharing: InAppSharing,
        public dragState: DragStateService
    ){}

    ngOnInit(): void {
        this.operations().subscribeToLockStatus(this.file());
    }

    ngOnDestroy(): void {
        this.operations().unsubscribeFromLockStatus(this.file().externalId);
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
    }

    private _lastMouseDownShift = false;

    @HostListener('mousedown', ['$event'])
    onHostMouseDown(event: MouseEvent) {
        // Capture shift state at the start of any interaction. The most
        // recent mousedown's shift wins; toggleSelection consumes it.
        this._lastMouseDownShift = event.shiftKey;
    }

    toggleSelection() {
        if (this._lastMouseDownShift) {
            this._lastMouseDownShift = false;
            this.shiftClicked.emit();
            return;
        }

        this.file().isSelected.update(value => !value);
        this.selectionToggled.emit();
    }

    showPreview() {
        const file = this.file();

        if(file.isLocked())
            return;

        this.previewed.emit();
    }

    onThumbnailError(url: string) {
        this._failedThumbnailUrls.update(failed => {
            const next = new Set(failed);
            next.add(url);
            return next;
        });
    }
}

export class AppFileItems {
    public static canPreview(item: AppFileItem, allowDownload: boolean) {
        return allowDownload && !item.isLocked();
    }

    public static canEdit(item: AppFileItem, allowFileEdit: boolean) {
        return allowFileEdit && !item.isLocked() && item.extension === '.md';
    }
}
