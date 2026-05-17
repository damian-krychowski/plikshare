import { Component, OnDestroy, ViewChild, computed, effect, input, output, signal, untracked } from "@angular/core";
import { Subscription } from "rxjs";
import { SortDirection, SortMode } from "../../services/folders-and-files.api";
import { AppFileItem, AppFilePermissions, FileItemComponent, FileOperations } from "../../shared/file-item/file-item.component";
import { sortFilesInPlace } from "../../services/sort-items";
import { DraggableItemDirective } from "../../shared/drag-drop/draggable-item.directive";
import { DropTargetDirective } from "../../shared/drag-drop/drop-target.directive";
import { FlipAnimationDirective } from "../../shared/drag-drop/flip-animation.directive";
import { DraggedFileItem, DraggingStoppedEvent, DragStateService, getAllDraggedFiles } from "../../services/drag-state.service";
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
    hideDownloadAction = input(false);
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

        const draggedIds = new Set(event.item.items.map(i => i.item.externalId));
        const files = this.localFiles();

        // None of the dragged files are in this list — not our drag, skip.
        if (!files.some(f => draggedIds.has(f.externalId)))
            return;

        const currentFolder = this.currentFolderExternalId();

        if (event.reason === 'success' && event.destinationFolderExternalId === currentFolder) {
            // Files ended up in this folder — keep them where the drop placed them.
            // (same-folder reorder, or cross-folder phantom that landed here)
            return;
        }

        const withoutDragged = files.filter(f => !draggedIds.has(f.externalId));

        const isSourceFolder = event.item.parentFolderExternalId === currentFolder;

        if (event.reason === 'canceled' && isSourceFolder) {
            // Drag aborted in source folder — restore every dragged file to its
            // original slot. Insertions in ascending originalIndex order keep
            // earlier slots stable while later items are placed.
            const ascending = [...event.item.items]
                .sort((a, b) => a.originalIndex - b.originalIndex);

            const restored = [...withoutDragged];
            for (const { item, originalIndex } of ascending) {
                const insertAt = Math.min(originalIndex, restored.length);
                restored.splice(insertAt, 0, item);
            }

            this.localFiles.set(restored);
            return;
        }

        // Either: source folder success but files moved out (drop on another folder),
        // or foreign-folder phantom that didn't land here (success elsewhere / canceled).
        // Either way the files should not be in this list anymore.
        this.localFiles.set(withoutDragged);
    }

    private handleFilesInputChange() {
        const incoming = this.files();

        untracked(() => {
            const draggedItem = this._dragState.draggedItem();

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

            // Drag survives the input sync. For dragged items that match by
            // externalId in incoming, swap dragState references to the fresh
            // instances (and propagate isSelected) so counters/computeds see
            // the dragged state — those items stay at their natural positions.
            // Items not present in incoming are unshifted as phantoms so the
            // user's visual anchor for the dragged set survives a drill-in.
            if (draggedItem?.type === 'file') {
                this._dragState.syncDraggedFiles(localFiles);

                const incomingIds = new Set(localFiles.map(f => f.externalId));
                const refreshed = this._dragState.draggedItem();
                if (refreshed?.type === 'file') {
                    const phantoms = getAllDraggedFiles(refreshed)
                        .filter(p => !incomingIds.has(p.externalId));

                    if (phantoms.length > 0) {
                        localFiles.unshift(...phantoms);
                    }
                }
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

        const items = this.collectDraggedItems(files, draggedIdx);

        this._dragState.startDragging({
            type: 'file',
            items,
            parentFolderExternalId: this.currentFolderExternalId() ?? null
        });
    }

    // Multi-drag only when grabbing an already-selected file. Grabbing an
    // unselected file behaves like single-drag and leaves the current
    // selection untouched (Finder-style). Mixed-type selections naturally
    // restrict to same-type siblings — the folder selection stays put. Items
    // are collected in source order so phantom injection on drill-in matches
    // what the user sees in localFiles.
    private collectDraggedItems(files: AppFileItem[], draggedIdx: number): DraggedFileItem[] {
        const leader = files[draggedIdx];

        if (!leader.isSelected()) {
            return [{ item: leader, originalIndex: draggedIdx }];
        }

        const items: DraggedFileItem[] = [];
        for (let i = 0; i < files.length; i++) {
            if (files[i].isSelected()) {
                items.push({ item: files[i], originalIndex: i });
            }
        }
        return items;
    }

    onFileDragOverItem(file: AppFileItem, event: { position: 'before' | 'into' | 'after' }) {
        if (event.position === 'into')
            return;

        const dragged = this._dragState.draggedItem();

        if (!dragged || dragged.type !== 'file')
            return;

        // Hovering over any item in the dragged group: keep the block where it is.
        const draggedIds = this._dragState.draggedExternalIds();
        if (draggedIds.has(file.externalId))
            return;

        const list = this.localFiles();
        const withoutDragged = list.filter(f => !draggedIds.has(f.externalId));
        const draggedBlock = list.filter(f => draggedIds.has(f.externalId)); // preserves current block order

        const targetIdx = withoutDragged.findIndex(f => f.externalId === file.externalId);
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

        this.filesFlip?.capture();
        this.localFiles.set(next);
        this.filesFlip?.schedule();
    }

    private sameExternalIdOrder(a: AppFileItem[], b: AppFileItem[]): boolean {
        if (a.length !== b.length) return false;
        for (let i = 0; i < a.length; i++) {
            if (a[i].externalId !== b[i].externalId) return false;
        }
        return true;
    }

    async onFileDroppedAt(file: AppFileItem, event: { position: 'before' | 'into' | 'after' }) {
        if (event.position === 'into')
            return;

        const dragged = this._dragState.draggedItem();

        if (!dragged || dragged.type !== 'file')
            return;

        const draggedIds = this._dragState.draggedExternalIds();
        const files = this.localFiles();
        const allDraggedFiles = getAllDraggedFiles(dragged);

        // Drag state is stale if the leader vanished from this list — bail.
        if (!files.some(f => f.externalId === dragged.items[0].item.externalId)) {
            this._dragState.stopDragging({ reason: 'canceled' });
            return;
        }

        const currentFolder = this.currentFolderExternalId() ?? null;
        const isSameParentFolder = dragged.parentFolderExternalId === currentFolder;

        // Backend (MoveItemsToFolderQuery) distributes positions as base+0, base+1, …
        // for each item in the request, so one base position is enough for the whole
        // block — same-parent reorder takes the same base + a per-item ladder via
        // updatePositions.
        const basePosition = this.computeBlockBasePosition(draggedIds);
        allDraggedFiles.forEach((f, i) => f.position.set(basePosition + i));

        // Read all local state needed before stopDragging — subscribers may mutate localFiles.
        this._dragState.stopDragging({
            reason: 'success',
            destinationFolderExternalId: currentFolder
        });

        if (isSameParentFolder) {
            await this.persistPositions(
                allDraggedFiles.map((f, i) => ({
                    externalId: f.externalId,
                    position: basePosition + i
                })));
        } else {
            await this.moveItems(
                allDraggedFiles.map(f => f.externalId),
                currentFolder,
                basePosition);
        }
    }

    private computeBlockBasePosition(draggedIds: ReadonlySet<string>): number {
        const list = this.localFiles();
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
        fileExternalIds: string[],
        destinationFolderExternalId: string | null,
        destinationPosition: number | null
    ) {
        try {
            await this.filesApi().moveItems({
                fileExternalIds,
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

    private async persistPositions(files: { externalId: string, position: number }[]) {
        const api = this.filesApi();

        if (!api.updatePositions)
            return;

        try {
            await api.updatePositions({
                parentFolderExternalId: this.currentFolderExternalId() ?? null,
                folders: [],
                files
            });
        } catch (error) {
            console.error(`Something went wrong, updatePositions API FAILED`, error);
        }
    }

}
