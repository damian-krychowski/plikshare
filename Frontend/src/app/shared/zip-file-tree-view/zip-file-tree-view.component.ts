import { Component, computed, effect, input, Input, InputSignal, OnChanges, output, Signal, signal, SimpleChanges, ViewEncapsulation, WritableSignal } from '@angular/core';
import { MatTreeModule, MatTreeNestedDataSource } from '@angular/material/tree';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';
import { StorageSizePipe } from '../storage-size.pipe';
import { ActionButtonComponent } from '../buttons/action-btn/action-btn.component';
import { FileIconPipe } from '../../files-explorer/file-icon-pipe/file-icon.pipe';
import { toggle } from '../signal-utils';
import { NGramSearch } from '../../services/n-gram-search';
import { getNameWithHighlight } from '../name-with-highlight';

export type ZipTreeNode = ZipFileNode | ZipFolderNode;

export type ZipFileNode = {
    type: 'file';
    id: string;

    extension: string | null;

    fullName: string;
    fullNameLower: string;
    
    sizeInBytes: number;

    isVisible: WritableSignal<boolean>;
}

export type ZipFolderNode = {
    type: 'folder';
    id: string;

    name: string;
    children: ZipTreeNode[];
    isExpanded: WritableSignal<boolean>;
    wasRendered: Signal<boolean>;

    isVisible: WritableSignal<boolean>;
    wasLoaded: boolean;
}

@Component({
    selector: 'app-zip-file-tree-view',
    imports: [
        CommonModule,
        MatTreeModule,
        MatIconModule,
        MatButtonModule,
        FileIconPipe,
        StorageSizePipe,
        ActionButtonComponent
    ],
    templateUrl: './zip-file-tree-view.component.html',
    styleUrls: ['./zip-file-tree-view.component.scss'],
    encapsulation: ViewEncapsulation.None
})
export class ZipFileTreeViewComponent implements OnChanges {
    fileTree = input.required<ZipTreeNode[]>();
    searchPhrase = input<string>();
    canDownload = input(true);
    
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
            const {files, filesMap, foldersMap, pathMap} = this.getTreeStructures(nodes);

            this._filesMap = filesMap;
            this._foldersMap = foldersMap;
            this._pathMap = pathMap;

            this._nGramSearch = new NGramSearch<ZipFileNode>(
                files, 
                file => file.fullNameLower);

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

            const treeData = this.buildSubTree(
                matchingFiles,
                searchPhraseLower);

            this.searchResultDataSource.data = treeData;
            this._currentSearchPhrase = searchPhraseLower;
        }       
    }

    private buildSubTree(files: ZipFileNode[], searchPhraseLower: string): ZipTreeNode[] {
        const rootLevelNodes: ZipTreeNode[] = [];

        for (let index = 0; index < files.length; index++) {
            const file = files[index];
            const path = this._pathMap.get(file.id);

            if(!path) {
                throw new Error(`Cound not find folder path for fileNode: '${file.id}'`)
            }

            const newFile: ZipFileNode = {
                type: 'file',
                id: file.id,
                extension: file.extension,
                fullNameLower: file.fullNameLower,
                sizeInBytes: file.sizeInBytes,

                fullName: getNameWithHighlight(
                    file.fullName, 
                    searchPhraseLower),
                
                isVisible: signal(true),
            };

            if(path.length == 0) {
                rootLevelNodes.push(newFile);
            } else {
                let currentNodes = rootLevelNodes;

                for (let pathIndex = 0; pathIndex < path.length; pathIndex++) {
                    const folderId = path[pathIndex];
    
                    const folder = this._foldersMap.get(folderId);

                    if(!folder) {
                        throw new Error(`Could not find folder with id: '${folderId}'`);
                    }

                    const existingFolder: ZipFolderNode | undefined = currentNodes
                        .find((n: ZipTreeNode): n is ZipFolderNode => n.type == 'folder' && n.id === folder.id);

                    if(existingFolder) {
                        currentNodes = existingFolder.children;
                    } else {
                        const newFolder: ZipFolderNode = {
                            type: 'folder',
                            id: folder.id,
                            name: folder.name,
                            children: [],
                            isExpanded: signal(true),
                            isVisible: signal(true),
                            wasRendered: signal(true),
                            wasLoaded: true
                        };
    
                        currentNodes.push(newFolder);
                        currentNodes = newFolder.children;
                    }
                }

                currentNodes.push(newFile);
            }
        }

        return rootLevelNodes;
    }

    private applyNextSearchPhrase(args: {nodes: ZipTreeNode[], searchPhraseLower: string}) {
        // Structure to track nodes to process and their parents
        type NodeToProcess = {
            node: ZipTreeNode;
            depth: number;
        };
        
        // First collect all nodes with their depth and parent information
        const allNodes: NodeToProcess[] = [];
        const nodesToTraverse: {node: ZipTreeNode, depth: number}[] = 
            args.nodes.map(node => ({node, depth: 0}));
        
        // Build flat list of all nodes with their parents and depths
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
        
        // Sort by depth descending to process bottom-up
        allNodes.sort((a, b) => b.depth - a.depth);
        
        // Process each node
        for (const {node} of allNodes) {
            if (node.type === 'file') {
                const isMatchingNewPhrase = node
                    .fullNameLower
                    .includes(args.searchPhraseLower)
                
                if (isMatchingNewPhrase) {
                    const originalFile = this
                        ._filesMap
                        .get(node.id);

                    if(!originalFile) {
                        throw new Error(`Could not find FileNode with id: '${node.id}' while narrowing down search results`);
                    }

                    node.isVisible.set(true);

                    node.fullName = getNameWithHighlight(
                        originalFile.fullName, 
                        args.searchPhraseLower);
                } else {
                    node.isVisible.set(false);
                }
            } else if (node.type === 'folder') {
                // Folder is visible if any child is visible
                const hasVisibleChild = node.children.some(
                    child => child.isVisible()
                );
                node.isVisible.set(hasVisibleChild);
            }
        }
    }



    expand(node: ZipFolderNode) {
        toggle(node.isExpanded);
    }

    private getTreeStructures(nodes: ZipTreeNode[]) {
        const pathMap: Map<string, string[]> = new Map<string, string[]>();       
        const filesMap: Map<string, ZipFileNode> = new Map<string, ZipFileNode>(); 
        const foldersMap: Map<string, ZipFolderNode> = new Map<string, ZipFolderNode>(); 
        const files: ZipFileNode[] = [];
        
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
                foldersMap.set(node.id, node);

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
            filesMap,
            foldersMap,
            pathMap
        };
    }
}