import { Component, ViewChild, computed, effect, input, output, signal, untracked } from "@angular/core";
import { SortDirection, SortMode } from "../../services/folders-and-files.api";
import { AppFolderItem, AppFolderPermissions, FolderItemComponent, FolderOperations } from "../../shared/folder-item/folder-item.component";
import { sortFoldersInPlace } from "../../services/sort-items";
import { DraggableItemDirective } from "../../shared/drag-drop/draggable-item.directive";
import { DropTargetDirective } from "../../shared/drag-drop/drop-target.directive";
import { FlipAnimationDirective } from "../../shared/drag-drop/flip-animation.directive";
import { DragStateService, getDraggedExternalId } from "../../services/drag-state.service";
import { computePositionForInsertion } from "../../shared/drag-drop/item-positioning.utils";
import { FilesExplorerApi } from "../files-explorer.component";

const DND_LOG_PREFIX = '[FoldersListDnD]';

type PhantomFolder = {
    folder: AppFolderItem;
    originalParentFolderExternalId: string | null;
    originalIndexInParentFolder: number;
}

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
    moved = output<void>();

    @ViewChild('foldersFlip') foldersFlip?: FlipAnimationDirective;

    isSearchActive = computed(() => this.searchPhrase().length > 0);

    private _wasInitialized = false;
    localFolders = signal<AppFolderItem[]>([]);
    filteredOutFolders = signal<string[]>([]);

    private _phantom: PhantomFolder | null = null;

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
            const hasPhantom = !!this._phantom;

            const localFolders: AppFolderItem[] = [];

            for (const folder of incoming) {

                // Skip the phantom's source folder if it appears in incoming -
                // the phantom item itself represents it in the rendered list.
                if (hasPhantom && folder.externalId === this._phantom?.folder.externalId) {
                    continue;
                }

                localFolders.push(folder);
            }

            if (!this._wasInitialized) {
                sortFoldersInPlace(
                    localFolders,
                    this.sortMode(),
                    this.sortDirection()
                );
            }

            // Inject phantom (if active) so drag&drop survives the sync.
            if (hasPhantom) {
                const phantom: AppFolderItem = {
                    ...this._phantom!.folder,
                    position: signal(0)
                };

                localFolders.unshift(phantom);
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

        if (this._phantom) {
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

        if (this._phantom) {
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

    onFolderCtrlClicked(folder: AppFolderItem) {
        this.selectionAnchorExternalId = folder.externalId;
    }

    onFolderShiftClicked(folder: AppFolderItem) {
        if (!this.selectionAnchorExternalId) {
            folder.isSelected.update(v => !v);
            this.selectionAnchorExternalId = folder.externalId;
            return;
        }

        const folders = this.localFolders();

        const anchorIdx = folders.findIndex(
            i => i.externalId === this.selectionAnchorExternalId);

        const targetIdx = folders.findIndex(
            i => i.externalId === folder.externalId);

        if (anchorIdx === -1 || targetIdx === -1)
            return;

        const [from, to] = anchorIdx <= targetIdx
            ? [anchorIdx, targetIdx]
            : [targetIdx, anchorIdx];

        folders.forEach((item, idx) => {
            const inRange = idx >= from && idx <= to;

            if (item.isSelected() !== inRange)
                item.isSelected.set(inRange);
        });
    }

    onFolderDragStarted(externalId: string) {
        console.log(`${DND_LOG_PREFIX} onItemDragStarted CALLED`, {
            externalId,
            currentFolderExternalId: this.currentFolderExternalId() ?? null,
            localFoldersCount: this.localFolders().length
        });

        const folders = this
            .localFolders();

        const draggedIdx = folders
            .findIndex(f => f.externalId === externalId);

        if (draggedIdx == -1) {
            console.warn(`${DND_LOG_PREFIX} onItemDragStarted: folder NOT FOUND in localFolders`, {
                externalId,
                availableIds: this.localFolders().map(f => f.externalId)
            });

            return;
        }

        const dragged = folders[draggedIdx];

        this._phantom = {
            folder: dragged,
            originalIndexInParentFolder: draggedIdx,
            originalParentFolderExternalId: this.currentFolderExternalId()
        };

        console.log(`${DND_LOG_PREFIX} onItemDragStarted: folder found, setting drag state`, {
            phantom: this._phantom,
        });

        const next = [...folders];

        next[draggedIdx] = {
            ...dragged,
        };

        this.localFolders.set(next);

        this._dragState.draggedItem.set({
            type: 'folder',
            folder: dragged,
            parentFolderExternalId: this.currentFolderExternalId() ?? null
        });

        this._dragState.isDragging.set(true);

        console.log(`${DND_LOG_PREFIX} onItemDragStarted: drag state set, isDragging=true`);
    }

    onFolderDragEnded() {
        if(this._phantom == null) {
            throw new Error("Item drag ended but phantom is null");
        }

        console.log(`${DND_LOG_PREFIX} onItemDragEnded CALLED`, {
            currentDraggedItem: this._dragState.draggedItem(),
            currentIsDragging: this._dragState.isDragging(),
            currentPhantomExternalId: this._phantom.folder.externalId
        });

        const folder = this._phantom.folder;
        const folders = this.localFolders();

        const phantomIdx = folders
            .findIndex(f => f.externalId === folder.externalId);

        const withoutPhantom = phantomIdx === -1
            ? [...folders]
            : [...folders.slice(0, phantomIdx), ...folders.slice(phantomIdx + 1)];

        if(this._phantom.originalParentFolderExternalId === this.currentFolderExternalId()){
            const folderOriginalIds = this._phantom.originalIndexInParentFolder;

            const restored = [
                ...withoutPhantom.slice(0, folderOriginalIds),
                folder,
                ...withoutPhantom.slice(folderOriginalIds)
            ];

            this.localFolders.set(restored);
        } else {
            this.localFolders.set(withoutPhantom);
        }
    
        this._phantom = null;

        this._dragState.isDragging.set(false);
        this._dragState.draggedItem.set(null);       

        console.log(`${DND_LOG_PREFIX} onItemDragEnded: drag state cleared`);
    }

    onFolderDragOverStay(folder: AppFolderItem) {
        console.log(`${DND_LOG_PREFIX} onFolderDragOverStay CALLED`, {
            targetFolderExternalId: folder.externalId,
            targetFolderName: folder.name()
        });

        const dragged = this._dragState.draggedItem();

        if (!dragged) {
            console.log(`${DND_LOG_PREFIX} onFolderDragOverStay: no dragged item, exiting`);
            return;
        }

        if (dragged.type === 'folder' && dragged.folder.externalId === folder.externalId) {
            console.log(`${DND_LOG_PREFIX} onFolderDragOverStay: dragged folder is the same as target, exiting`, {
                draggedExternalId: dragged.folder.externalId
            });
            return;
        }

        console.log(`${DND_LOG_PREFIX} onFolderDragOverStay: opening folder`, {
            targetFolderExternalId: folder.externalId,
            draggedType: dragged.type,
            draggedExternalId: getDraggedExternalId(dragged)
        });

        this.operations()
            .openFolderFunc(folder.externalId, null);
    }

    private _lastDragOverLogKey: string | null = null;

    onFolderDragOverItem(folder: AppFolderItem, event: { position: 'before' | 'into' | 'after' }) {
        // Called on every mouse move over an item - log only on actual state change
        // (real reorder or unusual early-return). Repeated no-op invocations are silent.

        if (event.position === 'into')
            return;

        const dragged = this._dragState.draggedItem();

        if (!dragged || dragged.type !== 'folder' || !this._phantom)
            return;

        const phantomExternalId = this._phantom?.folder.externalId;
        const list = this.localFolders();
        const fromIdx = list.findIndex(f => f.externalId === phantomExternalId);
        const targetIdx = list.findIndex(f => f.externalId === folder.externalId);

        if (fromIdx === -1 || targetIdx === -1) {
            // Unusual case - log once per phantom+target combination
            const key = `notfound:${phantomExternalId}:${folder.externalId}`;

            if (this._lastDragOverLogKey !== key) {
                console.warn(`${DND_LOG_PREFIX} onFolderDragOverItem: phantom or target not found in list`, {
                    fromIdx,
                    targetIdx,
                    phantomExternalId: phantomExternalId,
                    targetExternalId: folder.externalId
                });
                this._lastDragOverLogKey = key;
            }

            return;
        }

        let toIdx = event.position === 'before'
            ? targetIdx
            : targetIdx + 1;

        if (toIdx > fromIdx) toIdx -= 1;

        if (toIdx === fromIdx)
            return;

        // Real reorder - log it
        console.log(`${DND_LOG_PREFIX} onFolderDragOverItem: REORDER`, {
            targetFolderExternalId: folder.externalId,
            targetFolderName: folder.name(),
            position: event.position,
            fromIdx,
            toIdx,
            listLength: list.length
        });
        
        this._lastDragOverLogKey = `reorder:${fromIdx}:${toIdx}`;

        this.foldersFlip?.capture();

        const next = [...list];

        const [item] = next.splice(fromIdx, 1);

        next.splice(toIdx, 0, item);

        this.localFolders.set(next);

        this.foldersFlip?.schedule();
    }

    async onFolderDroppedAt(folder: AppFolderItem, event: { position: 'before' | 'into' | 'after' }) {
        if(this._phantom == null){
            throw new Error("Phantom was not present during drop at folder phase");
        }
        
        const phantomExternalId = this._phantom.folder.externalId;
        
        console.log(`${DND_LOG_PREFIX} onFolderDroppedAt CALLED`, {
            targetFolderExternalId: folder.externalId,
            targetFolderName: folder.name(),
            position: event.position,
            phantomExternalId: phantomExternalId
        });

        const dragged = this._dragState.draggedItem();

        if (!dragged) {
            console.warn(`${DND_LOG_PREFIX} onFolderDroppedAt: no dragged item, exiting`);
            return;
        }

        const draggedId = getDraggedExternalId(dragged);
        const isTargetSelf = dragged.type === 'folder' && draggedId === folder.externalId;

        console.log(`${DND_LOG_PREFIX} onFolderDroppedAt: drop context`, {
            draggedType: dragged.type,
            draggedId,
            sourceFolderExternalId: dragged.parentFolderExternalId,
            isTargetSelf
        });

        if (event.position === 'into' && !isTargetSelf) {
            console.log(`${DND_LOG_PREFIX} onFolderDroppedAt: dropping INTO target folder (CANNCELLED)`, {
                draggedType: dragged.type,
                draggedId,
                destinationFolderExternalId: folder.externalId
            });
            return;
        }

        const currentFolder = this.currentFolderExternalId() ?? null;
        const isSameFolder = dragged.parentFolderExternalId === currentFolder;

        const newPosition = this.computePhantomDropPosition(
            phantomExternalId);

        console.log(`${DND_LOG_PREFIX} onFolderDroppedAt: reorder/move analysis`, {
            currentFolder,
            sourceFolderExternalId: dragged.parentFolderExternalId,
            isSameFolder,
            newPosition,
        });

        if (dragged.type === 'folder') {
            console.log(`${DND_LOG_PREFIX} onFolderDroppedAt: SAME-FOLDER REORDER path`);

            const list = this.localFolders();
            
            const phantomIdx = list.findIndex(f => f.externalId === phantomExternalId);
            
            if (phantomIdx === -1) {
                console.warn(`${DND_LOG_PREFIX} onFolderDroppedAt: phantom not found in list during same-folder reorder, exiting`, {
                    phantomExternalId: phantomExternalId
                });

                return;
            }

            console.log(`${DND_LOG_PREFIX} onFolderDroppedAt: replacing phantom with original source, updating position`, {
                phantom: this._phantom,
                newPosition
            });

            this._phantom.folder.position.set(newPosition);

            const next = [...list];
            next[phantomIdx] = this._phantom.folder;
            this.localFolders.set(next);

            this._phantom = null;

            console.log(`${DND_LOG_PREFIX} onFolderDroppedAt: phantom state reset, persisting position`);

            this._dragState.isDragging.set(false);
            this._dragState.draggedItem.set(null);

            console.log(`${DND_LOG_PREFIX} onFolderDroppedAt: same-folder reorder complete, drag state cleared`);
        }

        console.log(`${DND_LOG_PREFIX} onFolderDroppedAt: CROSS-FOLDER MOVE path`, {
            draggedType: dragged.type,
            draggedId,
            destinationFolderExternalId: currentFolder,
            destinationPosition: newPosition
        });

        if(isSameFolder){
            await this.persistPosition(draggedId, newPosition);     
        } else {
            await this.executeMove(dragged.type, draggedId, currentFolder, newPosition);
        }
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
        console.log(`${DND_LOG_PREFIX} executeMove CALLED`, {
            type,
            externalId,
            destinationFolderExternalId,
            destinationPosition
        });

        this._dragState.isDragging.set(false);
        this._dragState.draggedItem.set(null);

        console.log(`${DND_LOG_PREFIX} executeMove: drag state cleared, calling moveItems API`);

        try {
            await this.filesApi().moveItems({
                fileExternalIds: type === 'file' ? [externalId] : [],
                folderExternalIds: type === 'folder' ? [externalId] : [],
                fileUploadExternalIds: [],
                destinationFolderExternalId,
                destinationPosition
            });

            console.log(`${DND_LOG_PREFIX} executeMove: moveItems API succeeded, invalidating prefetched entries`);

            this.filesApi().invalidatePrefetchedEntries();
            this.moved.emit();

            console.log(`${DND_LOG_PREFIX} executeMove: moved event emitted`);
        } catch (error) {
            console.error(`${DND_LOG_PREFIX} executeMove: moveItems API FAILED`, error);
        }
    }

    private async persistPosition(externalId: string, position: number) {
        console.log(`${DND_LOG_PREFIX} persistPosition CALLED`, {
            externalId,
            position,
            parentFolderExternalId: this.currentFolderExternalId() ?? null
        });

        const api = this.filesApi();

        if (!api.updatePositions) {
            console.warn(`${DND_LOG_PREFIX} persistPosition: api.updatePositions is not defined, skipping`);
            return;
        }

        try {
            await api.updatePositions({
                parentFolderExternalId: this.currentFolderExternalId() ?? null,
                folders: [{ externalId, position }],
                files: []
            });

            console.log(`${DND_LOG_PREFIX} persistPosition: updatePositions API succeeded`);
        } catch (error) {
            console.error(`${DND_LOG_PREFIX} persistPosition: updatePositions API FAILED`, error);
        }
    }
}