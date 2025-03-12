import { Component, computed, input, OnChanges, output, signal, SimpleChanges, ViewEncapsulation } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';
import { EntryPageService } from '../../services/entry-page.service';
import { toggle } from '../signal-utils';
import { AppFolderAncestor, AppFolderItem } from '../folder-item/folder-item.component';
import { AppFileItem, AppFileItems } from '../file-item/file-item.component';
import { FormsModule } from '@angular/forms';
import { SearchFilesTreeFileItem, SearchFilesTreeFolderItem, SearchFilesTreeResponse } from '../../services/folders-and-files.api';
import { getNameWithHighlight } from '../name-with-highlight';
import { TreeItem, AppTreeItem, FolderTreeItem, FileTreeItem, TreeViewMode } from './tree-item';
import { FileTreeNodeComponent } from './file-tree-node/file-tree-node.component';
import { FolderTreeNodeComponent } from './folder-tree-node/folder-tree-node.component';
import { Debouncer } from '../../services/debouncer';


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

type TreeItemSelectionState = {
    state: 'selected' | 'excluded';
    node: TreeItem;
}

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

@Component({
    selector: 'app-file-tree-view',
    imports: [
        FormsModule,
        CommonModule,
        MatIconModule,
        MatButtonModule,
        FileTreeNodeComponent,
        FolderTreeNodeComponent
    ],
    templateUrl: './file-tree-view.component.html',
    styleUrls: ['./file-tree-view.component.scss'],
    encapsulation: ViewEncapsulation.None
})
export class FileTreeViewComponent implements OnChanges {
    topLevelItems = input.required<AppTreeItem[]>();
    canSelect = input.required<boolean>();
    isActive = input.required<boolean>();
    viewMode = input.required<TreeViewMode>();
    searchPhrase = input<string>('');

    allowDownload = input(false);
    
    selectionStateChanged = output<FileTreeSelectionState>();
    
    fileClicked = output<AppFileItem>();
    fileDownloadClicked = output<AppFileItem>();

    folderPrefetchRequested = output<AppFolderItem>();
    folderLoadRequested = output<LoadFolderNodeRequest>();
    folderSetToRoot = output<AppFolderItem>();

    itemsDeleted = output<FileTreeDeleteSelectionState>();

    searchRequested = output<FileTreeSearchRequest>();
    searchedFilesSelectionChanged = output<SearchedFilesSelection | null>();

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

    dataSource = signal<TreeItem[]>([]);

    constructor(
        public entryPage: EntryPageService){
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

        for (const folder of this.getFolderNodes(nodes)) {
            map.set(folder.item.externalId, folder);
        }

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

        folder.children.set(newChildren);
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

        return nodes;
    }

    private mapFolderItem(item: AppFolderItem) {
        const isExpandedSignal = signal(false);
        const isSearchedSignal = signal(false);
        
        const parentFolderExternalId = this.getParentExternalIdOfFolder(
            item);

        const {parentSignal, isParentSelectedSignal, isParentExcludedSignal} = this.prepareParentSignals(
            parentFolderExternalId);

        const isExcludedSignal = signal(false);
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

        const isExcludedSignal = signal(false);
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

            canPreview: computed(() => AppFileItems.canPreview(item, this.allowDownload(), true))
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
        for (const folder of this.getFolderNodes(nodes)) {
            if (folder.item.externalId === folderExternalId) {
                return folder;
            }
        }
        
        return null;
    }


    toggleFileSelection(fileNode: FileTreeItem) {
        toggle(fileNode.item.isSelected);
    }

    private onIsSelectedChange(item: TreeItem, isSelected: boolean) {
        item.item.isSelected.set(isSelected);
        
        if(item.type === 'folder') {
            for (const child of this.getAllNodes(item.children())) {
                if(isSelected && child.item.isSelected()) {
                    child.item.isSelected.set(false);
                }
                
                if(!isSelected && child.isExcluded()) {
                    child.isExcluded.set(false);
                }
            }
        }

        this.calculateAndUpdateSelectionState();
        this.calcualteAndUpdateSearchedFilesSelectionState();
    }

