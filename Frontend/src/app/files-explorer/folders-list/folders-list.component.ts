import { Component, ViewChild, computed, effect, input, output, signal, untracked } from "@angular/core";
import { SortDirection, SortMode } from "../../services/folders-and-files.api";
import { AppFolderItem, AppFolderPermissions, FolderItemComponent, FolderOperations } from "../../shared/folder-item/folder-item.component";
import { sortFoldersInPlace } from "../../services/sort-items";
import { DraggableItemDirective } from "../../shared/drag-drop/draggable-item.directive";
import { DropTargetDirective } from "../../shared/drag-drop/drop-target.directive";
import { FlipAnimationDirective } from "../../shared/drag-drop/flip-animation.directive";
import { DragStateService } from "../../services/drag-state.service";
import { computePositionForInsertion } from "../../shared/drag-drop/item-positioning.utils";
import { FilesExplorerApi } from "../files-explorer.component";

@Component({
    selector: 'app-folders-list',
    imports: [
        FolderItemComponent,
        DraggableItemDirective,
        DropTargetDirective,
        FlipAnimationDirective
    ],
    templateUrl: './folders-list.component.html',
    styleUrl: './folders-list.component.scss'
})
export class FoldersListComponent {
    sortMode = input.required<SortMode>();
    sortDirection = input.required<SortDirection>();
    folders = input.required<AppFolderItem[]>();
    searchPhrase = input.required<string>();

    currentFolderExternalId = input<string | null>(null);
    canReorder = input(false);
    operations = input.required<FolderOperations>();
    hideActions = input(false);
    permissions = input.required<AppFolderPermissions>();
    isAnyNameEditPending = input(false);
    filesApi = input.required<FilesExplorerApi>();

    deleted = output<AppFolderItem>();
    boxCreated = output<AppFolderItem>();

    @ViewChild('foldersFlip') foldersFlip?: FlipAnimationDirective;

    isSearchActive = computed(() => this.searchPhrase().length > 0);

    private _wasInitialized = false;
    localFolders = signal<AppFolderItem[]>([]);
    filteredOutFolders = signal<string[]>([]);

    hasNoListSearchMatches = computed(() =>
        this.isSearchActive()
        && this.localFolders().length === this.filteredOutFolders().length);

    private selectionAnchorExternalId: string | null = null;

    constructor(private _dragState: DragStateService) {
        effect(() => this.handleFoldersInputChange());
        effect(() => this.handleSortingInputsChange());
        effect(() => this.handleSearchPhraseInputChange());
        // effect(() => this.handleDragItemChange());
    }

