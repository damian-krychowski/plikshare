import { Component, OnDestroy, ViewChild, computed, effect, input, output, signal, untracked } from "@angular/core";
import { Subscription } from "rxjs";
import { SortDirection, SortMode } from "../../services/folders-and-files.api";
import { AppFileItem, AppFilePermissions, FileItemComponent, FileOperations } from "../../shared/file-item/file-item.component";
import { sortFilesInPlace } from "../../services/sort-items";
import { DraggableItemDirective } from "../../shared/drag-drop/draggable-item.directive";
import { DropTargetDirective } from "../../shared/drag-drop/drop-target.directive";
import { FlipAnimationDirective } from "../../shared/drag-drop/flip-animation.directive";
import { DraggingStoppedEvent, DragStateService } from "../../services/drag-state.service";
import { computePositionForInsertion } from "../../shared/drag-drop/item-positioning.utils";
import { FilesExplorerApi } from "../files-explorer.component";

@Component({
    selector: 'app-files-list',
    imports: [
        FileItemComponent,
        DraggableItemDirective,
        DropTargetDirective,
        FlipAnimationDirective
    ],
    templateUrl: './files-list.component.html',
    styleUrl: './files-list.component.scss'
})
export class FilesListComponent implements OnDestroy {
    sortMode = input.required<SortMode>();
    sortDirection = input.required<SortDirection>();
    files = input.required<AppFileItem[]>();
    searchPhrase = input.required<string>();

    currentFolderExternalId = input<string | null>(null);
    canReorder = input(false);
    operations = input.required<FileOperations>();
    hideActions = input(false);
    permissions = input.required<AppFilePermissions>();
    isAnyNameEditPending = input(false);
    filesApi = input.required<FilesExplorerApi>();

    deleted = output<AppFileItem>();
    previewed = output<AppFileItem>();

    @ViewChild('filesFlip') filesFlip?: FlipAnimationDirective;

    isSearchActive = computed(() => this.searchPhrase().length > 0);

    private _wasInitialized = false;
    localFiles = signal<AppFileItem[]>([]);
    filteredOutFiles = signal<string[]>([]);

    hasNoListSearchMatches = computed(() =>
        this.isSearchActive()
        && this.localFiles().length === this.filteredOutFiles().length);

    private selectionAnchorExternalId: string | null = null;
    private _draggingStoppedSubscription: Subscription | null = null;

    constructor(private _dragState: DragStateService) {
        effect(() => this.handleFilesInputChange());
        effect(() => this.handleSortingInputsChange());
        effect(() => this.handleSearchPhraseInputChange());

        this._draggingStoppedSubscription = this._dragState.draggingStopped$
            .subscribe(event => this.onDraggingStopped(event));
    }

    ngOnDestroy(): void {
        this._draggingStoppedSubscription?.unsubscribe();
    }

    private onDraggingStopped(event: DraggingStoppedEvent) {
        if (event.item.type !== 'file')
            return;

        const file = event.item.file;
        const files = this.localFiles();
        const draggedIdx = files.findIndex(f => f.externalId === file.externalId);

        if (draggedIdx === -1)
            return;

        const currentFolder = this.currentFolderExternalId();

        if (event.reason === 'success' && event.destinationFolderExternalId === currentFolder) {
            // File ended up in this folder — keep it where the drop placed it.
            // (same-folder reorder, or cross-folder phantom that landed here)
            return;
        }

        const withoutDragged = [
            ...files.slice(0, draggedIdx),
            ...files.slice(draggedIdx + 1)
        ];

        const isSourceFolder = event.item.parentFolderExternalId === currentFolder;

        if (event.reason === 'canceled' && isSourceFolder) {
            // Drag aborted in source folder — undo any drag-over reordering.
            const originalIdx = event.item.originalIndexInParentFolder;

            this.localFiles.set([
                ...withoutDragged.slice(0, originalIdx),
                file,
                ...withoutDragged.slice(originalIdx)
            ]);
            return;
        }

        // Either: source folder success but file moved out (drop on another folder),
        // or foreign-folder phantom that didn't land here (success elsewhere / canceled).
        // Either way the file should not be in this list anymore.
        this.localFiles.set(withoutDragged);
    }

    private handleFilesInputChange() {
        const incoming = this.files();

        untracked(() => {
            const draggedItem = this._dragState.draggedItem();
            const hasPhantom = draggedItem?.type === 'file';

            const localFiles: AppFileItem[] = [
                ...incoming
            ];

            if (!this._wasInitialized) {
                sortFilesInPlace(
                    localFiles,
                    this.sortMode(),
                    this.sortDirection()
                );
            }

            // Inject phantom (if active) so drag&drop survives the sync.
            if (hasPhantom) {
                const phantomIdx = localFiles
                    .findIndex(f => f.externalId === draggedItem.file.externalId);

                if (phantomIdx !== -1) {
                    localFiles.splice(phantomIdx, 1);
                }
                
                localFiles.unshift(draggedItem.file);
            }

            this.localFiles.set(localFiles);

            if (!this._wasInitialized) {
                const filteredOut = this.getFilteredOutFiles(
                    localFiles,
                    this.searchPhrase());

                this.filteredOutFiles.set(filteredOut);
            }

            this._wasInitialized = true;
        });
    }

    private handleSortingInputsChange() {
        const sortMode = this.sortMode();
        const sortDirection = this.sortDirection();

        if (!this._wasInitialized)
            return;

        if (this._dragState.isDragging()) {
            return;
        }

        untracked(() => sortFilesInPlace(
            this.localFiles(),
            sortMode,
            sortDirection
        ));
    }

