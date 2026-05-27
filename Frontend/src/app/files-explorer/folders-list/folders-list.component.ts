import { Component, ViewChild, computed, effect, input, output, signal, untracked } from "@angular/core";
import { SortDirection, SortMode } from "../../services/folders-and-files.api";
import { AppFolderItem, AppFolderPermissions, FolderItemComponent, FolderOperations } from "../../shared/folder-item/folder-item.component";
import { sortFoldersInPlace } from "../../services/sort-items";
import { DraggableItemDirective } from "../../shared/drag-drop/draggable-item.directive";
import { DropTargetDirective } from "../../shared/drag-drop/drop-target.directive";
import { FlipAnimationDirective } from "../../shared/drag-drop/flip-animation.directive";
import { DraggedFolderItem, DragStateService, getAllDraggedFiles, getAllDraggedFolders } from "../../services/drag-state.service";
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
    hideShareAction = input(false);
    permissions = input.required<AppFolderPermissions>();
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

            // Drag survives the input sync. For dragged items that match by
            // externalId in incoming, swap dragState references to the fresh
            // instances (and propagate isSelected) so counters/computeds see
            // the dragged state — those items stay at their natural positions.
            // Items not present in incoming are unshifted as phantoms so the
            // user's visual anchor for the dragged set survives a drill-in.
            if (draggedItem?.type === 'folder') {
                this._dragState.syncDraggedFolders(localFolders);

                const incomingIds = new Set(localFolders.map(f => f.externalId));
                const refreshed = this._dragState.draggedItem();
                if (refreshed?.type === 'folder') {
                    const phantoms = getAllDraggedFolders(refreshed)
                        .filter(p => !incomingIds.has(p.externalId));

                    if (phantoms.length > 0) {
                        localFolders.unshift(...phantoms);
                    }
                }
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

        const items = this.collectDraggedItems(folders, draggedIdx);

        this._dragState.startDragging({
            type: 'folder',
            items,
            parentFolderExternalId: this.currentFolderExternalId() ?? null
        });
    }

    // Multi-drag only when grabbing an already-selected folder. Grabbing an
    // unselected folder behaves like single-drag and leaves the current
    // selection untouched (Finder-style). Mixed-type selections naturally
    // restrict to same-type siblings — the file selection stays put. Items are
    // collected in source order so phantom injection on drill-in matches what
    // the user sees in localFolders.
    private collectDraggedItems(folders: AppFolderItem[], draggedIdx: number): DraggedFolderItem[] {
        const leader = folders[draggedIdx];

        if (!leader.isSelected()) {
            return [{ item: leader, originalIndex: draggedIdx }];
        }

        const items: DraggedFolderItem[] = [];
        for (let i = 0; i < folders.length; i++) {
            if (folders[i].isSelected()) {
                items.push({ item: folders[i], originalIndex: i });
            }
        }
        return items;
    }

    onFolderDragEnded() {
        const dragged = this._dragState.draggedItem();

        if (dragged == null || dragged.type !== 'folder')
            return;

        const draggedIds = this._dragState.draggedExternalIds();
        const folders = this.localFolders();
        const withoutDragged = folders.filter(f => !draggedIds.has(f.externalId));

        if (dragged.parentFolderExternalId === this.currentFolderExternalId()) {
            // Restore every dragged folder to its original slot. Insertions in
            // ascending originalIndex order keep earlier slots stable while
            // later items are placed.
            const ascending = [...dragged.items]
                .sort((a, b) => a.originalIndex - b.originalIndex);

            const restored = [...withoutDragged];
            for (const { item, originalIndex } of ascending) {
                const insertAt = Math.min(originalIndex, restored.length);
                restored.splice(insertAt, 0, item);
            }

            this.localFolders.set(restored);
        } else {
            this.localFolders.set(withoutDragged);
        }

        this._dragState.stopDragging({ reason: 'canceled' });
    }

    onFolderDragOverStay(folder: AppFolderItem) {
        const dragged = this._dragState.draggedItem();

        // Suppress only when hovering an internal folder drag over one of its
        // own dragged folders. Null dragged == OS-file drag, which should
        // always drill in.
        if (dragged?.type === 'folder' && this._dragState.draggedExternalIds().has(folder.externalId))
            return;

        this.operations()
            .openFolderFunc(folder.externalId, null);
    }

    onFolderDragOverItem(folder: AppFolderItem, event: { position: 'before' | 'into' | 'after' }) {
        if (event.position === 'into')
            return;

        const dragged = this._dragState.draggedItem();

        if (!dragged || dragged.type !== 'folder')
            return;

        // Hovering over any item in the dragged group: keep the block where it is.
        const draggedIds = this._dragState.draggedExternalIds();
        if (draggedIds.has(folder.externalId))
            return;

        const list = this.localFolders();
        const withoutDragged = list.filter(f => !draggedIds.has(f.externalId));
        const draggedBlock = list.filter(f => draggedIds.has(f.externalId)); // preserves current block order

        const targetIdx = withoutDragged.findIndex(f => f.externalId === folder.externalId);
        if (targetIdx === -1)
            return;

        const insertIdx = event.position === 'before' ? targetIdx : targetIdx + 1;

        const next = [
            ...withoutDragged.slice(0, insertIdx),
            ...draggedBlock,
            ...withoutDragged.slice(insertIdx)
        ];

        if (this.sameExternalIdOrder(list, next))
            return;

        this.foldersFlip?.capture();
        this.localFolders.set(next);
        this.foldersFlip?.schedule();
    }

    private sameExternalIdOrder(a: AppFolderItem[], b: AppFolderItem[]): boolean {
        if (a.length !== b.length) return false;
        for (let i = 0; i < a.length; i++) {
            if (a[i].externalId !== b[i].externalId) return false;
        }
        return true;
    }

    async onFolderDroppedAt(folder: AppFolderItem, event: { position: 'before' | 'into' | 'after' }) {
        const dragged = this._dragState.draggedItem();

        if (!dragged)
            return;

        if (dragged.type === 'file') {
            if (event.position === 'into') {
                const allDraggedFiles = getAllDraggedFiles(dragged);

                this._dragState.stopDragging({
                    reason: 'success',
                    destinationFolderExternalId: folder.externalId
                });

                await this.moveItems(
                    [],
                    allDraggedFiles.map(f => f.externalId),
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
            const draggedIds = this._dragState.draggedExternalIds();
            const folders = this.localFolders();
            const allDraggedFolders = getAllDraggedFolders(dragged);

            // Drag state is stale if the leader vanished from this list — bail.
            if (!folders.some(f => f.externalId === dragged.items[0].item.externalId)) {
                this._dragState.stopDragging({
                    reason: 'canceled'
                });
                return;
            }

            const isTargetInDragged = draggedIds.has(folder.externalId);

            if (event.position === 'into' && !isTargetInDragged) {
                const withoutDragged = folders.filter(f => !draggedIds.has(f.externalId));

                this.localFolders.set(withoutDragged);

                this._dragState.stopDragging({
                    reason: 'success',
                    destinationFolderExternalId: folder.externalId
                });

                await this.moveItems(
                    allDraggedFolders.map(f => f.externalId),
                    [],
                    folder.externalId,
                    null);
            } else {
                const currentFolder = this.currentFolderExternalId() ?? null;
                const isSameParentFolder = dragged.parentFolderExternalId === currentFolder;

                // Backend (MoveItemsToFolderQuery) distributes positions as
                // base+0, base+1, … for each item in the request, so one base
                // position is enough for the whole block — same-parent reorder
                // takes the same base + a per-item ladder via updatePositions.
                const basePosition = this.computeBlockBasePosition(draggedIds);
                allDraggedFolders.forEach((f, i) => f.position.set(basePosition + i));

                this._dragState.stopDragging({
                    reason: 'success',
                    destinationFolderExternalId: currentFolder
                });

                if (isSameParentFolder) {
                    await this.persistPositions(
                        allDraggedFolders.map((f, i) => ({
                            externalId: f.externalId,
                            position: basePosition + i
                        })),
                        []);
                } else {
                    await this.moveItems(
                        allDraggedFolders.map(f => f.externalId),
                        [],
                        currentFolder,
                        basePosition);
                }
            }
            return;
        }

        throw new Error(`Unrecognized dragged item type ${(dragged as any).type}`);
    }

    private computeBlockBasePosition(draggedIds: ReadonlySet<string>): number {
        const list = this.localFolders();
        const firstDraggedIdx = list.findIndex(f => draggedIds.has(f.externalId));
        const neighbors = list.filter(f => !draggedIds.has(f.externalId));

        const insertionIdx = firstDraggedIdx === -1
            ? neighbors.length
            : Math.min(firstDraggedIdx, neighbors.length);

        return computePositionForInsertion(
            neighbors,
            insertionIdx,
            item => item.position());
    }

    private async moveItems(
        folderExternalIds: string[],
        fileExternalIds: string[],
        destinationFolderExternalId: string | null,
        destinationPosition: number | null
    ) {
        try {
            await this.filesApi().moveItems({
                fileExternalIds,
                folderExternalIds,
                fileUploadExternalIds: [],
                destinationFolderExternalId,
                destinationPosition
            });

            this.filesApi().invalidatePrefetchedEntries();
        } catch (error) {
            console.error(`Something went wrong, moveItems API FAILED`, error);
        }
    }

    private async persistPositions(
        folders: { externalId: string, position: number }[],
        files: { externalId: string, position: number }[]
    ) {
        const api = this.filesApi();

        if (!api.updatePositions)
            return;

        try {
            await api.updatePositions({
                parentFolderExternalId: this.currentFolderExternalId() ?? null,
                folders,
                files
            });
        } catch (error) {
            console.error(`Something went wrong, updatePositions API FAILED`, error);
        }
    }

}