    private handleFoldersInputChange() {
        const incoming = this.folders();

        untracked(() => {
            const draggedItem = this._dragState.draggedItem();
            const hasPhantom = draggedItem?.type === 'folder';

            const localFolders: AppFolderItem[] = [
                ...incoming
            ];

            if (!this._wasInitialized) {
                sortFoldersInPlace(
                    localFolders,
                    this.sortMode(),
                    this.sortDirection()
                );
            }

            // Inject phantom (if active) so drag&drop survives the sync.
            if (hasPhantom) {
                const phantomIdx = localFolders
                    .findIndex(f => f.externalId === draggedItem.folder.externalId);

                if (phantomIdx !== -1) {
                    localFolders.splice(phantomIdx, 1);
                }
                
                localFolders.unshift(draggedItem.folder);
            }

            this.localFolders.set(localFolders);

            if (!this._wasInitialized) {
                const filteredOut = this.getFilteredOutFolders(
                    localFolders,
                    this.searchPhrase());

                this.filteredOutFolders.set(filteredOut);
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

        untracked(() => sortFoldersInPlace(
            this.localFolders(),
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
            const filteredOut = this.getFilteredOutFolders(
                this.localFolders(),
                searchPhrase);

            this.filteredOutFolders.set(filteredOut);
        });
    }

    private getFilteredOutFolders(folders: AppFolderItem[], phrase: string): string[] {
        const searchPhrase = phrase.toLowerCase();

        const result = folders
            .filter(f => !f.name().toLowerCase().includes(searchPhrase))
            .map(f => f.externalId);

        return result;
    }

    onFolderSelectionToggled(folder: AppFolderItem) {
        if (folder.isSelected()) {
            this.selectionAnchorExternalId = folder.externalId;
            return;
        }

        const firstSelected = this.localFolders().find(f => f.isSelected());
        this.selectionAnchorExternalId = firstSelected?.externalId ?? null;
    }

    onFolderShiftClicked(folder: AppFolderItem) {
        const anchorId = this.selectionAnchorExternalId;

        if (!anchorId) {
            folder.isSelected.update(v => !v);
            this.onFolderSelectionToggled(folder);
            return;
        }

        const folders = this.localFolders();
        const anchorIdx = folders.findIndex(i => i.externalId === anchorId);
        const targetIdx = folders.findIndex(i => i.externalId === folder.externalId);

        if (anchorIdx === -1 || targetIdx === -1)
            return;

        const from = Math.min(anchorIdx, targetIdx);
        const to = Math.max(anchorIdx, targetIdx);

        folders.forEach((item, idx) => {
            const inRange = idx >= from && idx <= to;

            if (item.isSelected() !== inRange)
                item.isSelected.set(inRange);
        });
    }

    onFolderDragStarted(externalId: string) {
        const folders = this
            .localFolders();

        const draggedIdx = folders
            .findIndex(f => f.externalId === externalId);

        if (draggedIdx == -1)
            return;

        this._dragState.startDragging({
            type: 'folder',
            folder: folders[draggedIdx],
            parentFolderExternalId: this.currentFolderExternalId() ?? null,
            originalIndexInParentFolder: draggedIdx
        });
    }

    onFolderDragEnded() {
        const dragged = this._dragState.draggedItem();

        if (dragged == null || dragged.type !== 'folder')
            return;

        const folder = dragged.folder;
        const folders = this.localFolders();

        const draggedIdx = folders
            .findIndex(f => f.externalId === folder.externalId);

        const withoutDragged = draggedIdx === -1
            ? [...folders]
            : [...folders.slice(0, draggedIdx), ...folders.slice(draggedIdx + 1)];

        if(dragged.parentFolderExternalId === this.currentFolderExternalId()){
            const folderOriginalIdx = dragged.originalIndexInParentFolder;

            const restored = [
                ...withoutDragged.slice(0, folderOriginalIdx),
                folder,
                ...withoutDragged.slice(folderOriginalIdx)
            ];

            this.localFolders.set(restored);
        } else {
            this.localFolders.set(withoutDragged);
        }

        this._dragState.stopDragging({ reason: 'canceled' });
    }

    onFolderDragOverStay(folder: AppFolderItem) {
        const dragged = this._dragState.draggedItem();

        // Suppress only when hovering an internal drag over its own source
        // folder. Null dragged == OS-file drag, which should always drill in.
        if (dragged?.type === 'folder' && dragged.folder.externalId === folder.externalId)
            return;

        this.operations()
            .openFolderFunc(folder.externalId, null);
    }

    onFolderDragOverItem(folder: AppFolderItem, event: { position: 'before' | 'into' | 'after' }) {
        // Called on every mouse move over an item - log only on actual state change
        // (real reorder or unusual early-return). Repeated no-op invocations are silent.
        if (event.position === 'into')
            return;

        const dragged = this._dragState.draggedItem();

        if (!dragged || dragged.type !== 'folder')
            return;

        const draggedFolderExternalId = dragged.folder.externalId;
        const list = this.localFolders();
        const fromIdx = list.findIndex(f => f.externalId === draggedFolderExternalId);
        const targetIdx = list.findIndex(f => f.externalId === folder.externalId);

        if (fromIdx === -1 || targetIdx === -1)
            return;

        let toIdx = event.position === 'before'
            ? targetIdx
            : targetIdx + 1;

        if (toIdx > fromIdx) 
            toIdx -= 1;

        if (toIdx === fromIdx)
            return;
        
        this.foldersFlip?.capture();

        const next = [...list];

        const [item] = next.splice(fromIdx, 1);

        next.splice(toIdx, 0, item);

        this.localFolders.set(next);

        this.foldersFlip?.schedule();
    }

    async onFolderDroppedAt(folder: AppFolderItem, event: { position: 'before' | 'into' | 'after' }) {
        const dragged = this._dragState.draggedItem();

        if (!dragged)
            return;

        if (dragged.type === 'file') {
            if (event.position === 'into') {
                this._dragState.stopDragging({ 
                    reason: 'success', 
                    destinationFolderExternalId: folder.externalId 
                });

                await this.executeMove(
                    dragged.type,
                    dragged.file.externalId,
                    folder.externalId,
                    null);
            } else {
                // File dropped on a folder's before/after zone — no-op.
                this._dragState.stopDragging({ 
                    reason: 'canceled' 
                });
            }
            return;
        }

        if (dragged.type === 'folder') {
            const folders = this.localFolders();

            const draggedIdx = folders
                .findIndex(f => f.externalId === dragged.folder.externalId);

            if (draggedIdx === -1) {
                this._dragState.stopDragging({ 
                    reason: 'canceled' 
                });

                return;
            }

            const isTargetSelf = dragged.folder.externalId === folder.externalId;

            if (event.position === 'into' && !isTargetSelf) {
                const withoutDragged = [
                    ...folders.slice(0, draggedIdx),
                    ...folders.slice(draggedIdx + 1)];

                this.localFolders.set(withoutDragged);

                this._dragState.stopDragging({ 
                    reason: 'success', 
                    destinationFolderExternalId: folder.externalId 
                });

                await this.executeMove(
                    dragged.type,
                    dragged.folder.externalId,
                    folder.externalId,
                    null);
            } else {
                const currentFolder = this.currentFolderExternalId() ?? null;
                const isSameParentFolder = dragged.parentFolderExternalId === currentFolder;

                const newPosition = this.computePhantomDropPosition(
                    dragged.folder.externalId);

                dragged.folder.position.set(newPosition);

                this._dragState.stopDragging({ 
                    reason: 'success',
                     destinationFolderExternalId: currentFolder 
                });

                if (isSameParentFolder) {
                    await this.persistPosition(
                        dragged.folder.externalId,
                        newPosition);
                } else {
                    await this.executeMove(
                        dragged.type,
                        dragged.folder.externalId,
                        currentFolder,
                        newPosition);
                }
            }
            return;
        }

        throw new Error(`Unrecognized dragged item type ${(dragged as any).type}`);
    }

    private computePhantomDropPosition(
        phantomExternalId: string
    ): number {
        const list = this.localFolders();

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
        type: 'folder' | 'file',
        externalId: string,
        destinationFolderExternalId: string | null,
        destinationPosition: number | null
    ) {
        try {
            await this.filesApi().moveItems({
                fileExternalIds: type === 'file' ? [externalId] : [],
                folderExternalIds: type === 'folder' ? [externalId] : [],
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
                folders: [{ externalId, position }],
                files: []
            });
        } catch (error) {
            console.error(`Something went wrong, updatePositions API FAILED`, error);
        }
    }
}