    private handleSearchPhraseInputChange() {
        const searchPhrase = this.searchPhrase();

        if (!this._wasInitialized)
            return;

        if (this._dragState.isDragging()) {
            return;
        }

        untracked(() => {
            const filteredOut = this.getFilteredOutFiles(
                this.localFiles(),
                searchPhrase);

            this.filteredOutFiles.set(filteredOut);
        });
    }

    private getFilteredOutFiles(files: AppFileItem[], phrase: string): string[] {
        const searchPhrase = phrase.toLowerCase();

        const result = files
            .filter(f => !(f.name() + f.extension).toLowerCase().includes(searchPhrase))
            .map(f => f.externalId);

        return result;
    }

    onFileSelectionToggled(file: AppFileItem) {
        if (file.isSelected()) {
            this.selectionAnchorExternalId = file.externalId;
            return;
        }

        const firstSelected = this.localFiles().find(f => f.isSelected());
        this.selectionAnchorExternalId = firstSelected?.externalId ?? null;
    }

    onFileShiftClicked(file: AppFileItem) {
        const anchorId = this.selectionAnchorExternalId;

        if (!anchorId) {
            file.isSelected.update(v => !v);
            this.onFileSelectionToggled(file);
            return;
        }

        const files = this.localFiles();
        const anchorIdx = files.findIndex(i => i.externalId === anchorId);
        const targetIdx = files.findIndex(i => i.externalId === file.externalId);

        if (anchorIdx === -1 || targetIdx === -1)
            return;

        const from = Math.min(anchorIdx, targetIdx);
        const to = Math.max(anchorIdx, targetIdx);

        files.forEach((item, idx) => {
            const inRange = idx >= from && idx <= to;

            if (item.isSelected() !== inRange)
                item.isSelected.set(inRange);
        });
    }

    onFileDragStarted(externalId: string) {
        const files = this
            .localFiles();

        const draggedIdx = files
            .findIndex(f => f.externalId === externalId);

        if (draggedIdx == -1)
            return;

        this._dragState.startDragging({
            type: 'file',
            file: files[draggedIdx],
            parentFolderExternalId: this.currentFolderExternalId() ?? null,
            originalIndexInParentFolder: draggedIdx
        });
    }

    onFileDragOverItem(file: AppFileItem, event: { position: 'before' | 'into' | 'after' }) {
        if (event.position === 'into')
            return;

        const dragged = this._dragState.draggedItem();

        if (!dragged || dragged.type !== 'file')
            return;

        const draggedFileExternalId = dragged.file.externalId;
        const list = this.localFiles();
        const fromIdx = list.findIndex(f => f.externalId === draggedFileExternalId);
        const targetIdx = list.findIndex(f => f.externalId === file.externalId);

        if (fromIdx === -1 || targetIdx === -1)
            return;

        let toIdx = event.position === 'before'
            ? targetIdx
            : targetIdx + 1;

        if (toIdx > fromIdx)
            toIdx -= 1;

        if (toIdx === fromIdx)
            return;

        this.filesFlip?.capture();

        const next = [...list];

        const [item] = next.splice(fromIdx, 1);

        next.splice(toIdx, 0, item);

        this.localFiles.set(next);

        this.filesFlip?.schedule();
    }

    async onFileDroppedAt(file: AppFileItem, event: { position: 'before' | 'into' | 'after' }) {
        if (event.position === 'into')
            return;

        const dragged = this._dragState.draggedItem();

        if (!dragged || dragged.type !== 'file')
            return;

        const files = this.localFiles();

        const draggedIdx = files
            .findIndex(f => f.externalId === dragged.file.externalId);

        if (draggedIdx === -1) {
            this._dragState.stopDragging({ reason: 'canceled' });
            return;
        }

        const currentFolder = this.currentFolderExternalId() ?? null;
        const isSameParentFolder = dragged.parentFolderExternalId === currentFolder;

        const newPosition = this.computePhantomDropPosition(
            dragged.file.externalId);

        dragged.file.position.set(newPosition);

        // Read all local state needed before stopDragging — subscribers may mutate localFiles.
        this._dragState.stopDragging({ 
            reason: 'success', 
            destinationFolderExternalId: currentFolder 
        });

        if (isSameParentFolder) {
            await this.persistPosition(
                dragged.file.externalId,
                newPosition);
        } else {
            await this.executeMove(
                dragged.file.externalId,
                this.currentFolderExternalId() ?? null,
                newPosition);
        }
    }

    private computePhantomDropPosition(
        phantomExternalId: string
    ): number {
        const list = this.localFiles();

        const idx = list
            .findIndex(f => f.externalId === phantomExternalId);

        const neighbors = list
            .filter(f => f.externalId !== phantomExternalId);

        const insertionIdx = idx === -1
            ? neighbors.length
            : Math.min(idx, neighbors.length);

        const result = computePositionForInsertion(
            neighbors,
            insertionIdx,
            item => item.position());

        return result;
    }

    private async executeMove(
        externalId: string,
        destinationFolderExternalId: string | null,
        destinationPosition: number | null
    ) {
        try {
            await this.filesApi().moveItems({
                fileExternalIds: [externalId],
                folderExternalIds: [],
                fileUploadExternalIds: [],
                destinationFolderExternalId,
                destinationPosition
            });

            this.filesApi().invalidatePrefetchedEntries();
        } catch (error) {
            console.error(`Something went wrong, moveItems API FAILED`, error);
        }
    }

    private async persistPosition(externalId: string, position: number) {
        const api = this.filesApi();

        if (!api.updatePositions)
            return;

        try {
            await api.updatePositions({
                parentFolderExternalId: this.currentFolderExternalId() ?? null,
                folders: [],
                files: [{ externalId, position }]
            });
        } catch (error) {
            console.error(`Something went wrong, updatePositions API FAILED`, error);
        }
    }
}