    private onIsExcludedChange(item: TreeItem, isExcluded: boolean) {
        item.isExcluded.set(isExcluded);

        if(item.type === 'folder') {
            for (const child of this.getAllNodes(item.children())) {                
                if(!isExcluded && child.isExcluded()) {
                    child.isExcluded.set(false);
                }
            }
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

        const nodes: FileTreeItem[] = []
            
        for (const node of this.getAllNodes(this.nodes())) {
            if(selectedFileExternalIdsSet.has(node.item.externalId)) {
                nodes.push(node as FileTreeItem);
            }
        }

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

    private getSelectionState(): FileTreeSelectionState {
        const result: FileTreeSelectionState = {
            selectedFolderExternalIds: [],
            selectedFileExternalIds: [],
            excludedFolderExternalIds: [],
            excludedFileExternalIds: []
        };

        for (const nodeWithState of this.getSelectedOrExcludedNodes(this.nodes())) {
            if(nodeWithState.state == 'selected') {
                if(nodeWithState.node.type == 'file') {                    
                    result.selectedFileExternalIds.push(nodeWithState.node.item.externalId);
                } else if(nodeWithState.node.type == 'folder') {
                    result.selectedFolderExternalIds.push(nodeWithState.node.item.externalId);                    
                }                
            } else if (nodeWithState.state == 'excluded') {
                if(nodeWithState.node.type == 'file') {                    
                    result.excludedFileExternalIds.push(nodeWithState.node.item.externalId);
                } else if(nodeWithState.node.type == 'folder') {
                    result.excludedFolderExternalIds.push(nodeWithState.node.item.externalId);                    
                }  
            }
        }

        return result;
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

    private *getSelectedOrExcludedNodes(nodes: TreeItem[]): Generator<TreeItemSelectionState> {
        const selectionStack: TreeItem[] = nodes.slice();
        const exlusionStack: TreeItem[] = [];

        while(selectionStack.length > 0) {
            const node = selectionStack.pop();

            if(!node)
                continue;

            if(node.item.isSelected()) {
                yield {
                    node: node,
                    state: 'selected'
                };

                if(node.type == 'folder') {    
                    exlusionStack.push(...node.children());
                }               
            } else if(node.isExcluded()) {
                yield {
                    node: node,
                    state: 'excluded'
                };
            } else {
                if(node.type == 'folder'){
                    selectionStack.push(...node.children());
                }
            }          
        }        

        while(exlusionStack.length > 0) {
            const node = exlusionStack.pop();

            if(!node)
                continue;

            if(node.isExcluded()) {
                yield {
                    node: node,
                    state: 'excluded'
                };
            } else {
                if(node.type == 'folder'){
                    exlusionStack.push(...node.children());
                }
            }
        }
    }

    private *getFolderNodes(nodes: TreeItem[]): Generator<FolderTreeItem> {
        for (const node of this.getAllNodes(nodes)) {
            if(node.type === 'folder')
                yield node;
        }
    }

    private *getAllNodes(nodes: TreeItem[]): Generator<TreeItem> {
        const stack = nodes.slice();

        while (stack.length > 0) {
            const node = stack.pop();
            
            if (!node) {
                continue;
            }
            
            yield node;
            
            if(node.type === 'folder') {
                stack.push(...node.children());
            }
        }
    }

    deleteSelectedItems() {
        const selectedNodes: TreeItem[] = [];

        for (const nodeWithState of this.getSelectedOrExcludedNodes(this.nodes())) {
            if(nodeWithState.state == 'excluded') {
                //cannot progress with deleting if any nodes are excluded
                return;
            } else if (nodeWithState.state == 'selected') {
                selectedNodes.push(nodeWithState.node);
            }
        }

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
    private performSearch(phrase: string) {
        this.unmarkSearchedNodes();

        if(!phrase) {
            this.clearSearch();
            return;
        }

        const currentPhrase = phrase.toLowerCase();
        const lastSearchResponse = this._lastSearchResponse();

        const isNewPhraseANarrowDownOfPrevious = lastSearchResponse
            && lastSearchResponse.phrase 
            && currentPhrase.includes(lastSearchResponse.phrase);

        if(isNewPhraseANarrowDownOfPrevious && !this._searchDebouncer.isOn()){  
            this.executeSearchQuery({
                isNewPhraseANarrowDownOfPrevious: true,
                phrase: phrase
            });
        } else {
            this._searchDebouncer.debounce(() => this.executeSearchQuery({
                isNewPhraseANarrowDownOfPrevious: true,
                phrase: phrase
            }));
        }
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
    }) {
        const currentPhrase = args.phrase.toLowerCase();
        const lastSearchResponse = this._lastSearchResponse();


        if(lastSearchResponse && args.isNewPhraseANarrowDownOfPrevious) {           
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
        for (const node of this.getAllNodes(this.nodes())) {
            if(node.isSearched()){
                node.isSearched.set(false);
            }
        }
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

            levelToProcess.parentNode.children.update(children => [...children, ...childrenToAdd]);
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
        for (const nodeState of this.getSelectedOrExcludedNodes(this.nodes())) {
            if(nodeState.state === 'excluded') {
                nodeState.node.isExcluded.set(false);
            }

            if(nodeState.state === 'selected') {
                nodeState.node.item.isSelected.set(false);
            }
        }

        this.calculateAndUpdateSelectionState();
        this.calcualteAndUpdateSearchedFilesSelectionState();
    }
}