import { Component, computed, DestroyRef, effect, ElementRef, inject, input, OnChanges, output, Signal, signal, SimpleChanges, viewChild, WritableSignal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

import { StorageSizePipe } from '../storage-size.pipe';
import { ActionButtonComponent } from '../buttons/action-btn/action-btn.component';
import { FileIconPipe } from '../../files-explorer/file-icon-pipe/file-icon.pipe';
import { toggle } from '../signal-utils';
import { NGramSearch } from '../../services/n-gram-search';
import { getNameWithHighlight } from '../name-with-highlight';
import { TreeCheckobxComponent } from '../file-tree-view/tree-checkbox/tree-checkbox.component';

// One flattened row produced by the virtual-scroll renderer. We walk the tree
// in DFS order and emit one entry per visible node — children of collapsed
// folders are skipped, so this is also what drives expand/collapse animations.
export type FlatTreeRow = {
    node: StaticTreeNode;
    depth: number;
};

// 'select' shows tri-state checkboxes that feed a bulk-download payload (used by
// zip preview inside a workspace). 'download' shows per-file download icons and
// emits fileDownloadClicked — the legacy quick-share UX where each file is
// fetched on its own through a presigned link.
export type StaticFileTreeViewMode = 'select' | 'download';

export type StaticTreeNode = StaticFileNode | StaticFolderNode;

// Lightweight metadata entry for search. Lets callers provide a flat index of
// every name+ancestor chain in the source data WITHOUT materializing the heavy
// StaticTreeNode objects (with their Angular signals). When the user searches,
// we hit only the matching ids and materialize the path root→match. Subtrees
// that contain no match stay unbuilt.
export type StaticSearchCorpusEntry = {
    id: string;
    name: string;
    nameLower: string;
    type: 'file' | 'folder';
    // Folder ids from root down to the immediate parent (exclusive of the entry
    // itself). Empty array for root-level entries.
    ancestorFolderIds: string[];
};

// Aggregated bulk-selection payload exposed by the tree component (and by every
// folder's subtreeState). All ids are StaticTreeNode.id (string). Consumers map
// to their own typed id space (numeric for zip, external-id string for shares).
export type StaticTreeSelection = {
    selectedFolderIds: string[];
    selectedFileIds: string[];
    excludedFolderIds: string[];
    excludedFileIds: string[];
};

export const EMPTY_TREE_SELECTION: StaticTreeSelection = Object.freeze({
    selectedFolderIds: [],
    selectedFileIds: [],
    excludedFolderIds: [],
    excludedFileIds: []
}) as StaticTreeSelection;

// Aggregates a children-array into a StaticTreeSelection by composing each
// child's own subtreeState. Folder selection semantics:
//   - selected folder → push its id, take ONLY excludes from its subtree (its
//     own subtree selects are redundant under a selected ancestor)
//   - excluded folder → push its id, prune the subtree entirely
//   - neither → merge the whole subtreeState upward
// Reactivity is driven by the child signals read here — subtreeState (signal),
// isSelected (signal), isExcluded (signal). Cascade up happens for free.
export function collectSelectionFromChildren(children: StaticTreeNode[]): StaticTreeSelection {
    const out: StaticTreeSelection = {
        selectedFolderIds: [],
        selectedFileIds: [],
        excludedFolderIds: [],
        excludedFileIds: []
    };

    for (const child of children) {
        if (child.type === 'file') {
            if (child.isSelected()) {
                out.selectedFileIds.push(child.id);
            } else if (child.isExcluded()) {
                out.excludedFileIds.push(child.id);
            }
            continue;
        }

        const sub = child.subtreeState();

        if (child.isSelected()) {
            out.selectedFolderIds.push(child.id);
            // selects under a selected folder are redundant — keep only excludes
            out.excludedFolderIds.push(...sub.excludedFolderIds);
            out.excludedFileIds.push(...sub.excludedFileIds);
        } else if (child.isExcluded()) {
            out.excludedFolderIds.push(child.id);
            // excluded subtree pruned — nothing else to carry up
        } else {
            out.selectedFolderIds.push(...sub.selectedFolderIds);
            out.selectedFileIds.push(...sub.selectedFileIds);
            out.excludedFolderIds.push(...sub.excludedFolderIds);
            out.excludedFileIds.push(...sub.excludedFileIds);
        }
    }

    return out;
}

export type StaticFileNode = {
    type: 'file';
    id: string;

    extension: string | null;

    fullName: string;
    fullNameLower: string;

    sizeInBytes: number;

    isVisible: WritableSignal<boolean>;

    // Bulk-selection state. Selection cascades from parent → descendant: a node
    // is effectively selected if any ancestor folder is selected, and effectively
    // excluded if any ancestor folder is excluded. Search-tree clones reuse these
    // exact signal references so the same checkbox state shows up in both views.
    isSelected: WritableSignal<boolean>;
    isExcluded: WritableSignal<boolean>;
    parent: StaticFolderNode | null;
    isParentSelected: Signal<boolean>;
    isParentExcluded: Signal<boolean>;
}

export type StaticFolderNode = {
    type: 'folder';
    id: string;

    name: string;
    nameLower: string;
    children: StaticTreeNode[];
    isExpanded: WritableSignal<boolean>;

    isVisible: WritableSignal<boolean>;

    // Optional lazy-build hook. When non-null, the folder's `children` have not
    // been materialized yet — calling this populates `children` and the
    // call-site MUST null this field out so the next expand is a no-op. The
    // implementation also has to invalidate any signal-of-signal subtreeState
    // it captured (so the cascade picks up the freshly-pushed children).
    loadChildren?: (() => void) | null;

    // Optional enumeration of this folder's full subtree (files + folders) for
    // search indexing, WITHOUT materializing StaticTreeNodes. Lazy-tree builders
    // populate it from source data (e.g. ZipArchive). Returned ancestor chains
    // are RELATIVE to this folder (empty = immediate child). When absent the
    // search index falls back to walking `children` — fine for eager trees.
    enumerateDescendantsForSearch?: () => StaticSearchCorpusEntry[];

    // See StaticFileNode comment — same cascading-selection contract applies.
    isSelected: WritableSignal<boolean>;
    isExcluded: WritableSignal<boolean>;
    parent: StaticFolderNode | null;
    isParentSelected: Signal<boolean>;
    isParentExcluded: Signal<boolean>;

    // Full bulk-selection payload for this folder's subtree (selects + excludes
    // the user explicitly made anywhere below). Lazy folders implement this as
    // signal-of-signal so swapping in the real computation after loadChildren
    // automatically propagates up the cascade — parent's collectSelectionFromChildren
    // reads child.subtreeState(), so any change here re-runs the parent.
    subtreeState: Signal<StaticTreeSelection>;

    // Derived from subtreeState — kept for templates that show "N selected" on
    // collapsed folders. Equals selects-count of THIS folder's own subtree
    // (excludes don't count toward the badge).
    selectedDescendantsCount: Signal<number>;
}

@Component({
    selector: 'app-static-file-tree-view',
    imports: [
        MatIconModule,
        MatButtonModule,
        FileIconPipe,
        StorageSizePipe,
        TreeCheckobxComponent,
        ActionButtonComponent
    ],
    templateUrl: './static-file-tree-view.component.html',
    styleUrls: ['./static-file-tree-view.component.scss']
})
export class StaticFileTreeViewComponent implements OnChanges {
    fileTree = input.required<StaticTreeNode[]>();
    searchPhrase = input<string>();
    canDownload = input(true);
    mode = input<StaticFileTreeViewMode>('select');

    fileClicked = output<StaticFileNode>();
    fileDownloadClicked = output<StaticFileNode>();

    // Emits the aggregated bulk-selection payload on every change. Replaces the
    // collectSelected/collectExcludesUnder helpers that used to live in each
    // consumer (quick-share, zip-preview) — those duplicated identical recursion
    // and didn't compose with lazy folders' signal-of-signal cascade.
    selectionChanged = output<StaticTreeSelection>();

    // Public read-only signal exposing the same payload — consumers that prefer
    // a signal (@ViewChild path) can read it directly without holding a
    // WritableSignal copy.
    public selectionState: Signal<StaticTreeSelection> = computed(() =>
        collectSelectionFromChildren(this.fileTree()));

    isSearchActive = computed(() => {
        const phrase = this.searchPhrase();

        return phrase && phrase.length >= 3;
    });

    // Original (un-highlighted) names indexed by id. Populated either from the
    // caller-provided corpus or from a full tree walk. applyNextSearchPhrase
    // reads from here to re-apply highlights as the user narrows the phrase.
    private _originalNameById: Map<string, string> = new Map();

    private _nGramSearch: NGramSearch<StaticSearchCorpusEntry> =
        new NGramSearch<StaticSearchCorpusEntry>([], () => '');

    private _folderNGramSearch: NGramSearch<StaticSearchCorpusEntry> =
        new NGramSearch<StaticSearchCorpusEntry>([], () => '');

    private _currentSearchPhrase: string | null = null;

    // ViewChild refs to the two host divs — the geometry source for the
    // window-driven virtualization. We read getBoundingClientRect() of these
    // against window.innerHeight to figure out which slice is on-screen.
    private _mainHost = viewChild<ElementRef<HTMLElement>>('mainHost');
    private _searchHost = viewChild<ElementRef<HTMLElement>>('searchHost');

    constructor(){
        // Scroll triggers: we listen in CAPTURE phase on the window so we catch
        // scroll events from EVERY scrollable ancestor of the host — not just
        // the window itself. SPA shells frequently put `overflow: auto` on an
        // inner element (router-outlet wrapper, sidenav content, etc.), in
        // which case window.scroll never fires and ViewportRuler stays silent.
        // Capture-phase listening sees the event on its way down through the
        // ancestor chain regardless of which element actually scrolls.
        const onScroll = () => requestAnimationFrame(() => this.recomputeRanges());

        window.addEventListener('scroll', onScroll, { capture: true, passive: true });
        window.addEventListener('resize', onScroll, { passive: true });

        const destroyRef = inject(DestroyRef);
        destroyRef.onDestroy(() => {
            window.removeEventListener('scroll', onScroll, { capture: true });
            window.removeEventListener('resize', onScroll);
        });

        // Content changes (expand/collapse, tree swap, search toggle) shift
        // the host's geometry and the total row count — recompute the ranges
        // after the DOM has been updated.
        effect(() => {
            this.flatVisibleNodes();
            this.flatSearchVisibleNodes();
            this.isSearchActive();

            requestAnimationFrame(() => this.recomputeRanges());
        });

        // Emit the aggregated selection payload as a (selectionChanged) event
        // whenever the cascade settles on a new value.
        effect(() => {
            this.selectionChanged.emit(this.selectionState());
        });
    }

    // Search-result root, held as a signal so flatSearchVisibleNodes recomputes
    // when applyNextSearchPhrase mutates visibility / when buildSubTree returns
    // a fresh subtree on a fresh search phrase.
    private _searchResultRoot: WritableSignal<StaticTreeNode[]> = signal([]);

    // Flat DFS render lists for the virtual scroll. Each emits ONE entry per
    // visible node — children of collapsed folders are skipped, indent is
    // expressed as `depth` so the template can position with padding-left
    // instead of nested DOM. Reactivity: isExpanded() (collapsed folders cut
    // their subtree) plus isVisible() (search narrowing hides results).
    flatVisibleNodes = computed<FlatTreeRow[]>(() => this.flatten(this.fileTree(), false));
    flatSearchVisibleNodes = computed<FlatTreeRow[]>(() => this.flatten(this._searchResultRoot(), true));

    // Fixed row height — drives both the absolute-positioning math and the
    // CSS height of .virtual-row. MUST match exactly or rows overlap/gap.
    static readonly ROW_HEIGHT_PX = 44;
    rowHeightPx = StaticFileTreeViewComponent.ROW_HEIGHT_PX;

    // How many rows above/below the visible window slice to also render —
    // smooths over the gap during fast scrolling so the user doesn't see
    // unrendered rows before the next recompute lands. With `track row.node`
    // the DOM is reused across slice shifts, so a generous buffer is cheap.
    private static readonly RENDER_BUFFER_ROWS = 30;

    // Host element grows to the full virtual content height — that's what
    // makes the PAGE scrollbar (rather than a viewport-local one) reflect the
    // real tree size. Rows are positioned absolutely within this height.
    mainTotalHeightPx = computed(() => this.flatVisibleNodes().length * StaticFileTreeViewComponent.ROW_HEIGHT_PX);
    searchTotalHeightPx = computed(() => this.flatSearchVisibleNodes().length * StaticFileTreeViewComponent.ROW_HEIGHT_PX);

    // {start, end} index of rows to actually render. Updated by
    // recomputeRanges() on window scroll/resize and on tree-content changes.
    private _mainRenderedRange: WritableSignal<{ start: number; end: number }> = signal({ start: 0, end: 0 });
    private _searchRenderedRange: WritableSignal<{ start: number; end: number }> = signal({ start: 0, end: 0 });

    // Slice of the flat list to actually render, with absolute index attached
    // (used as `top = index * ROW_HEIGHT_PX` in the template).
    mainRenderedRows = computed(() => this.sliceForRange(this.flatVisibleNodes(), this._mainRenderedRange()));
    searchRenderedRows = computed(() => this.sliceForRange(this.flatSearchVisibleNodes(), this._searchRenderedRange()));

    private sliceForRange(
        flat: FlatTreeRow[],
        range: { start: number; end: number }
    ): (FlatTreeRow & { index: number })[] {
        const end = Math.min(range.end, flat.length);
        const out: (FlatTreeRow & { index: number })[] = [];

        for (let i = range.start; i < end; i++) {
            out.push({ ...flat[i], index: i });
        }

        return out;
    }

    // Inspects the host's position within the window viewport and updates the
    // rendered range to cover only the on-screen slice (plus buffer). Called
    // on window scroll/resize and on every content change.
    private recomputeRanges(): void {
        const mainEl = this._mainHost()?.nativeElement;
        if (mainEl && !this.isSearchActive()) {
            this._mainRenderedRange.set(
                this.computeRange(mainEl, this.flatVisibleNodes().length));
        }

        const searchEl = this._searchHost()?.nativeElement;
        if (searchEl && this.isSearchActive()) {
            this._searchRenderedRange.set(
                this.computeRange(searchEl, this.flatSearchVisibleNodes().length));
        }
    }

    private computeRange(host: HTMLElement, itemCount: number): { start: number; end: number } {
        if (itemCount === 0) return { start: 0, end: 0 };

        const rect = host.getBoundingClientRect();
        const winHeight = typeof window !== 'undefined' ? window.innerHeight : 800;

        // Pixels of the host that sit above the viewport's top edge.
        const overflowAbove = Math.max(0, -rect.top);
        // Pixels of the host that fit inside the viewport from rect.top down.
        const visiblePx = Math.max(0, Math.min(rect.height, winHeight - Math.max(0, rect.top)));

        const startIdx = Math.floor(overflowAbove / StaticFileTreeViewComponent.ROW_HEIGHT_PX);
        const endIdx = Math.ceil((overflowAbove + visiblePx) / StaticFileTreeViewComponent.ROW_HEIGHT_PX);

        const buffer = StaticFileTreeViewComponent.RENDER_BUFFER_ROWS;
        return {
            start: Math.max(0, startIdx - buffer),
            end: Math.min(itemCount, endIdx + buffer)
        };
    }

    private flatten(roots: StaticTreeNode[], respectVisibility: boolean): FlatTreeRow[] {
        const result: FlatTreeRow[] = [];
        const stack: FlatTreeRow[] = [];

        // Push in reverse so DFS pops first-child-first.
        for (let i = roots.length - 1; i >= 0; i--) {
            stack.push({ node: roots[i], depth: 0 });
        }

        while (stack.length > 0) {
            const row = stack.pop()!;
            const { node, depth } = row;

            if (respectVisibility && !node.isVisible()) {
                continue;
            }

            result.push(row);

            if (node.type === 'folder' && node.isExpanded()) {
                for (let i = node.children.length - 1; i >= 0; i--) {
                    stack.push({ node: node.children[i], depth: depth + 1 });
                }
            }
        }

        return result;
    }

    trackByNodeId = (_: number, row: FlatTreeRow) => row.node.id;

    // Tracks whether the search-related structures (n-gram indexes + the
    // _originalNameById map) reflect the current tree. Reset on every fileTree
    // change and refreshed lazily in ensureSearchIndexBuilt() — so a tree that's
    // never searched never pays for indexing.
    private _searchIndexBuilt = false;

    ngOnChanges(changes: SimpleChanges) {
        if (changes['fileTree']) {
            // Reset search-side state; rebuilt lazily on the first search.
            this._originalNameById = new Map();
            this._nGramSearch = new NGramSearch<StaticSearchCorpusEntry>([], () => '');
            this._folderNGramSearch = new NGramSearch<StaticSearchCorpusEntry>([], () => '');
            this._searchIndexBuilt = false;
            this._currentSearchPhrase = null;
            this._searchResultRoot.set([]);
        }

        if(changes['searchPhrase']) {
           this.tryApplySearchPhrase();
        }
    }

    // Walks fileTree to build the n-gram search corpus. For folders that expose
    // `enumerateDescendantsForSearch` (lazy trees, e.g. ZipArchive), we pull
    // their full subtree metadata from source data WITHOUT materializing any
    // StaticTreeNode — that's the whole point. For folders without the hook
    // (eager trees, e.g. trash/quick-share) we walk `children` recursively.
    private ensureSearchIndexBuilt(): void {
        if (this._searchIndexBuilt) return;

        const files: StaticSearchCorpusEntry[] = [];
        const folders: StaticSearchCorpusEntry[] = [];

        const addEntry = (entry: StaticSearchCorpusEntry) => {
            if (entry.type === 'file') files.push(entry);
            else folders.push(entry);
            this._originalNameById.set(entry.id, entry.name);
        };

        const stack: { node: StaticTreeNode; ancestors: string[] }[] =
            this.fileTree().map(node => ({ node, ancestors: [] }));

        while (stack.length > 0) {
            const { node, ancestors } = stack.pop()!;

            if (node.type === 'file') {
                addEntry({
                    id: node.id,
                    name: node.fullName,
                    nameLower: node.fullNameLower,
                    type: 'file',
                    ancestorFolderIds: ancestors
                });

                continue;
            }

            addEntry({
                id: node.id,
                name: node.name,
                nameLower: node.nameLower,
                type: 'folder',
                ancestorFolderIds: ancestors
            });

            if (node.enumerateDescendantsForSearch) {
                // Lazy folder — source data knows its subtree without us having
                // to materialize. Re-anchor each entry's ancestor chain so it's
                // absolute (from fileTree root) rather than relative to `node`.
                const childAncestorPrefix = [...ancestors, node.id];

                for (const descendant of node.enumerateDescendantsForSearch()) {
                    addEntry({
                        ...descendant,
                        ancestorFolderIds: [
                            ...childAncestorPrefix,
                            ...descendant.ancestorFolderIds
                        ]
                    });
                }
            } else {
                // Eager folder — walk its already-built children.
                const childAncestors = [...ancestors, node.id];
                for (const child of node.children) {
                    stack.push({ node: child, ancestors: childAncestors });
                }
            }
        }

        this._nGramSearch = new NGramSearch<StaticSearchCorpusEntry>(
            files,
            entry => entry.nameLower);

        this._folderNGramSearch = new NGramSearch<StaticSearchCorpusEntry>(
            folders,
            entry => entry.nameLower);

        this._searchIndexBuilt = true;
    }

    // Walk down the tree along ancestorFolderIds, invoking loadChildren on each
    // unmaterialized folder. After this returns, every folder on the path has
    // its `children` populated (and idempotent loadChildren = null), so
    // buildSubTree can reach the match by walking fileTree normally. Folders
    // OFF the path stay lazy.
    private materializePath(ancestorFolderIds: string[]): void {
        let currentLevel: StaticTreeNode[] = this.fileTree();

        for (const folderId of ancestorFolderIds) {
            const folder = currentLevel.find(
                n => n.type === 'folder' && n.id === folderId) as StaticFolderNode | undefined;

            if (!folder) return;

            if (folder.loadChildren) {
                folder.loadChildren();
                folder.loadChildren = null;
            }

            currentLevel = folder.children;
        }
    }

    private tryApplySearchPhrase() {
        const searchPhrase = this.searchPhrase();

        if(!searchPhrase){
            this._currentSearchPhrase = null;
            return;
        }

        if(searchPhrase.length < 3){
            this._currentSearchPhrase = null;
            return;
        }

        this.ensureSearchIndexBuilt();

        const searchPhraseLower = searchPhrase.toLowerCase();

        if(this._currentSearchPhrase && searchPhraseLower.startsWith(this._currentSearchPhrase)) {
            //if new search phrase starts with the current search phrase it means we dont have to rebuild the whole
            //tree from matching entries, we can do a cheaper operation - narrow down the current result. That wont
            //require a DOM rebuild, we will simply hide the results which no longer matches.

            this.applyNextSearchPhrase({
                nodes: this._searchResultRoot(),
                searchPhraseLower: searchPhraseLower
            });

            // Force a re-emit of the search root so the flatSearchVisibleNodes
            // computed picks up the in-place name/visibility mutations.
            this._searchResultRoot.set([...this._searchResultRoot()]);
        } else {
            //we search matchin entries a compose an archive using only those
            //that allows to limit the size of DOM we need to render

            const matchingFiles = this
                ._nGramSearch
                .search(searchPhraseLower);

            const matchingFolders = this
                ._folderNGramSearch
                .search(searchPhraseLower);

            // Materialize only the paths to actual matches. Subtrees with no
            // hits stay unbuilt — for a deep archive with a couple of matches
            // this is the difference between materializing a handful of nodes
            // and the whole tree.
            for (const match of matchingFiles) {
                this.materializePath(match.ancestorFolderIds);
            }
            
            for (const match of matchingFolders) {
                this.materializePath(match.ancestorFolderIds);
            }

            const matchingFileIds = new Set(matchingFiles.map(f => f.id));
            const matchingFolderIds = new Set(matchingFolders.map(f => f.id));

            const treeData = this.buildSubTree(
                matchingFileIds,
                matchingFolderIds,
                searchPhraseLower);

            this._searchResultRoot.set(treeData);
            this._currentSearchPhrase = searchPhraseLower;
        }
    }

    private buildSubTree(
        matchingFileIds: Set<string>,
        matchingFolderIds: Set<string>,
        searchPhraseLower: string): StaticTreeNode[] {
        // Walk the original tree top-down in its native order. A node is kept iff
        // it matches itself or any descendant does — folder match does NOT cascade
        // into its contents, only the folder itself is highlighted. Ancestors of
        // matches stay as path context with their original names.
        const cloneSubtree = (node: StaticTreeNode): StaticTreeNode | null => {
            if (node.type === 'file') {
                if (!matchingFileIds.has(node.id)) return null;

                return {
                    type: 'file',
                    id: node.id,
                    extension: node.extension,
                    fullNameLower: node.fullNameLower,
                    sizeInBytes: node.sizeInBytes,

                    fullName: getNameWithHighlight(node.fullName, searchPhraseLower),

                    isVisible: signal(true),

                    isSelected: node.isSelected,
                    isExcluded: node.isExcluded,
                    parent: node.parent,
                    isParentSelected: node.isParentSelected,
                    isParentExcluded: node.isParentExcluded
                };
            }

            const folderMatches = matchingFolderIds.has(node.id);

            const clonedChildren: StaticTreeNode[] = [];
            for (const child of node.children) {
                const clone = cloneSubtree(child);
                if (clone) clonedChildren.push(clone);
            }

            if (!folderMatches && clonedChildren.length === 0) {
                return null;
            }

            // Search-result clones share isSelected/isExcluded signals with
            // the originals, so checkbox clicks propagate to the main tree.
            // subtreeState here is plain (no signal-of-signal) because
            // clonedChildren is built fully upfront — no lazy mutation.
            const cloneSubtreeState = computed(() => collectSelectionFromChildren(clonedChildren));

            return {
                type: 'folder',
                id: node.id,
                name: folderMatches
                    ? getNameWithHighlight(node.name, searchPhraseLower)
                    : node.name,
                nameLower: node.nameLower,
                children: clonedChildren,
                isExpanded: signal(true),
                isVisible: signal(true),

                isSelected: node.isSelected,
                isExcluded: node.isExcluded,
                parent: node.parent,
                isParentSelected: node.isParentSelected,
                isParentExcluded: node.isParentExcluded,
                subtreeState: cloneSubtreeState,
                selectedDescendantsCount: computed(() => {
                    const s = cloneSubtreeState();
                    return s.selectedFolderIds.length + s.selectedFileIds.length;
                })
            };
        };

        const result: StaticTreeNode[] = [];

        for (const root of this.fileTree()) {
            const clone = cloneSubtree(root);
            if (clone) result.push(clone);
        }

        return result;
    }

    private applyNextSearchPhrase(args: {nodes: StaticTreeNode[], searchPhraseLower: string}) {
        type NodeToProcess = {
            node: StaticTreeNode;
            depth: number;
        };

        // File is visible iff its own name matches. Folder is visible iff its name
        // matches OR any descendant is visible (i.e. shown as path context). Folder
        // match does NOT auto-include children — they have to qualify on their own.
        const allNodes: NodeToProcess[] = [];
        const nodesToTraverse: NodeToProcess[] =
            args.nodes.map(node => ({node, depth: 0}));

        while (nodesToTraverse.length > 0) {
            const current = nodesToTraverse.pop()!;
            allNodes.push(current);

            if (current.node.type === 'folder') {
                nodesToTraverse.push(
                    ...current.node.children.map(child => ({
                        node: child,
                        depth: current.depth + 1
                    }))
                );
            }
        }

        // Sort by depth descending so a folder's children resolve visibility before
        // the folder asks "do I have a visible child?".
        allNodes.sort((a, b) => b.depth - a.depth);

        for (const {node} of allNodes) {
            if (node.type === 'file') {
                const fileMatches = node.fullNameLower.includes(args.searchPhraseLower);

                if (fileMatches) {
                    const originalName = this._originalNameById.get(node.id);
                    if(originalName === undefined) {
                        throw new Error(`Could not find original name for FileNode id: '${node.id}' while narrowing down search results`);
                    }

                    node.fullName = getNameWithHighlight(originalName, args.searchPhraseLower);
                }

                node.isVisible.set(fileMatches);
            } else if (node.type === 'folder') {
                const folderMatches = node.nameLower.includes(args.searchPhraseLower);
                const hasVisibleChild = node.children.some(child => child.isVisible());
                const isVisible = folderMatches || hasVisibleChild;

                if (isVisible) {
                    const originalName = this._originalNameById.get(node.id);
                    if(originalName === undefined) {
                        throw new Error(`Could not find original name for FolderNode id: '${node.id}' while narrowing down search results`);
                    }

                    node.name = folderMatches
                        ? getNameWithHighlight(originalName, args.searchPhraseLower)
                        : originalName;
                }

                node.isVisible.set(isVisible);
            }
        }
    }



    expand(node: StaticFolderNode) {
        if (node.loadChildren) {
            node.loadChildren();
            node.loadChildren = null;
        }

        toggle(node.isExpanded);
    }

    // Clicking a folder checkbox cascades: pulling a folder INTO the selection
    // drops any individual descendant selections (they would otherwise duplicate
    // entries in the payload); pulling a folder OUT of the selection wipes any
    // descendant exclusions (excludes are only meaningful under a selected root).
    onIsSelectedChange(node: StaticTreeNode, isSelected: boolean) {
        node.isSelected.set(isSelected);

        if (node.type === 'folder') {
            for (const child of this.getAllDescendantNodes(node)) {
                if (isSelected && child.isSelected()) {
                    child.isSelected.set(false);
                }
                if (!isSelected && child.isExcluded()) {
                    child.isExcluded.set(false);
                }
            }
        }
    }

    // Un-excluding a folder also un-excludes all descendants — a child exclude is
    // only meaningful within a still-excluded ancestor, so cleaning up here keeps
    // the visible checkbox state and the eventual payload consistent.
    onIsExcludedChange(node: StaticTreeNode, isExcluded: boolean) {
        node.isExcluded.set(isExcluded);

        if (node.type === 'folder' && !isExcluded) {
            for (const child of this.getAllDescendantNodes(node)) {
                if (child.isExcluded()) {
                    child.isExcluded.set(false);
                }
            }
        }
    }

    private *getAllDescendantNodes(node: StaticFolderNode): Generator<StaticTreeNode> {
        for (const child of node.children) {
            yield child;

            if (child.type === 'folder') {
                yield* this.getAllDescendantNodes(child);
            }
        }
    }
}