import { Component, computed, DestroyRef, effect, ElementRef, inject, input, OnChanges, output, signal, SimpleChanges, untracked, viewChild, WritableSignal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

import { toggle } from '../signal-utils';
import { AppFolderAncestor, AppFolderItem } from '../folder-item/folder-item.component';
import { AppFileItem, AppFileItems } from '../file-item/file-item.component';
import { FormsModule } from '@angular/forms';
import { SearchFilesTreeFileItem, SearchFilesTreeFolderItem, SearchFilesTreeResponse, SortDirection, SortMode } from '../../services/folders-and-files.api';
import { getNameWithHighlight } from '../name-with-highlight';
import { TreeItem, AppTreeItem, FolderTreeItem, FileTreeItem, TreeViewMode } from './tree-item';
import { FileTreeNodeComponent } from './file-tree-node/file-tree-node.component';
import { FolderTreeNodeComponent } from './folder-tree-node/folder-tree-node.component';
import { Debouncer } from '../../services/debouncer';
import { sortFiles, sortFolders } from '../../services/sort-items';


export type FileTreeDeleteSelectionState = {
    selectedFolderExternalIds: string[];
    selectedFileExternalIds: string[];
};

export type FileTreeSelectionState = {
    selectedFolderExternalIds: string[];
    selectedFileExternalIds: string[];

    excludedFolderExternalIds: string[];
    excludedFileExternalIds: string[];
}

/*
* Compares two FileTreeSelectionState instances for equality
* @param state1 First FileTreeSelectionState instance
* @param state2 Second FileTreeSelectionState instance
* @returns boolean indicating whether the states are equal
*/
export function areFileTreeSelectionsEqual(
   state1: FileTreeSelectionState,
   state2: FileTreeSelectionState
): boolean {
   // Helper function to compare arrays regardless of order
   const areArraysEqual = (arr1: string[], arr2: string[]): boolean => {
       if (arr1.length !== arr2.length) {
           return false;
       }
       const set1 = new Set(arr1);
       return arr2.every(id => set1.has(id));
   };

   // Compare each property
   return (
       areArraysEqual(state1.selectedFolderExternalIds, state2.selectedFolderExternalIds) &&
       areArraysEqual(state1.selectedFileExternalIds, state2.selectedFileExternalIds) &&
       areArraysEqual(state1.excludedFolderExternalIds, state2.excludedFolderExternalIds) &&
       areArraysEqual(state1.excludedFileExternalIds, state2.excludedFileExternalIds)
   );
}

type SelectionStateLabel = 'selected' | 'excluded';

export type LoadFolderNodeRequest = {
    folder: AppFolderItem;
    folderLoadedCallback: (children: AppTreeItem[]) => void;
}

export type FileTreeSearchRequest = {
    phrase: string;
    callback: (response: SearchFilesTreeResponse) => void;
}

type TreeNavigationChange = UpTheTreeNaviationChange | DownTheTreeNaviationChange | SameLevelTreeNavigationChange;

type UpTheTreeNaviationChange = {
    type: 'up-the-tree';
    currentParentFolder: FolderTreeItem;
}

type DownTheTreeNaviationChange = {
    type: 'down-the-tree';
    oldParentFolder: FolderTreeItem;
}

type SameLevelTreeNavigationChange = {
    type: 'same-level'
}

type SearchResponse = {
    phrase: string;
    response: SearchFilesTreeResponse
};

export type SearchedFilesSelection = {
    matchingFiles: number;
    selectedFiles: number;
}

// One flattened row produced by the virtual-scroll renderer. DFS walk of the
// tree in display order — one entry per visible node; collapsed/filtered
// children are skipped. `depth` drives the indent (padding-left in template)
// so we don't need nested DOM. `height` is the row's pixel height (varies in
// show-only-selected: rows that display an ancestor path are taller) and
// `offset` is the prefix-sum of all preceding rows' heights — the absolute
// `top` for this row. Known-variable-height virtualization: heights come from
// the data, not from measuring the DOM.
export type FlatTreeRow = {
    node: TreeItem;
    depth: number;
    height: number;
    offset: number;
};

@Component({
    selector: 'app-file-tree-view',
    imports: [
    FormsModule,
    MatIconModule,
    MatButtonModule,
    FileTreeNodeComponent,
    FolderTreeNodeComponent
],
    templateUrl: './file-tree-view.component.html',
    styleUrls: ['./file-tree-view.component.scss']
})
export class FileTreeViewComponent implements OnChanges {
    topLevelItems = input.required<AppTreeItem[]>();
    canSelect = input.required<boolean>();
    isActive = input.required<boolean>();
    viewMode = input.required<TreeViewMode>();
    searchPhrase = input<string>('');
    sortMode = input<SortMode>('custom');
    sortDirection = input<SortDirection>('asc');

    // Folder externalIds to auto-expand on render. Triggers the existing loadFolderChildrenHandler
    // (same code path as a user click on the chevron) — no new load logic. Works recursively:
    // as children load and new folder wrappers appear, those are checked against this list too.
    autoExpandFolderIds = input<string[]>([]);
    private _autoExpandIdSet = computed(() => new Set(this.autoExpandFolderIds()));

    // Item externalIds (folders or files) that should start with isExcluded=true on their wrapper.
    // Read once when each wrapper is created in mapFolderItem / mapFileItem.
    initiallyExcludedExternalIds = input<string[]>([]);

    allowDownload = input(false);

    // Thumbnail support (mirrors list-view). Same toggle/state as the list — the explorer feeds the
    // same signals. readyMiniEtags drives the live-update effect for nodes that don't share the
    // list's AppFileItem instances (sub-folders, search results).
    showThumbnails = input(false);
    processingFileIds = input<ReadonlySet<string>>(new Set());
    readyMiniEtags = input<ReadonlyMap<string, string>>(new Map());
    getThumbnailUrl = input<((fileExternalId: string) => string) | undefined>(undefined);

    selectionStateChanged = output<FileTreeSelectionState>();
    
    fileClicked = output<AppFileItem>();
    fileDownloadClicked = output<AppFileItem>();

    folderPrefetchRequested = output<AppFolderItem>();
    folderLoadRequested = output<LoadFolderNodeRequest>();
    folderSetToRoot = output<AppFolderItem>();

    itemsDeleted = output<FileTreeDeleteSelectionState>();

    searchRequested = output<FileTreeSearchRequest>();
    searchedFilesSelectionChanged = output<SearchedFilesSelection | null>();
    searchActivated = output<void>();

    nodes = signal<TreeItem[]>([]);
    nodesCount = computed(() => this.nodes().length);

    selectionState  = signal<FileTreeSelectionState>({
        excludedFileExternalIds: [],
        excludedFolderExternalIds: [],
        selectedFileExternalIds: [],
        selectedFolderExternalIds: []
    });

    isAnyItemSelected = computed(() => {
        const selectionState = this.selectionState();

        return selectionState.selectedFileExternalIds.length > 0 
            || selectionState.selectedFolderExternalIds.length > 0;
    });

    isSearchActive = signal(false);

    private _lastSearchResponse = signal<SearchResponse | null>(null);
    private _narrowedSearchResponse = signal<SearchFilesTreeResponse | null>(null);
    
    hasSearchGivenNoResults = computed(() => {
        const lastSearchResponse = this._lastSearchResponse();

        if(!lastSearchResponse)
            return false;

        if(lastSearchResponse.response.files.length == 0){
            return true;
        }

        const narrowedSearchResponse = this._narrowedSearchResponse();

        if(!narrowedSearchResponse)
            return false;

        return narrowedSearchResponse.files.length == 0;
    });

    searchTooManyResultsCounter = computed(() => {
        const lastSearchResponse = this._lastSearchResponse();

        if(!lastSearchResponse)
            return -1;

        return lastSearchResponse.response.tooManyResultsCounter;
    }); 

    private _foldersMap = signal<Map<string, FolderTreeItem>>(new Map());

    
    fileClickedHandler = (node: FileTreeItem) => this.fileClicked.emit(node.item);

    setFolderToRootHandler = (node: FolderTreeItem) => this.folderSetToRoot.emit(node.item);

    prefetchFolderHandler = (node: FolderTreeItem) => this.folderPrefetchRequested.emit(node.item);

    loadFolderChildrenHandler = (node: FolderTreeItem) => this.folderLoadRequested.emit({
        folder: node.item,

        folderLoadedCallback: (items: AppTreeItem[]) => {
            node.wasLoaded = true;                    
            this.convertItemsToFolderChildren(node, items);
        }
    });

    isSelectedChangedHandler = (node: TreeItem, isSelected: boolean) => this.onIsSelectedChange(node, isSelected);
    isExcludedChangedHandler = (node: TreeItem, isExcluded: boolean) => this.onIsExcludedChange(node, isExcluded);
    checkboxMouseDownHandler = (event: MouseEvent) => this.onCheckboxMouseDown(event);

    dataSource = signal<TreeItem[]>([]);

    // Window-driven virtualization, ported from StaticFileTreeViewComponent. The
    // old approach rendered one Angular component per node — at 10k items that
    // was several seconds of component instantiation + DOM work even though only
    // a small slice was on-screen. We now produce a flat DFS row list and
    // render only the visible slice.
    private _hostRef = viewChild<ElementRef<HTMLElement>>('host');

    // Default single-line row height. Rows that show an ancestor path (in
    // show-only-selected, for items viewed from another folder) are taller.
    // Both MUST match the rendered heights in file-tree-view.component.scss /
    // the node templates, or absolute positioning desyncs.
    static readonly ROW_HEIGHT_PX = 44;
    // Measured rendered height of a row with an ancestor path is ~52.39px;
    // rounded up so the slot never clips the content (tiny sub-px slack is
    // invisible, overlap is not).
    private static readonly ROW_WITH_PATH_PX = 53;
    private static readonly RENDER_BUFFER_ROWS = 30;

    // DFS-flattened list of currently-visible rows. Folder children are pushed
    // only when the folder is effectively expanded (isExpanded OR — in show-all
    // with active search — searchedChildrenCount > 0). Per-node visibility
    // replicates the @class.invisible logic that used to live in
    // file-tree-node / folder-tree-node templates, so collapsed/filtered nodes
    // don't take a row slot (which would leave gaps in absolute-positioned UI).
    flatVisibleNodes = computed<FlatTreeRow[]>(() => {
        const roots = this.dataSource();
        const viewMode = this.viewMode();
        const isSearchActive = this.isSearchActive();

        return this.flatten(
            roots,
            viewMode,
            isSearchActive);
    });

    // Total scroll height = bottom edge of the last row (offset + height).
    totalHeightPx = computed(() => {
        const flat = this.flatVisibleNodes();
        if (flat.length === 0) return 0;
        const last = flat[flat.length - 1];
        return last.offset + last.height;
    });

    private _renderedRange: WritableSignal<{ start: number; end: number }> = signal({ start: 0, end: 0 });

    renderedRows = computed<FlatTreeRow[]>(() => {
        const flat = this.flatVisibleNodes();
        const { start, end } = this._renderedRange();

        // Clamp both ends — `_renderedRange` is a separate signal that can
        // briefly hold stale values (set by a RAF-scheduled recomputeRange,
        // while flatVisibleNodes can shrink synchronously via signal cascade,
        // e.g. when search collapses the list from 10k to a handful). Without
        // clamping start, `for (i=stale; i<smallerEnd; i++)` loops zero times
        // and the user sees an empty list until the next RAF lands.
        const realEnd = Math.min(end, flat.length);
        const realStart = Math.min(start, realEnd);

        // Each row already carries its absolute `offset` and `height`, so the
        // template positions it directly — no index→pixel math needed here.
        return flat.slice(realStart, realEnd);
    });

    constructor(){
        // Re-sort already loaded tree levels when the user toggles sort mode/direction.
        // The mutation pass is wrapped in `untracked` so that reading `_foldersMap()` and
        // each folder's `children()` does not register them as effect dependencies — without
        // this guard, calling `children.set(...)` would re-trigger the effect → infinite loop.
        effect(() => {
            const mode = this.sortMode();
            const direction = this.sortDirection();
            untracked(() => this.resortTree(mode, direction));
        });

        // Capture-phase scroll listener — catches scroll on ANY ancestor (SPA
        // shells often scroll an inner element rather than window). RAF batches
        // scroll bursts into one recompute per frame.
        const onScroll = () => requestAnimationFrame(() => this.recomputeRange());
        window.addEventListener('scroll', onScroll, { capture: true, passive: true });
        window.addEventListener('resize', onScroll, { passive: true });

        const destroyRef = inject(DestroyRef);
        destroyRef.onDestroy(() => {
            window.removeEventListener('scroll', onScroll, { capture: true });
            window.removeEventListener('resize', onScroll);
        });

        // Content shifts (expand/collapse, sort, search, dataSource swap) change
        // the row count and host geometry — recompute after DOM has been updated.
        effect(() => {
            this.flatVisibleNodes();
            requestAnimationFrame(() => this.recomputeRange());
        });

        let wasSearchActive = false;
        effect(() => {
            const active = this.isSearchActive();
            if (active && !wasSearchActive) {
                this.searchActivated.emit();
            }
            wasSearchActive = active;
        });

        // Apply freshly-generated Mini etags onto the tree's own file nodes (sub-folders / search
        // results), which are separate AppFileItem instances from the list. Top-level nodes reuse the
        // list's items, so the explorer's own effect already covers them — re-setting an unchanged
        // etag is a no-op, so the overlap is harmless.
        effect(() => {
            const etags = this.readyMiniEtags();
            
            if (etags.size === 0)
                return;

            untracked(() => {
                for (const row of this.flatVisibleNodes()) {
                    if (row.node.type !== 'file')
                        continue;

                    const item = row.node.item;
                    const etag = etags.get(item.externalId);
                    const current = item.metadata();

                    if (etag && current?.thumbnail?.miniEtag !== etag)
                        item.metadata.set({
                            thumbnail: { miniEtag: etag },
                            dimensions: current?.dimensions ?? null
                        });
                }
            });
        });
    }

    private flatten(roots: TreeItem[], viewMode: TreeViewMode, isSearchActive: boolean): FlatTreeRow[] {
        const result: FlatTreeRow[] = [];
        const stack: { node: TreeItem; depth: number }[] = [];

        for (let i = roots.length - 1; i >= 0; i--) {
            stack.push({ node: roots[i], depth: 0 });
        }

        // Running prefix-sum of row heights — each emitted row's `offset` is
        // the total height of all rows before it (its absolute top).
        let offset = 0;

        while (stack.length > 0) {
            const { node, depth } = stack.pop()!;

            if (!this.isNodeVisible(node, viewMode, isSearchActive))
                continue;

            const height = this.rowHeight(node, viewMode);
            result.push({ node, depth, height, offset });
            offset += height;

            if (node.type !== 'folder') continue;

            const effectivelyExpanded = node.isExpanded()
                || (viewMode === 'show-all' && node.searchedChildrenCount() > 0);

            if (!effectivelyExpanded) continue;

            const children = node.children();
            for (let i = children.length - 1; i >= 0; i--) {
                stack.push({ node: children[i], depth: depth + 1 });
            }
        }

        return result;
    }

    // Row height is derived from the data, not measured: a row is taller only
    // when it actually renders an ancestor path — i.e. in show-only-selected,
    // for a selected item that has a non-empty fullPath (viewed from another
    // folder). MUST stay in sync with the node templates' `pathText` gate.
    private rowHeight(node: TreeItem, viewMode: TreeViewMode): number {
        if (viewMode === 'show-only-selected'
            && node.item.isSelected()
            && !!node.fullPath()) {
            return FileTreeViewComponent.ROW_WITH_PATH_PX;
        }
        return FileTreeViewComponent.ROW_HEIGHT_PX;
    }

    private isNodeVisible(node: TreeItem, viewMode: TreeViewMode, isSearchActive: boolean): boolean {
        if (viewMode === 'show-all') {
            if (node.type === 'file') {
                return !isSearchActive || node.isSearched();
            }

            return !isSearchActive
                || node.isSearched()
                || node.searchedChildrenCount() > 0;
        }

        // show-only-selected — visibility IS the selection state.
        return (node.item.isSelected() || node.isParentSelected())
            && !node.isExcluded() && !node.isParentExcluded();
    }

    private recomputeRange(): void {
        const el = this._hostRef()?.nativeElement;
        const flat = this.flatVisibleNodes();

        if (!el || flat.length === 0) {
            this._renderedRange.set({ start: 0, end: 0 });
            return;
        }

        const rect = el.getBoundingClientRect();
        const winHeight = typeof window !== 'undefined' ? window.innerHeight : 800;

        const viewTop = Math.max(0, -rect.top);
        const visiblePx = Math.max(0, Math.min(rect.height, winHeight - Math.max(0, rect.top)));
        const viewBottom = viewTop + visiblePx;

        // Rows are variable-height, so the on-screen slice can't be derived by
        // division — binary-search the prefix-sum offsets instead.
        const startIdx = this.firstRowEndingAfter(flat, viewTop);
        const endIdx = this.firstRowStartingAtOrAfter(flat, viewBottom);

        const buffer = FileTreeViewComponent.RENDER_BUFFER_ROWS;
        const end = Math.min(flat.length, endIdx + buffer);
        this._renderedRange.set({
            start: Math.max(0, Math.min(startIdx - buffer, end)),
            end
        });
    }

    // Smallest index whose bottom edge (offset + height) is below `y` — i.e.
    // the first row still (partly) visible from the top of the viewport.
    private firstRowEndingAfter(flat: FlatTreeRow[], y: number): number {
        let lo = 0;
        let hi = flat.length;
        while (lo < hi) {
            const mid = (lo + hi) >> 1;
            if (flat[mid].offset + flat[mid].height > y) {
                hi = mid;
            } else {
                lo = mid + 1;
            }
        }
        return lo;
    }

    // Smallest index whose top edge (offset) is at or below the viewport
    // bottom — exclusive end of the visible slice.
    private firstRowStartingAtOrAfter(flat: FlatTreeRow[], y: number): number {
        let lo = 0;
        let hi = flat.length;
        while (lo < hi) {
            const mid = (lo + hi) >> 1;
            if (flat[mid].offset >= y) {
                hi = mid;
            } else {
                lo = mid + 1;
            }
        }
        return lo;
    }

    private resortTree(mode: SortMode, direction: SortDirection) {
        for (const folder of this._foldersMap().values()) {
            const current = folder.children();
            
            if (current.length === 0) 
                continue;

            folder.children.set(
                this.sortMixed(
                    current, 
                    mode, 
                    direction));
        }

        const top = this.nodes();
        if (top.length > 0) {
            const sortedTop = this.sortMixed(
                top, 
                mode, 
                direction);
            
            this.nodes.set(sortedTop);

            this.dataSource.set(
                this.viewMode() === 'show-all'
                    ? sortedTop
                    : this.buildTreeOfSelectedNodes(sortedTop)
            );
        }
    }

    private sortMixed(items: TreeItem[], mode: SortMode, direction: SortDirection): TreeItem[] {
        const folders = items.filter((i): i is FolderTreeItem => i.type === 'folder');
        const files = items.filter((i): i is FileTreeItem => i.type === 'file');

        const sortedFolders = sortFolders(folders.map(f => f.item), mode, direction);
        const sortedFiles = sortFiles(files.map(f => f.item), mode, direction);

        const folderByItem = new Map(folders.map(f => [f.item.externalId, f]));
        const fileByItem = new Map(files.map(f => [f.item.externalId, f]));

        return [
            ...sortedFolders.map(item => folderByItem.get(item.externalId)!),
            ...sortedFiles.map(item => fileByItem.get(item.externalId)!)
        ];
    }

    ngOnChanges(changes: SimpleChanges) {
        let shouldReevaluateSelectionState = false;
        let shouldSetDataSource = false;

        if (changes['topLevelItems']) {
            const oldNodes = this.nodes();

            const topLevelItems = this.topLevelItems();
            const currentNodes = this.getTreeStructures(topLevelItems);

            if(oldNodes?.length > 0) {
                this.applyOldNodesOnCurrentNodes(
                    currentNodes,
                    oldNodes);
            }

            this.nodes.set(currentNodes);
            this._foldersMap.set(this.buildFoldersMap(currentNodes));

            shouldReevaluateSelectionState = true;
            shouldSetDataSource = true;

            this.unmarkSearchedNodes();
            this.clearSearch();

            this.autoExpandMatchingFolders(currentNodes);
        }

        const isActiveChanges = changes['isActive'];
        if(isActiveChanges && isActiveChanges.currentValue === true) {
            shouldReevaluateSelectionState = true;
        } 

        if(changes['viewMode']) {
            shouldSetDataSource = true;
        }

        if(changes['searchPhrase']) {
            const phrase = this.searchPhrase();

            this.performSearch(phrase);
        }

        if(shouldSetDataSource){            
            if(this.viewMode() == 'show-all') {
                this.dataSource.set(this.nodes());
            } else {
                const nodes = this.buildTreeOfSelectedNodes(this.nodes());
                this.dataSource.set(nodes);
            }
        }

        if(shouldReevaluateSelectionState) {            
            const newSelectionState = this.getSelectionState();

            if(!areFileTreeSelectionsEqual(this.selectionState(), newSelectionState)) {
                this.updateSelectionState(newSelectionState);
            }
        }
    }  

    private buildFoldersMap(nodes: TreeItem[]): Map<string, FolderTreeItem> {
        const map: Map<string, FolderTreeItem> = new Map();

        this.walkFolderNodes(nodes, folder => {
            map.set(folder.item.externalId, folder);
        });

        return map;
    }

    private applyOldNodesOnCurrentNodes(currentNodes: TreeItem[], oldNodes: TreeItem[]) {
        const treeNavigationChange = this.getTreeNavigationChange(currentNodes, oldNodes);

        if(treeNavigationChange.type == 'up-the-tree') {
            this.applyOldNodesForUpTheTreeNavigation(
                treeNavigationChange.currentParentFolder,
                oldNodes
            );
        } else if(treeNavigationChange.type == 'down-the-tree') {
            this.applyOldNodesForDownTheTreeNavigation(
                treeNavigationChange.oldParentFolder,
                currentNodes
            );
        } else if(treeNavigationChange.type == 'same-level') {
            this.applyOldNodesForSameLevelNavigation(
                currentNodes,
                oldNodes
            );
        } else {
            throw new Error("Unknow tree navigation change type: " + (treeNavigationChange as any).type);
        }
    }

    private applyOldNodesForSameLevelNavigation(currentNodes: TreeItem[], oldNodes: TreeItem[]) {
        const currentFolders = currentNodes
            .filter((child): child is FolderTreeItem => child.type === 'folder');

        for (const currentFolder of currentFolders) {
            const oldFolder = oldNodes
                .find((node): node is FolderTreeItem => node.type === 'folder' && node.item.externalId === currentFolder.item.externalId);

            if(!oldFolder)
                continue;

            if(oldFolder.isExpanded()){
                currentFolder.children.update(children => [...children, ...oldFolder.children()])
                currentFolder.isExpanded.set(true);
                currentFolder.wasLoaded = true;
            }

            if(oldFolder.item.isSelected()){
                currentFolder.item.isSelected.set(true);
            }
        }
        
        const currentFiles = currentNodes
            .filter((child): child is FileTreeItem => child.type === 'file');

        for (const currentFile of currentFiles) {
            const oldFile = oldNodes
                .find((node): node is FileTreeItem => node.type === 'file' && node.item.externalId === currentFile.item.externalId);

            if(!oldFile)
                continue;

            if(oldFile.item.isSelected()){
                currentFile.item.isSelected.set(true);
            }
        }
    }

    private applyOldNodesForDownTheTreeNavigation(oldParentFolder: FolderTreeItem, currentNodes: TreeItem[]){
        const currentFolders = currentNodes
            .filter((child): child is FolderTreeItem => child.type === 'folder');

        const oldParentChildren = oldParentFolder.children();

        for (const currentFolder of currentFolders) {
            const oldFolder = oldParentChildren
                .find((child): child is FolderTreeItem => child.type === 'folder' && child.item.externalId === currentFolder.item.externalId);

            if(!oldFolder)
                continue;
            
            currentFolder.children.update(children => [...children, ...oldFolder.children()])
            currentFolder.wasLoaded = true;

            if(oldFolder.isExpanded()){
                currentFolder.isExpanded.set(true);
            }

            if(oldFolder.item.isSelected()){
                currentFolder.item.isSelected.set(true);
            }
        }
        
        const currentFiles = currentNodes
            .filter((child): child is FileTreeItem => child.type === 'file');

        for (const currentFile of currentFiles) {
            const oldFile = oldParentChildren
                .find((child): child is FileTreeItem => child.type === 'file' && child.item.externalId === currentFile.item.externalId);

            if(!oldFile)
                continue;

            if(oldFile.item.isSelected()){
                currentFile.item.isSelected.set(true);
            }
        }
    }

    private applyOldNodesForUpTheTreeNavigation(parentFolder: FolderTreeItem, oldNodes: TreeItem[]) {
        parentFolder.children.update(children => [...children, ...oldNodes])
        parentFolder.wasLoaded = true;
        parentFolder.isExpanded.set(true);
    }

    private getTreeNavigationChange(currentNodes: TreeItem[], oldNodes: TreeItem[]): TreeNavigationChange {
        const parentFolderExternalIdOfOldNodes = this.getParentFolderExternalIdOfBranch(
            oldNodes);

        if(parentFolderExternalIdOfOldNodes) {
            const parentFolder = currentNodes
                .find((node): node is FolderTreeItem => node.type === 'folder' && node.item.externalId === parentFolderExternalIdOfOldNodes);
                
            if(parentFolder) {
                return {
                    type: 'up-the-tree',
                    currentParentFolder: parentFolder
                }
            }
        } 

        const parentFolderExternalIdOfNewNodes = this.getParentFolderExternalIdOfBranch(
            currentNodes);
            
        if(parentFolderExternalIdOfNewNodes) {
            const oldParentNode = this.tryGetFolderNode(
                oldNodes,
                parentFolderExternalIdOfNewNodes);

            if(oldParentNode) {
                return {
                    type: 'down-the-tree',
                    oldParentFolder: oldParentNode
                };
            }
        }

        return {
            type: 'same-level'
        };
    }

    private getParentFolderExternalIdOfBranch(oldNodes: TreeItem[]): string | null {
        //its enought to get parent folder from the first node becasue all items belong to the same parent
        //if they were displayed previously

        if(!oldNodes || !oldNodes.length)
            return null;

        const node = oldNodes[0];

        if(node.type =='file'){
            return node.item.folderExternalId;
        }

        if(node.type == 'folder') {
            return this.getParentExternalIdOfFolder(node.item);
        }

        return null;
    }

    private getParentExternalIdOfFolder(folder: AppFolderItem): string | null {
        if(!folder.ancestors || folder.ancestors.length == 0)
            return null;

        return folder.ancestors[folder.ancestors.length - 1].externalId;
    }

    private convertItemsToFolderChildren(folder: FolderTreeItem, items: AppTreeItem[]) {
        const newFolders: FolderTreeItem[] = [];
        const newChildren: TreeItem[] = [];

        const currentChildren = folder.children();

        for (const item of items) {
            const existingItem = currentChildren.find(c => c.item.externalId == item.externalId);

            if(existingItem) {
                newChildren.push(existingItem);
            } else {
                if(item.type == 'file') {
                    const fileNode: FileTreeItem = this.mapFileItem(
                        item);

                    newChildren.push(fileNode);
                } else if(item.type == 'folder') {
                    const folderNode: FolderTreeItem = this.mapFolderItem(
                        item);

                    newChildren.push(folderNode);
                    newFolders.push(folderNode);
                } else {
                    throw new Error("Unknown tree top level item type");
                }
            }
        }

        if(newFolders.length > 0) {
            this._foldersMap.update(map => {
                for (const folder of newFolders) {
                    map.set(folder.item.externalId, folder);
                }

                return map;
            });
        }

        folder.children.set(this.sortMixed(
            newChildren, 
            this.sortMode(), 
            this.sortDirection()));

        // Newly-loaded children may already be selected (set on AppItem by the host before the
        // load callback) or excluded (seeded from initiallyExcludedExternalIds in mapXxxItem).
        // Refresh selectionState so external observers see the full picture without needing a
        // user click to trigger re-evaluation.
        this.calculateAndUpdateSelectionState();

        this.autoExpandMatchingFolders(newChildren);
    }

    // Triggers expansion on folders whose externalId is in autoExpandFolderIds, using the
    // same loadFolderChildrenHandler a user's chevron click does. This method itself does NOT
    // self-call — the cascade to deeper levels happens through an async event/callback chain:
    //   loadFolderChildrenHandler(node)
    //     → emits folderLoadRequested
    //     → host fetches children, invokes folderLoadedCallback
    //     → convertItemsToFolderChildren builds new wrappers
    //     → convertItemsToFolderChildren calls autoExpandMatchingFolders(newChildren)
    //     → and around again for any of those matching the list.
    private autoExpandMatchingFolders(nodes: TreeItem[]) {
        const ids = this._autoExpandIdSet();

        if (ids.size === 0)
            return;

        for (const node of nodes) {
            if (node.type !== 'folder')
                continue;

            if (!ids.has(node.item.externalId))
                continue;

            node.isExpanded.set(true);

            if (!node.wasLoaded)
                this.loadFolderChildrenHandler(node);
        }
    }

    private getTreeStructures(topLevelItems: AppTreeItem[]) {
        const nodes: TreeItem[] = [];

        for (const item of topLevelItems) {
            if(item.type == 'file') {
                const fileNode: FileTreeItem = this.mapFileItem(
                    item);

                nodes.push(fileNode);
            } else if(item.type == 'folder') {
                const folderNode: FolderTreeItem = this.mapFolderItem(
                    item);

                nodes.push(folderNode);
            } else {
                throw new Error("Unknown tree top level item type");
            }
        }

        return this.sortMixed(nodes, this.sortMode(), this.sortDirection());
    }

    private mapFolderItem(item: AppFolderItem) {
        const isExpandedSignal = signal(false);
        const isSearchedSignal = signal(false);
        
        const parentFolderExternalId = this.getParentExternalIdOfFolder(
            item);

        const {parentSignal, isParentSelectedSignal, isParentExcludedSignal} = this.prepareParentSignals(
            parentFolderExternalId);

        const isExcludedSignal = signal(this.initiallyExcludedExternalIds().includes(item.externalId));
        const childrenSignal = signal<TreeItem[]>([]);

        //todo maybe both selected and searched count signals could be merged into one, to limit number of iterations through children collections

        const selectedChildrenCountSignal = computed(() => {
            const children = childrenSignal();

            if(!children || !children.length)
                return 0;

            let count = 0;

            for (let index = 0; index < children.length; index++) {
                const child = children[index];
                
                if(child.item.isSelected()) {
                    count += 1;
                } else if(child.type === 'folder') {
                    count += child.selectedChildrenCount();
                }
            }

            return count;
        });

        const searchedChildrenCountSignal = computed(() => {
            const children = childrenSignal();

            if(!children || !children.length)
                return 0;

            let count = 0;

            for (let index = 0; index < children.length; index++) {
                const child = children[index];
                
                if(child.isSearched()) {
                    count += 1;
                } 
                
                if(child.type === 'folder') {
                    count += child.searchedChildrenCount();
                }
            }

            return count;
        });

        const wasRenderedMemory = {
            wasRendered: false
        };

        const wasRendered = computed(() => {
            if (wasRenderedMemory.wasRendered)
                return true;

            if (isExpandedSignal()) {
                wasRenderedMemory.wasRendered = true;
                return true;
            }

            if(searchedChildrenCountSignal() > 0) {
                wasRenderedMemory.wasRendered = true;
                return true;
            }

            return false;
        });

        const folderNode: FolderTreeItem = {
            type: 'folder',
            item: item,

            isExpanded: isExpandedSignal,
            isSearched: isSearchedSignal,
            nameWithHighlight: signal(''),

            wasRendered: wasRendered,
            isExcluded: isExcludedSignal,
            wasLoaded: false,

            parent: parentSignal,
            isParentSelected: isParentSelectedSignal,
            isParentExcluded: isParentExcludedSignal,
            fullPath: computed(() => this.getFullPath(parentSignal())),
            
            children: childrenSignal,
            selectedChildrenCount: selectedChildrenCountSignal,
            searchedChildrenCount: searchedChildrenCountSignal
        };

        return folderNode;
    }

    private mapFileItem(item: AppFileItem): FileTreeItem {
        const {parentSignal, isParentSelectedSignal, isParentExcludedSignal} = this.prepareParentSignals(
            item.folderExternalId);

        const isExcludedSignal = signal(this.initiallyExcludedExternalIds().includes(item.externalId));
        const isSearchedSignal = signal(false);

        return {
            type: 'file',
            item: item,

            isExcluded: isExcludedSignal,
            isSearched: isSearchedSignal,
            nameWithHighlight: signal(''),

            parent: parentSignal,
            isParentSelected: isParentSelectedSignal,
            isParentExcluded: isParentExcludedSignal,
            fullPath: computed(() => this.getFullPath(parentSignal())),

            canPreview: computed(() => AppFileItems.canPreview(item, this.allowDownload()))
        };
    }

    private getFullPath(parent: FolderTreeItem | null): string | null {
        if(!parent)
            return null;

        return `${this.getFullPath(parent.parent()) ?? ''}/${parent.item.name()}`;
    }

    private prepareParentSignals(parentExternalId: string | null) {
        const parentSignal = computed(() => {
            const foldersMap = this._foldersMap();
            
            return parentExternalId
                ? (foldersMap.get(parentExternalId) ?? null)
                : null;
        });
        
        const isParentSelectedSignal = computed(() => {
            const parent = parentSignal();

            if(!parent)
                return false;

            return parent.isParentSelected() || parent.item.isSelected();
        });

        const isParentExcludedSignal = computed(() => {
            const parent = parentSignal();

            if(!parent)
                return false;

            return parent.isParentExcluded() || parent.isExcluded();
        });

        return {
            parentSignal,
            isParentSelectedSignal,
            isParentExcludedSignal
        };
    }

    private tryGetFolderNode(nodes: TreeItem[], folderExternalId: string): FolderTreeItem | null {
        let found: FolderTreeItem | null = null;

        this.walkFolderNodes(nodes, folder => {
            if (folder.item.externalId === folderExternalId) {
                found = folder;
                return false;
            }

            return;
        });

        return found;
    }


    toggleFileSelection(fileNode: FileTreeItem) {
        toggle(fileNode.item.isSelected);
    }

    // Shift-click range select, mirroring StaticFileTreeViewComponent / files-list.
    // The last mousedown's shift state is captured on the checkbox wrapper
    // (onCheckboxMouseDown) and consumed by the next onIsSelectedChange.
    // _selectionAnchorExternalId is the other end of the range — the last item
    // selected by a plain click.
    private _lastShiftDown = false;
    private _selectionAnchorExternalId: string | null = null;

    onCheckboxMouseDown(event: MouseEvent) {
        this._lastShiftDown = event.shiftKey;
    }

    // Modifier-clicks anywhere on a row drive selection instead of previewing the
    // file or expanding the folder: shift extends the range, ctrl/meta toggles the
    // single clicked node (and re-anchors). Plain clicks bubble here too but return
    // early, so normal preview/expand keeps working. Checkbox clicks never reach
    // this — their wrapper stops propagation.
    onRowClicked(node: TreeItem, event: MouseEvent) {
        if (!this.canSelect())
            return;

        if (event.shiftKey) {
            event.preventDefault();
            this._lastShiftDown = true;
            this.onIsSelectedChange(node, !node.item.isSelected());
        } else if (event.ctrlKey || event.metaKey) {
            event.preventDefault();
            this.onIsSelectedChange(node, !node.item.isSelected());
        }
    }

    private onIsSelectedChange(item: TreeItem, isSelected: boolean) {
        const shift = this._lastShiftDown;
        this._lastShiftDown = false;

        if (shift && this._selectionAnchorExternalId) {
            this.selectSiblingRangeTo(item);
        } else {
            this.applySelectionToNode(item, isSelected);
            this._selectionAnchorExternalId = isSelected ? item.item.externalId : null;
        }

        this.calculateAndUpdateSelectionState();
        this.calcualteAndUpdateSearchedFilesSelectionState();
    }

    private applySelectionToNode(item: TreeItem, isSelected: boolean) {
        item.item.isSelected.set(isSelected);

        if (item.type === 'folder') {
            this.walkAllNodes(item.children(), child => {
                if (isSelected && child.item.isSelected()) {
                    child.item.isSelected.set(false);
                }

                if (!isSelected && child.isExcluded()) {
                    child.isExcluded.set(false);
                }
            });
        }
    }

    // Shift-range select scoped to a single branch: the range spans only the
    // target's visible siblings (children of a shared parent), so selection in
    // one open folder is independent of any other. Assigns inRange across the
    // whole sibling set — selecting inside [from, to] AND deselecting outside —
    // so dragging the range narrows as well as widens. The anchor stays pinned
    // to the original plain click; only a shift-click in a DIFFERENT branch
    // (anchor not among the target's siblings) re-anchors here. Each node goes
    // through applySelectionToNode, so folder cascades stay consistent.
    private selectSiblingRangeTo(target: TreeItem) {
        const siblings = this.getVisibleSiblings(target);

        const anchorIdx = siblings.findIndex(n => n.item.externalId === this._selectionAnchorExternalId);
        const targetIdx = siblings.findIndex(n => n.item.externalId === target.item.externalId);

        if (anchorIdx === -1 || targetIdx === -1) {
            this.applySelectionToNode(target, true);
            this._selectionAnchorExternalId = target.item.externalId;
            return;
        }

        const from = Math.min(anchorIdx, targetIdx);
        const to = Math.max(anchorIdx, targetIdx);

        for (let i = 0; i < siblings.length; i++) {
            const node = siblings[i];
            const inRange = i >= from && i <= to;

            if (node.item.isSelected() !== inRange) {
                this.applySelectionToNode(node, inRange);
            }
        }
    }

    private getVisibleSiblings(node: TreeItem): TreeItem[] {
        const parent = node.parent();
        const siblings = parent ? parent.children() : this.nodes();

        const viewMode = this.viewMode();
        const isSearchActive = this.isSearchActive();

        return siblings.filter(n => this.isNodeVisible(n, viewMode, isSearchActive));
    }

    private onIsExcludedChange(item: TreeItem, isExcluded: boolean) {
        item.isExcluded.set(isExcluded);

        if (item.type === 'folder') {
            this.walkAllNodes(item.children(), child => {
                if (!isExcluded && child.isExcluded()) {
                    child.isExcluded.set(false);
                }
            });
        }

        this.calculateAndUpdateSelectionState();
        this.calcualteAndUpdateSearchedFilesSelectionState();
    }
 
    private calcualteAndUpdateSearchedFilesSelectionState() {
        const searchedFiles = this.getSearchedFileNodes();

        if(searchedFiles.length > 0) {
            this.searchedFilesSelectionChanged.emit({
                matchingFiles: searchedFiles.length,
                selectedFiles: this.calculateSelectedSearchedFiles(searchedFiles)
            });
        }
    }
    
    toggleSearchedFilesSelection() {
        if(this.viewMode() == 'show-only-selected')
            return;

        const searchedFiles = this.getSearchedFileNodes();

        if(searchedFiles.length == 0)
            return;

        const allFiles = searchedFiles.length;
        const selectedFiles = this.calculateSelectedSearchedFiles(searchedFiles);

        if(allFiles == selectedFiles) {
            for (const file of searchedFiles) {
                if(file.item.isSelected()) {
                    file.item.isSelected.set(false);
                }
            }
        } else {
            for (const file of searchedFiles) {
                if(file.isParentSelected()) {
                    if(file.isExcluded()) {
                        file.isExcluded.set(false);
                    }
                } else {
                    if(!file.item.isSelected()) {
                        file.item.isSelected.set(true);
                    }
                }
            }
        }

        this.searchedFilesSelectionChanged.emit({
            matchingFiles: searchedFiles.length,
            selectedFiles: this.calculateSelectedSearchedFiles(searchedFiles)
        });

        this.calculateAndUpdateSelectionState();
    }

    private calculateSelectedSearchedFiles(searchedFiles: FileTreeItem[]) {
        return  searchedFiles
            .filter(node => node.item.isSelected() || (node.isParentSelected() && !node.isExcluded()))
            .length;
    }

    private getSearchedFileNodes() {
        const lastSearchResponse = this._lastSearchResponse();

        if(!lastSearchResponse)
            return [];

        const selectedFileExternalIdsSet = new Set(lastSearchResponse
            .response
            .files
            .map(f => f.externalId));

        const nodes: FileTreeItem[] = [];

        this.walkAllNodes(this.nodes(), node => {
            if (selectedFileExternalIdsSet.has(node.item.externalId)) {
                nodes.push(node as FileTreeItem);
            }
        });

        return nodes;
    }

    private calculateAndUpdateSelectionState() {        
        const newSelectionState = this.getSelectionState();
        this.updateSelectionState(newSelectionState);
    }

    private updateSelectionState(newSelectionState: FileTreeSelectionState) {
        this.selectionState.set(newSelectionState);
        this.selectionStateChanged.emit(newSelectionState);
    }

    // Hot path — runs on every checkbox toggle. DFS is inlined here (rather
    // than delegated to `walkSelectedOrExcludedNodes`) to skip the per-node
    // callback indirection and push id values directly into the typed result
    // arrays. At 10k+ items even the visitor's function call overhead matters.
    private getSelectionState(): FileTreeSelectionState {
        const selectedFolderExternalIds: string[] = [];
        const selectedFileExternalIds: string[] = [];
        const excludedFolderExternalIds: string[] = [];
        const excludedFileExternalIds: string[] = [];

        const selectionStack: TreeItem[] = this.nodes().slice();
        const exclusionStack: TreeItem[] = [];

        // Phase 1: walk the tree top-down. Selected nodes terminate the
        // selection-side descent (children become exclusion-only candidates);
        // excluded nodes terminate entirely; neither → keep descending.
        while (selectionStack.length > 0) {
            const node = selectionStack.pop()!;

            if (node.item.isSelected()) {
                if (node.type === 'file') {
                    selectedFileExternalIds.push(node.item.externalId);
                } else {
                    selectedFolderExternalIds.push(node.item.externalId);
                    const children = node.children();
                    for (let i = 0; i < children.length; i++) {
                        exclusionStack.push(children[i]);
                    }
                }
            } else if (node.isExcluded()) {
                if (node.type === 'file') {
                    excludedFileExternalIds.push(node.item.externalId);
                } else {
                    excludedFolderExternalIds.push(node.item.externalId);
                }
            } else if (node.type === 'folder') {
                const children = node.children();
                for (let i = 0; i < children.length; i++) {
                    selectionStack.push(children[i]);
                }
            }
        }

        // Phase 2: walk subtrees that sit under a selected folder. Only
        // excludes are meaningful here — selects would be redundant under a
        // selected ancestor.
        while (exclusionStack.length > 0) {
            const node = exclusionStack.pop()!;

            if (node.isExcluded()) {
                if (node.type === 'file') {
                    excludedFileExternalIds.push(node.item.externalId);
                } else {
                    excludedFolderExternalIds.push(node.item.externalId);
                }
            } else if (node.type === 'folder') {
                const children = node.children();
                for (let i = 0; i < children.length; i++) {
                    exclusionStack.push(children[i]);
                }
            }
        }

        return {
            selectedFolderExternalIds,
            selectedFileExternalIds,
            excludedFolderExternalIds,
            excludedFileExternalIds
        };
    }

    private buildTreeOfSelectedNodes(nodes: TreeItem[]): TreeItem[] {
        const selectionStack: TreeItem[] = nodes.slice().reverse();
        const selectedNodes: TreeItem[] = [];

        while(selectionStack.length > 0) {
            const node = selectionStack.pop();

            if(!node)
                continue;

            if(node.item.isSelected()) {
                selectedNodes.push(node);           
            } else if(node.type == 'folder') {
                const children = node.children();

                for (let index = children.length - 1; index >= 0; index--) {
                    const child = children[index];
                    selectionStack.push(child);                    
                }
            }          
        }      
        
        return selectedNodes;
    }

    // Visitor-style DFS walkers. We previously used generator functions
    // (`function*` / `yield`), but V8 generators run 3-5× slower than plain
    // loops because every `yield` has to save/restore the function context.
    // At 10k+ nodes this added measurable lag to checkbox-click handling.
    // The callback form is allocation-free (no result array, no Generator
    // object) and supports early-exit by returning `false` from `visit`.
    private walkAllNodes(nodes: TreeItem[], visit: (node: TreeItem) => boolean | void): void {
        const stack: TreeItem[] = nodes.slice();

        while (stack.length > 0) {
            const node = stack.pop()!;

            if (visit(node) === false) 
                return;

            if (node.type === 'folder') {
                const children = node.children();
                
                for (let i = 0; i < children.length; i++) {
                    stack.push(children[i]);
                }
            }
        }
    }

    private walkFolderNodes(nodes: TreeItem[], visit: (folder: FolderTreeItem) => boolean | void): void {
        this.walkAllNodes(nodes, node => {
            if (node.type !== 'folder') 
                return;

            return visit(node);
        });
    }

    // Tree walk that classifies each yielded node as 'selected' or 'excluded'.
    // Selection cascades: a selected folder terminates the selection-side
    // descent (its subtree contributes only excludes); an excluded node
    // terminates entirely. The two-stack split (selectionStack vs
    // exclusionStack) preserves that invariant — once a folder is reported
    // selected, its children are walked exclusion-only.
    private walkSelectedOrExcludedNodes(
        nodes: TreeItem[],
        visit: (node: TreeItem, state: SelectionStateLabel) => boolean | void
    ): void {
        const selectionStack: TreeItem[] = nodes.slice();
        const exclusionStack: TreeItem[] = [];

        while (selectionStack.length > 0) {
            const node = selectionStack.pop()!;

            if (node.item.isSelected()) {
                if (visit(node, 'selected') === false) return;

                if (node.type === 'folder') {
                    const children = node.children();
                    for (let i = 0; i < children.length; i++) {
                        exclusionStack.push(children[i]);
                    }
                }
            } else if (node.isExcluded()) {
                if (visit(node, 'excluded') === false) return;
            } else if (node.type === 'folder') {
                const children = node.children();
                for (let i = 0; i < children.length; i++) {
                    selectionStack.push(children[i]);
                }
            }
        }

        while (exclusionStack.length > 0) {
            const node = exclusionStack.pop()!;

            if (node.isExcluded()) {
                if (visit(node, 'excluded') === false) return;
            } else if (node.type === 'folder') {
                const children = node.children();
                for (let i = 0; i < children.length; i++) {
                    exclusionStack.push(children[i]);
                }
            }
        }
    }

    deleteSelectedItems() {
        const selectedNodes: TreeItem[] = [];
        let hasExcluded = false;

        this.walkSelectedOrExcludedNodes(this.nodes(), (node, state) => {
            if (state === 'excluded') {
                //cannot progress with deleting if any nodes are excluded
                hasExcluded = true;
                return false;
            }
            selectedNodes.push(node);
            return;
        });

        if (hasExcluded) return;

        for (const node of selectedNodes) {
            const parentNode = node.parent();
            
            if(parentNode) { 
                parentNode.children.update(nodes => nodes.filter(n => n !== node))
            } else {
                this.nodes.update(nodes => nodes.filter(n => n !== node))
            }
        }

        this.itemsDeleted.emit({
            selectedFileExternalIds: selectedNodes
                .filter(node => node.type == 'file')
                .map(node => node.item.externalId),

            selectedFolderExternalIds: selectedNodes
                .filter(node => node.type == 'folder')
                .map(node => node.item.externalId),
        });
    }
    
    private _searchDebouncer = new Debouncer(250);

    // Monotonic token bumped on every performSearch. Async fresh-search
    // callbacks capture the value at request time and bail if it's stale —
    // a slow "00" response could otherwise land AFTER the user typed "007"
    // and mark its results as searched on top of the "007" results, so both
    // sets show up at once.
    private _searchGeneration = 0;

    private performSearch(phrase: string) {
        this.unmarkSearchedNodes();

        const generation = ++this._searchGeneration;

        if(!phrase) {
            this.clearSearch();
            return;
        }

        const currentPhrase = phrase.toLowerCase();

        // Narrow-down is only valid when the previous response actually held
        // results. If it came back as too-many (response.files = [], counter
        // > 0), filtering an empty array always yields empty and the stale
        // counter keeps showing "too many" while subsequent keystrokes that
        // would have returned a tractable result set never reach the backend.
        const isNewPhraseANarrowDownOfPrevious = this.canNarrowDown(currentPhrase);

        if(isNewPhraseANarrowDownOfPrevious && !this._searchDebouncer.isOn()){
            this.executeSearchQuery({
                isNewPhraseANarrowDownOfPrevious: true,
                phrase: phrase,
                generation
            });
        } else {
            this._searchDebouncer.debounce(() => this.executeSearchQuery({
                isNewPhraseANarrowDownOfPrevious: this.canNarrowDown(phrase.toLowerCase()),
                phrase: phrase,
                generation
            }));
        }
    }

    private canNarrowDown(currentPhrase: string): boolean {
        const last = this._lastSearchResponse();
        return !!last
            && !!last.phrase
            && last.response.tooManyResultsCounter === -1
            && currentPhrase.includes(last.phrase);
    }

    private clearSearch() {
        this._searchDebouncer.cancel();
        this._lastSearchResponse.set(null);
        this._narrowedSearchResponse.set(null);
        this.isSearchActive.set(false);
        this.searchedFilesSelectionChanged.emit(null);
    }

    private executeSearchQuery(args: {
        phrase: string;
        isNewPhraseANarrowDownOfPrevious: boolean;
        generation: number;
    }) {
        const currentPhrase = args.phrase.toLowerCase();
        const lastSearchResponse = this._lastSearchResponse();

        // Defense in depth — `canNarrowDown` also gates this; we re-check
        // here so a stray caller passing `isNewPhraseANarrowDownOfPrevious:
        // true` cannot accidentally enter the narrow path against a stale
        // too-many response. The condition is inlined (rather than extracted
        // to a local) so TS narrows `lastSearchResponse` to non-null inside
        // the branch.
        if(lastSearchResponse
                && args.isNewPhraseANarrowDownOfPrevious
                && lastSearchResponse.response.tooManyResultsCounter === -1) {
            //if new search phrase starts with the current search phrase it means we dont have to call the service for new search
            //we can do a cheaper operation - narrow down the current results. That wont
            //require a DOM rebuild, we will simply hide the results which no longer matches.
            this.isSearchActive.set(true);

            const narrowedSearchResponse = {
                folderExternalIds: lastSearchResponse.response.folderExternalIds.slice(),
                folders: lastSearchResponse.response.folders.slice(),
                files: lastSearchResponse.response.files.filter(file => {
                    const fullName = `${file.name}${file.extension}`;
                    return fullName.toLowerCase().includes(currentPhrase)
                }),
                tooManyResultsCounter: -1
            };

            this._narrowedSearchResponse.set(narrowedSearchResponse);

            this.consumeSearchResponse(narrowedSearchResponse, currentPhrase);
        } else {
            this.searchRequested.emit({
                phrase: args.phrase,
                callback: response => {
                    // Stale guard: a newer keystroke has superseded this
                    // request. Applying it now would re-mark obsolete results
                    // on top of the current phrase's results. unmarkSearchedNodes
                    // already ran for the newer phrase, so just drop this one.
                    if (args.generation !== this._searchGeneration)
                        return;

                    this.isSearchActive.set(true);

                    this._lastSearchResponse.set({
                        phrase: currentPhrase,
                        response: response
                    });

                    this.consumeSearchResponse(response, currentPhrase);
                }
            });
        }
    }

    private unmarkSearchedNodes() {
        this.walkAllNodes(this.nodes(), node => {
            if (node.isSearched()) {
                node.isSearched.set(false);
            }
        });
    }

    private consumeSearchResponse(response: SearchFilesTreeResponse, searchPhraseLower: string) {
        if(response.tooManyResultsCounter > 0)
            return;

        const rootFolders: SearchFilesTreeFolderItem[] = [];
        const rootFiles: SearchFilesTreeFileItem[] = [];
        const parentIdIndexToChildrenMap: Map<number, SearchFilesTreeFolderItem[]> = new Map();
        const folderIdIndexToFilesMap: Map<number, SearchFilesTreeFileItem[]> = new Map();

        for (const folder of response.folders) {
            if(folder.parentIdIndex === -1) {
                rootFolders.push(folder);
            } else {
                if(!parentIdIndexToChildrenMap.has(folder.parentIdIndex)) {
                    parentIdIndexToChildrenMap.set(folder.parentIdIndex, []);
                }

                const children = parentIdIndexToChildrenMap.get(folder.parentIdIndex);
                children?.push(folder);
            }
        }

        for (const file of response.files) {
            if(file.folderIdIndex === -1) {
                rootFiles.push(file);
            } else {
                if(!folderIdIndexToFilesMap.has(file.folderIdIndex)) {
                    folderIdIndexToFilesMap.set(file.folderIdIndex, []);
                }

                const children = folderIdIndexToFilesMap.get(file.folderIdIndex);
                children?.push(file);
            }
        }

        type FoldersToProcess = {
            folders: SearchFilesTreeFolderItem[];
            files: SearchFilesTreeFileItem[];

            parentNode: FolderTreeItem;
            currentAncestors: AppFolderAncestor[];
        }

        const stack: FoldersToProcess[] = [];
        const topNodes = this.nodes();

        for (const rootFolder of rootFolders) {
            const {externalId, childrenFolders, childrenFiles} = getFolderDependencies(
                rootFolder.idIndex);
            
            const correspondingFolderNode = topNodes
                .find((node): node is FolderTreeItem => node.type == 'folder' && node.item.externalId === externalId); 

            if(correspondingFolderNode) {
                stack.push({
                    folders: childrenFolders,
                    files: childrenFiles,
                    parentNode: correspondingFolderNode,
                    currentAncestors: [{
                        externalId: correspondingFolderNode.item.externalId,
                        name: correspondingFolderNode.item.name()
                    }]
                });
            }
        }

        for (const rootFile of rootFiles) {
            const correspondingFileNode = topNodes
                .find((node): node is FileTreeItem => node.type == 'file' && node.item.externalId === rootFile.externalId); 

            if(correspondingFileNode){      
                const name = getFileNameWithHighlight(correspondingFileNode);
                correspondingFileNode.nameWithHighlight.set(name)
                correspondingFileNode.isSearched.set(true);     
            }
        }

        while(stack.length > 0) {
            const levelToProcess = stack.pop();

            if(!levelToProcess)
                continue;

            const childrenToAdd: TreeItem[] = [];
            const parentNodeChildren = levelToProcess.parentNode.children();

            for (const folder of levelToProcess.folders) {
                const {externalId, childrenFolders, childrenFiles} = getFolderDependencies(
                    folder.idIndex);
                
                let correspondingFolderNode = parentNodeChildren
                    .find((node): node is FolderTreeItem => node.type == 'folder' && node.item.externalId === externalId); 
                    
                if(!correspondingFolderNode) {
                    const folderItem = prepareNewFolderItem({
                        externalId: externalId,
                        folder: folder,
                        ancestors: levelToProcess.currentAncestors
                    });

                    correspondingFolderNode = this.mapFolderItem(
                        folderItem);
                        
                    childrenToAdd.push(correspondingFolderNode);
                }

                stack.push({
                    files: childrenFiles,
                    folders: childrenFolders,

                    parentNode: correspondingFolderNode,
                    currentAncestors: [
                        ...levelToProcess.currentAncestors, {
                            externalId: correspondingFolderNode.item.externalId,
                            name: correspondingFolderNode.item.name()
                        }
                    ]
                });
            }            

            const newFolders = childrenToAdd
                .filter((node): node is FolderTreeItem => node.type == 'folder');

            if(newFolders.length > 0) {
                this._foldersMap.update(map => {
                    for (const folder of newFolders) {
                        map.set(folder.item.externalId, folder);
                    }
    
                    return map;
                });
            }

            for (const file of levelToProcess.files) {
                let correspondingFileNode = parentNodeChildren
                    .find((node): node is FileTreeItem => node.type == 'file' && node.item.externalId === file.externalId); 

                if(!correspondingFileNode) {
                    const fileItem = prepareNewFileItem(file);
                    correspondingFileNode = this.mapFileItem(fileItem);
                    childrenToAdd.push(correspondingFileNode);
                }

                const name = getFileNameWithHighlight(correspondingFileNode);
                correspondingFileNode.nameWithHighlight.set(name)
                correspondingFileNode.isSearched.set(true);
            }

            levelToProcess.parentNode.children.update(children =>
                this.sortMixed([...children, ...childrenToAdd], this.sortMode(), this.sortDirection()));
        }

        this.calcualteAndUpdateSearchedFilesSelectionState();

        function getFileNameWithHighlight(file: FileTreeItem) {
            return  getNameWithHighlight(
                `${file.item.name()}${file.item.extension}`,
                searchPhraseLower);
        }

        function prepareNewFileItem(file: SearchFilesTreeFileItem): AppFileItem {
            const folderExternalId = file.folderIdIndex == null
                ? null
                : response.folderExternalIds[file.folderIdIndex];

            return {
                type: 'file',
                externalId: file.externalId,
                name: signal(file.name),
                extension: file.extension,
                folderExternalId: folderExternalId,
                folderPath: null,
                isLocked: signal(file.isLocked),
                sizeInBytes: file.sizeInBytes,
                wasUploadedByUser: file.wasUploadedByUser,
                createdAt: file.createdAt == null ? null : new Date(file.createdAt),
                position: signal(file.position),
                metadata: signal(file.metadata ?? null),

                isCut: signal(false),
                isHighlighted: signal(false),
                isNameEditing: signal(false),
                isSelected: signal(false)
            };
        }

        function prepareNewFolderItem(args: {
            externalId: string,
            folder: SearchFilesTreeFolderItem;
            ancestors: AppFolderAncestor[]
        }): AppFolderItem {
            return {
                type: 'folder',
                externalId: args.externalId,
                name: signal(args.folder.name),

                ancestors: args.ancestors.slice(),

                wasCreatedByUser: args.folder.wasCreatedByUser,
                createdAt: args.folder.createdAt == null
                    ? null
                    : new Date(args.folder.createdAt),
                position: signal(args.folder.position),

                isCut: signal(false),
                isHighlighted: signal(false),
                isNameEditing: signal(false),
                isSelected: signal(false)
            };
        }

        function getFolderDependencies(idIndex: number) {
            const externalId = response.folderExternalIds[idIndex];    
            const childrenFolders = parentIdIndexToChildrenMap.get(idIndex) ?? [];
            const childrenFiles = folderIdIndexToFilesMap.get(idIndex) ?? [];

            return {externalId, childrenFolders, childrenFiles};
        }
    }

    cancelSelection() {
        this.walkSelectedOrExcludedNodes(this.nodes(), (node, state) => {
            if (state === 'excluded') {
                node.isExcluded.set(false);
            }

            if (state === 'selected') {
                node.item.isSelected.set(false);
            }
        });

        this.calculateAndUpdateSelectionState();
        this.calcualteAndUpdateSearchedFilesSelectionState();
    }
}