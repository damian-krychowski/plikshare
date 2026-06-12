import { Component, DestroyRef, ElementRef, OnDestroy, ViewChild, computed, effect, inject, input, output, signal, untracked, viewChild } from "@angular/core";
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
import { MinimapItemState, MinimapModel, buildMiniThumbUrl, buildMinimapItemState, fileRowsToMinimapModel } from "../files-minimap/minimap-model";

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
    operations = input.required<FileOperations>();
    permissions = input.required<AppFilePermissions>();
    filesApi = input.required<FilesExplorerApi>();

    currentFolderExternalId = input<string | null>(null);
    hoverHighlightId = input<string | null>(null);
    canReorder = input(false);
    hideActions = input(false);
    hideSelectCheckboxes = input(false);
    showThumbnails = input(false);
    processingFileIds = input<ReadonlySet<string>>(new Set());

    // While folder content is still streaming in, the parent passes the total
    // file count delivered in the first chunk so the scrollbar is sized for
    // the full list upfront — rows beyond the loaded prefix stay blank until
    // the stream completes. Null once the list is complete.
    expectedTotalCount = input<number | null>(null);

    deleted = output<AppFileItem>();
    previewed = output<AppFileItem>();
    hoveredItemChanged = output<string | null>();

    // How many rows from the top the current viewport (plus render buffer)
    // reaches into — lets the parent flush its streaming buffer the moment
    // the user scrolls past the loaded prefix instead of waiting for the
    // stream to complete.
    visibleRangeEndChanged = output<number>();

    @ViewChild('filesFlip') filesFlip?: FlipAnimationDirective;

    // Geometry source for the window-driven virtualization — we read its
    // bounding rect against window.innerHeight to figure out which slice of
    // visibleFiles() is currently on-screen.
    private _hostRef = viewChild<ElementRef<HTMLElement>>('listHost');

    isSearchActive = computed(() => this.searchPhrase().length > 0);

    private _wasInitialized = false;
    localFiles = signal<AppFileItem[]>([]);
    filteredOutFiles = signal<string[]>([]);

    // Files actually shown — drops the search-filtered ones. The template
    // used to do this with a per-row @if; pulling it into a computed lets
    // virtualization base its row count and offsets on the real list size.
    visibleFiles = computed<AppFileItem[]>(() => {
        const all = this.localFiles();
        if (!this.isSearchActive()) return all;
        const filteredOut = new Set(this.filteredOutFiles());
        return all.filter(f => !filteredOut.has(f.externalId));
    });

    hasNoListSearchMatches = computed(() =>
        this.isSearchActive()
        && this.localFiles().length === this.filteredOutFiles().length);

    // Fixed row height — MUST match `.item-bar { height: 72px }` in the
    // global styles, otherwise absolute positioning of rows desyncs from
    // their rendered height.
    static readonly ROW_HEIGHT_PX = 72;
    rowHeightPx = FilesListComponent.ROW_HEIGHT_PX;

    // Rows rendered above/below the on-screen window slice so fast scrolling
    // doesn't reveal an unrendered gap before the next recompute lands.
    private static readonly RENDER_BUFFER_ROWS = 30;

    // When the window teleports (minimap drag / fling) nothing in the rendered
    // slice is reusable — every row component is destroyed and recreated, which
    // costs tens of ms. Full replacements are capped to ~10/s; overlapping
    // (wheel-speed) updates stay per-frame.
    private static readonly FULL_REPLACEMENT_INTERVAL_MS = 100;

    // Row count driving the host height and the virtualization range. During
    // streaming it is the expected total (scrollbar correct from the first
    // chunk); search falls back to the loaded list because filtered-out state
    // of not-yet-loaded rows is unknown.
    private _virtualItemCount = computed(() => {
        const visibleCount = this.visibleFiles().length;

        if (this.isSearchActive())
            return visibleCount;

        const expected = this.expectedTotalCount();

        return expected == null
            ? visibleCount
            : Math.max(visibleCount, expected);
    });

    // Host gets `height: totalHeightPx` so the page scrollbar reflects the
    // real list size — rows are absolutely positioned within this height.
    totalHeightPx = computed(() => this._virtualItemCount() * FilesListComponent.ROW_HEIGHT_PX);

    // Visible-window slice that's actually rendered to the DOM. Updated by
    // recomputeRange() on scroll/resize and on visibleFiles content changes.
    private _renderedRange = signal<{ start: number; end: number }>({ start: 0, end: 0 });

    // The DOM-rendered subset, each row carrying its absolute index so the
    // template can position it at `top = index * ROW_HEIGHT_PX`.
    renderedFiles = computed<{ file: AppFileItem; index: number }[]>(() => {
        const flat = this.visibleFiles();
        const { start, end } = this._renderedRange();
        const realEnd = Math.min(end, flat.length);
        const out: { file: AppFileItem; index: number }[] = [];
        for (let i = start; i < realEnd; i++) {
            out.push({ file: flat[i], index: i });
        }
        return out;
    });

    minimapModel = computed<MinimapModel>(() => fileRowsToMinimapModel({
        files: this.visibleFiles(),
        rowHeight: FilesListComponent.ROW_HEIGHT_PX,
        totalHeight: this.totalHeightPx(),
        buildThumbUrl: this.showThumbnails()
            ? file => buildMiniThumbUrl(file, this.operations().getThumbnailUrl)
            : null,
        showCheckboxes: !this.hideSelectCheckboxes()
    }));

    minimapItemState = computed<MinimapItemState>(() => buildMinimapItemState(this.visibleFiles()));

    minimapContentEl = computed<HTMLElement | null>(() => this._hostRef()?.nativeElement ?? null);

    private selectionAnchorExternalId: string | null = null;
    private _draggingStoppedSubscription: Subscription | null = null;

    private _rangeRecomputeScheduled = false;

    constructor(private _dragState: DragStateService) {
        effect(() => this.handleFilesInputChange());
        effect(() => this.handleSortingInputsChange());
        effect(() => this.handleSearchPhraseInputChange());

        this._draggingStoppedSubscription = this._dragState.draggingStopped$
            .subscribe(event => this.onDraggingStopped(event));

        // Capture-phase scroll listener so we catch scroll on ANY ancestor —
        // the SPA shell often scrolls an inner element (router-outlet wrapper,
        // etc.) rather than the window itself, in which case window.scroll
        // never fires. The scroll-event burst is coalesced into one recompute
        // per frame, and the range signal is only written when the rendered
        // window actually shifts — scrolling within the buffer costs no
        // change detection.
        const onScroll = () => this.scheduleRecomputeRange();

        window.addEventListener('scroll', onScroll, { capture: true, passive: true });
        window.addEventListener('resize', onScroll, { passive: true });

        const destroyRef = inject(DestroyRef);
        destroyRef.onDestroy(() => {
            window.removeEventListener('scroll', onScroll, { capture: true });
            window.removeEventListener('resize', onScroll);
        });

        // Content changes (sort/filter/load/drag-reorder) shift the host's
        // geometry and the total row count — recompute after the DOM has been
        // updated.
        effect(() => {
            this.visibleFiles();
            this._virtualItemCount();
            this.scheduleRecomputeRange();
        });
    }

    private scheduleRecomputeRange(): void {
        if (this._rangeRecomputeScheduled)
            return;

        this._rangeRecomputeScheduled = true;

        requestAnimationFrame(() => {
            this._rangeRecomputeScheduled = false;
            this.recomputeRange();
        });
    }

    // Inspects the host's position within the window viewport and updates
    // the rendered range to cover only the on-screen slice (plus buffer).
    private _lastEmittedRangeEnd = -1;

    private recomputeRange(): void {
        const el = this._hostRef()?.nativeElement;
        const itemCount = untracked(() => this._virtualItemCount());

        if (!el || itemCount === 0) {
            this.applyRange(0, 0);
            this.emitRangeEnd(0);
            return;
        }

        const rect = el.getBoundingClientRect();
        const winHeight = typeof window !== 'undefined' ? window.innerHeight : 800;

        const overflowAbove = Math.max(0, -rect.top);
        const visiblePx = Math.max(0, Math.min(rect.height, winHeight - Math.max(0, rect.top)));

        const startIdx = Math.floor(overflowAbove / FilesListComponent.ROW_HEIGHT_PX);
        const endIdx = Math.ceil((overflowAbove + visiblePx) / FilesListComponent.ROW_HEIGHT_PX);

        const buffer = FilesListComponent.RENDER_BUFFER_ROWS;
        const end = Math.min(itemCount, endIdx + buffer);
        const start = Math.max(0, startIdx - buffer);

        const current = untracked(() => this._renderedRange());

        const isFullReplacement = current.end > current.start
            && (start >= current.end || end <= current.start);

        if (isFullReplacement) {
            const now = performance.now();

            if (now - this._lastFullReplacementAt < FilesListComponent.FULL_REPLACEMENT_INTERVAL_MS) {
                this.scheduleTrailingRecompute();
                this.emitRangeEnd(end);
                return;
            }

            this._lastFullReplacementAt = now;
        }

        this.applyRange(start, end);
        this.emitRangeEnd(end);
    }

    private _lastFullReplacementAt = 0;
    private _trailingRecomputeTimer: ReturnType<typeof setTimeout> | null = null;

    private scheduleTrailingRecompute(): void {
        if (this._trailingRecomputeTimer !== null)
            return;

        this._trailingRecomputeTimer = setTimeout(
            () => {
                this._trailingRecomputeTimer = null;
                this.scheduleRecomputeRange();
            },
            FilesListComponent.FULL_REPLACEMENT_INTERVAL_MS);
    }

    private applyRange(start: number, end: number): void {
        const current = untracked(() => this._renderedRange());

        if (current.start === start && current.end === end)
            return;

        this._renderedRange.set({ start, end });
    }

    private emitRangeEnd(end: number): void {
        if (end === this._lastEmittedRangeEnd)
            return;

        this._lastEmittedRangeEnd = end;
        this.visibleRangeEndChanged.emit(end);
    }

    ngOnDestroy(): void {
        this._draggingStoppedSubscription?.unsubscribe();

        if (this._trailingRecomputeTimer !== null)
            clearTimeout(this._trailingRecomputeTimer);
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

            if (incoming.length > 0)
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

        untracked(() => {
            sortFilesInPlace(
                this.localFiles(),
                sortMode,
                sortDirection
            );
            // sortFilesInPlace mutates the array in-place; the signal needs a
            // new reference to wake the visibleFiles/renderedFiles computeds.
            this.localFiles.update(arr => [...arr]);
        });
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

        const firstSelected = this.visibleFiles().find(f => f.isSelected());
        this.selectionAnchorExternalId = firstSelected?.externalId ?? null;
    }

    onFileShiftClicked(file: AppFileItem) {
        const anchorId = this.selectionAnchorExternalId;

        if (!anchorId) {
            file.isSelected.update(v => !v);
            this.onFileSelectionToggled(file);
            return;
        }

        // Range spans the search-filtered list, not the full one — shift-clicking two rows in a
        // filtered view must not select everything between them in the unfiltered list.
        const files = this.visibleFiles();
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
