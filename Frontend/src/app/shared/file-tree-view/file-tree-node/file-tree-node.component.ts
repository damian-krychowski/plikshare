import { CommonModule } from "@angular/common";
import { Component, input } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatTreeModule } from "@angular/material/tree";
import { FileIconPipe } from "../../../files-explorer/file-icon-pipe/file-icon.pipe";
import { StorageSizePipe } from "../../storage-size.pipe";
import { TreeCheckobxComponent } from "../tree-checkbox/tree-checkbox.component";
import { FileTreeItem, TreeItem, TreeViewMode } from "../tree-item";

@Component({
    selector: 'app-file-tree-node',
    imports: [
        FormsModule,
        CommonModule,
        MatTreeModule,
        MatIconModule,
        MatButtonModule,
        FileIconPipe,
        StorageSizePipe,
        TreeCheckobxComponent
    ],
    templateUrl: './file-tree-node.component.html',
    styleUrls: ['./file-tree-node.component.scss']
})
export class FileTreeNodeComponent {
    file = input.required<FileTreeItem>();
    canSelect = input.required<boolean>();
    viewMode = input.required<TreeViewMode>();
    isSearchActive = input.required<boolean>();

    fileClickedHandler = input.required<(node: FileTreeItem) => void>();
    isSelectedChangedHandler = input.required<(node: TreeItem, isSelected: boolean) => void>();
    isExcludedChangedHandler = input.required<(node: TreeItem, isExcluded: boolean) => void>();
}