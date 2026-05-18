import { Component, computed, effect, input, Input, InputSignal, OnChanges, output, Signal, signal, SimpleChanges, WritableSignal } from '@angular/core';
import { MatTreeModule, MatTreeNestedDataSource } from '@angular/material/tree';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

import { StorageSizePipe } from '../storage-size.pipe';
import { ActionButtonComponent } from '../buttons/action-btn/action-btn.component';
import { FileIconPipe } from '../../files-explorer/file-icon-pipe/file-icon.pipe';
import { toggle } from '../signal-utils';
import { NGramSearch } from '../../services/n-gram-search';
import { getNameWithHighlight } from '../name-with-highlight';
import { TreeCheckobxComponent } from '../file-tree-view/tree-checkbox/tree-checkbox.component';

// 'select' shows tri-state checkboxes that feed a bulk-download payload (used by
// zip preview inside a workspace). 'download' shows per-file download icons and
// emits fileDownloadClicked — the legacy quick-share UX where each file is
// fetched on its own through a presigned link.
export type ZipFileTreeViewMode = 'select' | 'download';

export type ZipTreeNode = ZipFileNode | ZipFolderNode;

export function countSelectedDescendants(children: ZipTreeNode[]): number {
    let count = 0;

    for (const child of children) {
        if (child.isSelected()) {
            count += 1;
        } else if (child.type === 'folder') {
            count += child.selectedDescendantsCount();
        }
    }

    return count;
}

export type ZipFileNode = {
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
    parent: ZipFolderNode | null;
    isParentSelected: Signal<boolean>;
    isParentExcluded: Signal<boolean>;
}

export type ZipFolderNode = {
    type: 'folder';
    id: string;

    name: string;
    nameLower: string;
    children: ZipTreeNode[];
    isExpanded: WritableSignal<boolean>;
    wasRendered: Signal<boolean>;

    isVisible: WritableSignal<boolean>;
    wasLoaded: boolean;

    // See ZipFileNode comment — same cascading-selection contract applies.
    isSelected: WritableSignal<boolean>;
    isExcluded: WritableSignal<boolean>;
    parent: ZipFolderNode | null;
    isParentSelected: Signal<boolean>;
    isParentExcluded: Signal<boolean>;

    selectedDescendantsCount: Signal<number>;
}

@Component({
    selector: 'app-zip-file-tree-view',
    imports: [
        MatTreeModule,
        MatIconModule,
        MatButtonModule,
        FileIconPipe,
        StorageSizePipe,
        TreeCheckobxComponent,
        ActionButtonComponent
    ],
    templateUrl: './zip-file-tree-view.component.html',
    styleUrls: ['./zip-file-tree-view.component.scss']
})
export class ZipFileTreeViewComponent implements OnChanges {
    fileTree = input.required<ZipTreeNode[]>();
    searchPhrase = input<string>();
    canDownload = input(true);
    mode = input<ZipFileTreeViewMode>('select');

    fileClicked = output<ZipFileNode>();
    fileDownloadClicked = output<ZipFileNode>();

    isSearchActive = computed(() => {
        const phrase = this.searchPhrase();

        return phrase && phrase.length >= 3;
    });

    private _filesMap: Map<string, ZipFileNode> = new Map();
    private _foldersMap: Map<string, ZipFolderNode> = new Map();
    private _pathMap: Map<string, string[]> = new Map();
    
    private _nGramSearch: NGramSearch<ZipFileNode> = new NGramSearch<ZipFileNode>([], () => '');
    private _folderNGramSearch: NGramSearch<ZipFolderNode> = new NGramSearch<ZipFolderNode>([], () => '');

    private _currentSearchPhrase: string | null = null;

    childrenAccessor = (node: ZipTreeNode) => {
        if(node.type == 'folder')
            return node.children;

        return [];
    }

    constructor(){
        }

    dataSource = new MatTreeNestedDataSource<ZipTreeNode>();
    searchResultDataSource = new MatTreeNestedDataSource<ZipTreeNode>();

    ngOnChanges(changes: SimpleChanges) {
        if (changes['fileTree']) {
            const nodes = this.fileTree();
            const {files, folders, filesMap, foldersMap, pathMap} = this.getTreeStructures(nodes);

            this._filesMap = filesMap;
            this._foldersMap = foldersMap;
            this._pathMap = pathMap;

            this._nGramSearch = new NGramSearch<ZipFileNode>(
                files,
                file => file.fullNameLower);

            this._folderNGramSearch = new NGramSearch<ZipFolderNode>(
                folders,
                folder => folder.nameLower);

            this.dataSource.data = nodes;
        }

        if(changes['searchPhrase']) {
           this.tryApplySearchPhrase();
        }
    }

    isFolder = (_: number, node: ZipTreeNode) => node.type == 'folder';
    isFile = (_: number, node: ZipTreeNode) => node.type == 'file';

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

        const searchPhraseLower = searchPhrase.toLowerCase();

