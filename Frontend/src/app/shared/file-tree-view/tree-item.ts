import { WritableSignal, Signal } from "@angular/core";
import { AppFileItem } from "../file-item/file-item.component";
import { AppFolderItem } from "../folder-item/folder-item.component";

export type TreeViewMode = 'show-all' | 'show-only-selected';

export type AppTreeItem = AppFolderItem | AppFileItem;

export type TreeItem = FileTreeItem | FolderTreeItem;

export type FileTreeItem = {
    type: 'file';
    item: AppFileItem;

    isExcluded: WritableSignal<boolean>;

    isSearched: WritableSignal<boolean>;
    nameWithHighlight: WritableSignal<string>;

    parent: Signal<FolderTreeItem | null>;
    isParentSelected: Signal<boolean>;
    isParentExcluded: Signal<boolean>;
    fullPath: Signal<string | null>;

    canPreview: Signal<boolean>;
}

export type FolderTreeItem = {
    type: 'folder';
    item: AppFolderItem;

    children: WritableSignal<TreeItem[]>

    isExcluded: WritableSignal<boolean>;
    isExpanded: WritableSignal<boolean>;

    isSearched: WritableSignal<boolean>;
    nameWithHighlight: WritableSignal<string>;

    wasRendered: Signal<boolean>;
    wasLoaded: boolean;

    parent: Signal<FolderTreeItem | null>;
    isParentSelected: Signal<boolean>;
    isParentExcluded: Signal<boolean>;
    fullPath: Signal<string | null>;

    selectedChildrenCount: Signal<number>;
    searchedChildrenCount: Signal<number>;
}