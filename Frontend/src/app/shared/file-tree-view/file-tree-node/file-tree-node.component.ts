
import { Component, computed, input, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatTreeModule } from "@angular/material/tree";
import { FileIconPipe } from "../../../files-explorer/file-icon-pipe/file-icon.pipe";
import { StorageSizePipe } from "../../storage-size.pipe";
import { TreeCheckobxComponent } from "../tree-checkbox/tree-checkbox.component";
import { FileTreeItem, TreeItem, TreeViewMode } from "../tree-item";
import { MarqueeOnTruncateDirective } from "../../marquee-on-truncate.directive";

@Component({
    selector: 'app-file-tree-node',
    imports: [
    FormsModule,
    MatTreeModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    FileIconPipe,
    StorageSizePipe,
    TreeCheckobxComponent,
    MarqueeOnTruncateDirective
],
    templateUrl: './file-tree-node.component.html',
    styleUrls: ['./file-tree-node.component.scss']
})
export class FileTreeNodeComponent {
    file = input.required<FileTreeItem>();
    canSelect = input.required<boolean>();
    viewMode = input.required<TreeViewMode>();
    isSearchActive = input.required<boolean>();

    // Thumbnail support — mirrors FileItemComponent. The slot swaps icon -> spinner -> <img> in a
    // fixed-size box so the row height never changes (the tree's virtual scroll depends on it).
    showThumbnails = input(false);
    isProcessing = input(false);
    getThumbnailUrl = input<((fileExternalId: string) => string) | undefined>(undefined);

    fileClickedHandler = input.required<(node: FileTreeItem) => void>();
    isSelectedChangedHandler = input.required<(node: TreeItem, isSelected: boolean) => void>();
    isExcludedChangedHandler = input.required<(node: TreeItem, isExcluded: boolean) => void>();
    checkboxMouseDownHandler = input.required<(event: MouseEvent) => void>();

    private _failedThumbnailUrls = signal<ReadonlySet<string>>(new Set<string>());

    miniThumbnailUrl = computed(() => {
        if (!this.showThumbnails())
            return null;

        const item = this.file().item;
        const etag = item.miniThumbnailEtag();

        if (!etag)
            return null;

        const base = this.getThumbnailUrl()?.(item.externalId);

        if (!base)
            return null;

        const separator = base.includes('?') ? '&' : '?';
        const url = `${base}${separator}v=${etag}`;

        return this._failedThumbnailUrls().has(url) ? null : url;
    });

    onThumbnailError(url: string): void {
        this._failedThumbnailUrls.update(set => new Set(set).add(url));
    }
}