        if(this._currentSearchPhrase && searchPhraseLower.startsWith(this._currentSearchPhrase)) {
            //if new search phrase starts with the current search phrase it means we dont have to rebuild the whole zip
            //archive from matching entries, we can do a cheaper operation - narrow down the current result. That wont
            //require a DOM rebuild, we will simply hide the results which no longer matches.

            this.applyNextSearchPhrase({
                nodes: this.searchResultDataSource.data,
                searchPhraseLower: searchPhraseLower
            });
        } else {
            //we search matchin entries a compose an archive using only those
            //that allows to limit the size of DOM we need to render

            const matchingFiles = this
                ._nGramSearch
                .search(searchPhraseLower);

            const matchingFolders = this
                ._folderNGramSearch
                .search(searchPhraseLower);

            const treeData = this.buildSubTree(
                matchingFiles,
                matchingFolders,
                searchPhraseLower);

            this.searchResultDataSource.data = treeData;
            this._currentSearchPhrase = searchPhraseLower;
        }
    }

    private buildSubTree(
        files: ZipFileNode[],
        folders: ZipFolderNode[],
        searchPhraseLower: string): ZipTreeNode[] {
        const matchingFileIds = new Set(files.map(f => f.id));
        const matchingFolderIds = new Set(folders.map(f => f.id));

        // Walk the original tree top-down in its native order. A node is kept iff
        // it matches itself or any descendant does — folder match does NOT cascade
        // into its contents, only the folder itself is highlighted. Ancestors of
        // matches stay as path context with their original names.
        const cloneSubtree = (node: ZipTreeNode): ZipTreeNode | null => {
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

            const clonedChildren: ZipTreeNode[] = [];
            for (const child of node.children) {
                const clone = cloneSubtree(child);
                if (clone) clonedChildren.push(clone);
            }

            if (!folderMatches && clonedChildren.length === 0) {
                return null;
            }

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
                wasRendered: signal(true),
                wasLoaded: true,

                isSelected: node.isSelected,
                isExcluded: node.isExcluded,
                parent: node.parent,
                isParentSelected: node.isParentSelected,
                isParentExcluded: node.isParentExcluded,
                selectedDescendantsCount: computed(() => countSelectedDescendants(clonedChildren))
            };
        };

        const result: ZipTreeNode[] = [];
        for (const root of this.fileTree()) {
            const clone = cloneSubtree(root);
            if (clone) result.push(clone);
        }

        return result;
    }

    private applyNextSearchPhrase(args: {nodes: ZipTreeNode[], searchPhraseLower: string}) {
        type NodeToProcess = {
            node: ZipTreeNode;
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
                    const originalFile = this._filesMap.get(node.id);
                    if(!originalFile) {
                        throw new Error(`Could not find FileNode with id: '${node.id}' while narrowing down search results`);
                    }

                    node.fullName = getNameWithHighlight(originalFile.fullName, args.searchPhraseLower);
                }

                node.isVisible.set(fileMatches);
            } else if (node.type === 'folder') {
                const folderMatches = node.nameLower.includes(args.searchPhraseLower);
                const hasVisibleChild = node.children.some(child => child.isVisible());
                const isVisible = folderMatches || hasVisibleChild;

                if (isVisible) {
                    const originalFolder = this._foldersMap.get(node.id);
                    if(!originalFolder) {
                        throw new Error(`Could not find FolderNode with id: '${node.id}' while narrowing down search results`);
                    }

                    node.name = folderMatches
                        ? getNameWithHighlight(originalFolder.name, args.searchPhraseLower)
                        : originalFolder.name;
                }

                node.isVisible.set(isVisible);
            }
        }
    }



    expand(node: ZipFolderNode) {
        toggle(node.isExpanded);
    }

    // Clicking a folder checkbox cascades: pulling a folder INTO the selection
    // drops any individual descendant selections (they would otherwise duplicate
    // entries in the payload); pulling a folder OUT of the selection wipes any
    // descendant exclusions (excludes are only meaningful under a selected root).
    onIsSelectedChange(node: ZipTreeNode, isSelected: boolean) {
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
    onIsExcludedChange(node: ZipTreeNode, isExcluded: boolean) {
        node.isExcluded.set(isExcluded);

        if (node.type === 'folder' && !isExcluded) {
            for (const child of this.getAllDescendantNodes(node)) {
                if (child.isExcluded()) {
                    child.isExcluded.set(false);
                }
            }
        }
    }

    private *getAllDescendantNodes(node: ZipFolderNode): Generator<ZipTreeNode> {
        for (const child of node.children) {
            yield child;
            if (child.type === 'folder') {
                yield* this.getAllDescendantNodes(child);
            }
        }
    }

    private getTreeStructures(nodes: ZipTreeNode[]) {
        const pathMap: Map<string, string[]> = new Map<string, string[]>();
        const filesMap: Map<string, ZipFileNode> = new Map<string, ZipFileNode>();
        const foldersMap: Map<string, ZipFolderNode> = new Map<string, ZipFolderNode>();
        const files: ZipFileNode[] = [];
        const folders: ZipFolderNode[] = [];

        type NodeWithPath = {
            node: ZipTreeNode;
            path: string[];
        };

        const stack: NodeWithPath[] = [];

        const topNodes: NodeWithPath[] = nodes.map(node => ({
            node: node,
            path: []
        }));

        stack.push(...topNodes);

        while(stack.length > 0) {
            const item = stack.pop();

            if(!item) continue;

            const node = item.node;

            if(node.type == 'file') {
                files.push(node);
                filesMap.set(node.id, node);
                pathMap.set(node.id, item.path);
            } else if(node.type == 'folder') {
                folders.push(node);
                foldersMap.set(node.id, node);
                pathMap.set(node.id, item.path);

                const folderPath = [...item.path, node.id];

                const children: NodeWithPath[] = node.children.map(child => ({
                    node: child,
                    path: folderPath
                }));

                stack.push(...children);
            } else {
                throw new Error(`Unknown FileTreeNode type: '${(item as any).type}'`)
            }
        }

        return {
            files,
            folders,
            filesMap,
            foldersMap,
            pathMap
        };
    }
}