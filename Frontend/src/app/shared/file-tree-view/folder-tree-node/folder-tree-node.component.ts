import { CommonModule } from "@angular/common";
import { Component, input } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatTreeModule } from "@angular/material/tree";
import { TreeCheckobxComponent } from "../tree-checkbox/tree-checkbox.component";
import { FileTreeItem, FolderTreeItem, TreeItem, TreeViewMode } from "../tree-item";
import { toggle } from "../../signal-utils";
import { FileTreeNodeComponent } from "../file-tree-node/file-tree-node.component";

@Component({
    selector: 'app-folder-tree-node',
    imports: [
        FormsModule,
        CommonModule,
        MatTreeModule,
        MatIconModule,
        MatButtonModule,
        TreeCheckobxComponent,
        FileTreeNodeComponent
    ],
    templateUrl: './folder-tree-node.component.html',
    styleUrls: ['./folder-tree-node.component.scss']
})
export class FolderTreeNodeComponent {
    folder = input.required<FolderTreeItem>();
    canSelect = input.required<boolean>();
    viewMode = input.required<TreeViewMode>();
    isSearchActive = input.required<boolean>();
    
    fileClickedHandler = input.required<(node: FileTreeItem) => void>();
    setFolderToRootHandler = input.required<(node: FolderTreeItem) => void>();
    prefetchFolderHandler = input.required<(node: FolderTreeItem) => void>();
    loadFolderChildrenHandler = input.required<(node: FolderTreeItem) => void>();
    isSelectedChangedHandler = input.required<(node: TreeItem, isSelected: boolean) => void>();
    isExcludedChangedHandler = input.required<(node: TreeItem, isExcluded: boolean) => void>();

    prefetchFolder() {
        if(this.folder().wasLoaded)
            return;

        this.prefetchFolderHandler()(this.folder());
    }

    expand() {
        const folder = this.folder();

        toggle(folder.isExpanded);
        
        if(folder.wasLoaded)
            return;

        this.loadFolderChildrenHandler()(folder);
    }
}