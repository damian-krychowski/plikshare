import { Component, ElementRef, HostListener, OnInit, computed, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { TrashApi, TrashItemDto } from '../../services/trash.api';
import { GenericDialogService } from '../../shared/generic-message-dialog/generic-dialog-service';
import { ActionTextButtonComponent } from '../../shared/buttons/action-text-btn/action-text-btn.component';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { DataStore } from '../../services/data-store.service';
import { WorkspaceContextService } from '../workspace-context.service';
import { AuthService } from '../../services/auth.service';
import {
    ZipFileTreeViewComponent,
    ZipFileNode,
    ZipFolderNode,
    ZipTreeNode,
    countSelectedDescendants
} from '../../shared/zip-file-tree-view/zip-file-tree-view.component';
import { TrashFileItemComponent } from './trash-file-item/trash-file-item.component';
import {
    RestoreFromTrashDialogComponent,
    RestoreFromTrashDialogData,
    RestoreFromTrashDialogResult
} from './restore-from-trash-dialog/restore-from-trash-dialog.component';

// 'flat' — the grouped-by-folder list of trash-file-items. 'tree' — the nested tree view
// with select/exclude checkboxes (the zip-file-tree-view component).
type TrashViewMode = 'flat' | 'tree';

// Trash items sharing the same original folder. Rendered as a folder header + file list
// when it holds more than one file; a lone file is rendered flat, without a header.
type TrashGroup = {
    key: string;
    pathLabel: string;
    items: TrashItemDto[];
};

@Component({
    selector: 'app-trash',
    standalone: true,
    imports: [
        FormsModule,
        MatCheckboxModule,
        MatTooltipModule,
        ActionTextButtonComponent,
        ActionButtonComponent,
        ZipFileTreeViewComponent,
        TrashFileItemComponent
    ],
    templateUrl: './trash.component.html',
    styleUrl: './trash.component.scss'
})
export class TrashComponent implements OnInit {
    isLoading = signal(false);
    items = signal<TrashItemDto[]>([]);
    totalSizeInBytes = signal(0);

    isEmpty = computed(() => this.items().length === 0);

    viewMode = signal<TrashViewMode>('flat');
    viewMenuOpen = signal(false);

    // Items grouped by their original folder. Group order follows first appearance; item
    // order within a group is preserved from the source list.
    flatGroups = computed<TrashGroup[]>(() => {
        const map = new Map<string, TrashGroup>();

        for (const item of this.items()) {
            const key = JSON.stringify(item.originalFolderPath ?? []);
            let group = map.get(key);

            if (!group) {
                group = { key, pathLabel: this.formatPath(item), items: [] };
                map.set(key, group);
            }

            group.items.push(item);
        }

        return [...map.values()];
    });

    // All flat-view items in render order (groups flattened) — the basis for shift-range
    // selection, which must follow what the user actually sees.
    private orderedItems = computed<TrashItemDto[]>(() =>
        this.flatGroups().flatMap(g => g.items));

    // 'tree' view — the zip-file-tree-view input, rebuilt on load and on every mode switch.
    treeNodes = signal<ZipTreeNode[]>([]);

    // 'flat' view selection — a set of trash externalIds toggled by the per-row checkboxes.
    private _flatSelectedIds = signal<Set<string>>(new Set());

    // The externalIds currently selected, resolved from whichever view is active.
    selectedExternalIds = computed<string[]>(() => {
        if (this.viewMode() === 'tree') {
            const ids: string[] = [];
            this.collectTreeSelectedIds(this.treeNodes(), ids);
            return ids;
        }

        return [...this._flatSelectedIds()];
    });

    selectedCount = computed(() => this.selectedExternalIds().length);
    hasSelection = computed(() => this.selectedCount() > 0);

    // Drives the select-all checkbox — true only when every item is selected.
    areAllSelected = computed(() =>
        this.items().length > 0 && this.selectedCount() === this.items().length);

    // Anchor for shift-range selection in the flat view.
    private _selectionAnchorId: string | null = null;

    private _workspaceExternalId: string | null = null;

    // Delete-forever and empty-trash are owner/admin-only on the backend; the UI hides them
    // for everyone else. Restore stays available to all workspace members.
    isOwnerOrAdmin = computed(() => {
        const workspace = this._workspaceContext.workspace();

        if (!workspace)
            return false;

        return workspace.owner.externalId === this._auth.userExternalId()
            || this._auth.isAdmin();
    });

    constructor(
        private _trashApi: TrashApi,
        private _activatedRoute: ActivatedRoute,
        private _genericDialog: GenericDialogService,
        private _toastr: ToastrService,
        private _dataStore: DataStore,
        private _dialog: MatDialog,
        private _workspaceContext: WorkspaceContextService,
        private _auth: AuthService,
        private _el: ElementRef<HTMLElement>)
    {
    }

    async ngOnInit() {
        this._workspaceExternalId = this._activatedRoute.parent?.snapshot.params['workspaceExternalId'] ?? null;
        await this.load();
    }

    private async load() {
        if (!this._workspaceExternalId)
            return;

        try {
            this.isLoading.set(true);

            // Via the data store so a prefetch (trash icon hover) is consumed when present;
            // a reload after a mutation re-fetches fresh.
            const response = await this._dataStore.getTrashItems(this._workspaceExternalId);

            this.items.set(response.items);
            this.totalSizeInBytes.set(response.totalSizeInBytes);
            this.treeNodes.set(this.buildTree(response.items));
            this._flatSelectedIds.set(new Set());
            this._selectionAnchorId = null;
        } catch (error) {
            console.error('Failed to load the trash', error);
            this._toastr.error('Could not load the trash.');
        } finally {
            this.isLoading.set(false);
        }
    }

    // --- view mode ---------------------------------------------------------------------

    toggleViewMenu() {
        this.viewMenuOpen.update(open => !open);
    }

    setViewMode(mode: TrashViewMode) {
        this.viewMenuOpen.set(false);

        if (mode === this.viewMode())
            return;

        this.viewMode.set(mode);
        this.clearSelection();
    }

    @HostListener('document:click', ['$event'])
    onDocumentClick(event: MouseEvent) {
        if (!this.viewMenuOpen())
            return;

        if (!this._el.nativeElement.contains(event.target as Node))
            this.viewMenuOpen.set(false);
    }

    private clearSelection() {
        this._flatSelectedIds.set(new Set());
        this._selectionAnchorId = null;
        this.treeNodes.set(this.buildTree(this.items()));
    }

    // --- flat-view selection -----------------------------------------------------------

    isSelected(item: TrashItemDto): boolean {
        return this._flatSelectedIds().has(item.externalId);
    }

    onSelectionChange(item: TrashItemDto, selected: boolean) {
        this._flatSelectedIds.update(set => {
            const next = new Set(set);

            if (selected)
                next.add(item.externalId);
            else
                next.delete(item.externalId);

            return next;
        });

        // Anchor for shift-range selection: the just-selected item, or — when deselecting —
        // the first item still selected in render order.
        if (selected) {
            this._selectionAnchorId = item.externalId;
        } else {
            const selectedIds = this._flatSelectedIds();
            this._selectionAnchorId =
                this.orderedItems().find(i => selectedIds.has(i.externalId))?.externalId ?? null;
        }
    }

    // Shift-click selects the contiguous range between the anchor and the clicked item,
    // replacing the current selection — mirrors the files-explorer behaviour.
    onShiftSelected(item: TrashItemDto) {
        const ordered = this.orderedItems();

        if (!this._selectionAnchorId) {
            this.onSelectionChange(item, !this.isSelected(item));
            return;
        }

        const anchorIdx = ordered.findIndex(i => i.externalId === this._selectionAnchorId);
        const targetIdx = ordered.findIndex(i => i.externalId === item.externalId);

        if (anchorIdx === -1 || targetIdx === -1)
            return;

        const from = Math.min(anchorIdx, targetIdx);
        const to = Math.max(anchorIdx, targetIdx);

        const next = new Set<string>();
        for (let i = from; i <= to; i++)
            next.add(ordered[i].externalId);

        this._flatSelectedIds.set(next);
    }

    // Select-all checkbox — selects everything when not all selected, clears otherwise.
    // Works against whichever view is active.
    toggleSelectAll() {
        const selectAll = !this.areAllSelected();

        if (this.viewMode() === 'tree') {
            this.setTreeSelection(this.treeNodes(), selectAll);
        } else {
            this._flatSelectedIds.set(selectAll
                ? new Set(this.items().map(i => i.externalId))
                : new Set());
        }
    }

    private setTreeSelection(nodes: ZipTreeNode[], selected: boolean) {
        for (const node of nodes) {
            node.isExcluded.set(false);

            if (node.type === 'file') {
                node.isSelected.set(selected);
            } else {
                // Folders stay unselected — selection is driven at the file level so the
                // count stays exact regardless of folder nesting.
                node.isSelected.set(false);
                this.setTreeSelection(node.children, selected);
            }
        }
    }

    // --- tree building -----------------------------------------------------------------

    private buildTree(items: TrashItemDto[]): ZipTreeNode[] {
        const roots: ZipTreeNode[] = [];
        const folderByPath = new Map<string, ZipFolderNode>();

        // Resolves (creating on the way) the folder chain for a path, returning its leaf.
        const resolveFolder = (path: string[]): ZipFolderNode | null => {
            if (path.length === 0)
                return null;

            const key = JSON.stringify(path);
            const existing = folderByPath.get(key);
            if (existing)
                return existing;

            const parent = resolveFolder(path.slice(0, -1));
            const folder = this.makeFolderNode(path[path.length - 1], key, parent);

            folderByPath.set(key, folder);
            (parent ? parent.children : roots).push(folder);
            return folder;
        };

        for (const item of items) {
            const parent = resolveFolder(item.originalFolderPath ?? []);
            const fileNode = this.makeFileNode(item, parent);
            (parent ? parent.children : roots).push(fileNode);
        }

        return roots;
    }

    private makeFolderNode(name: string, id: string, parent: ZipFolderNode | null): ZipFolderNode {
        const children: ZipTreeNode[] = [];

        return {
            type: 'folder',
            id,
            name,
            nameLower: name.toLowerCase(),
            children,
            isExpanded: signal(true),
            wasRendered: signal(true),
            isVisible: signal(true),
            wasLoaded: true,
            isSelected: signal(false),
            isExcluded: signal(false),
            parent,
            isParentSelected: computed(() => parent ? (parent.isSelected() || parent.isParentSelected()) : false),
            isParentExcluded: computed(() => parent ? (parent.isExcluded() || parent.isParentExcluded()) : false),
            selectedDescendantsCount: computed(() => countSelectedDescendants(children))
        };
    }

    private makeFileNode(item: TrashItemDto, parent: ZipFolderNode | null): ZipFileNode {
        const fullName = `${item.name}${item.extension}`;

        return {
            type: 'file',
            id: item.externalId,
            extension: item.extension,
            fullName,
            fullNameLower: fullName.toLowerCase(),
            sizeInBytes: item.sizeInBytes,
            isVisible: signal(true),
            isSelected: signal(false),
            isExcluded: signal(false),
            parent,
            isParentSelected: computed(() => parent ? (parent.isSelected() || parent.isParentSelected()) : false),
            isParentExcluded: computed(() => parent ? (parent.isExcluded() || parent.isParentExcluded()) : false)
        };
    }

    // A file is effectively selected when it (or an ancestor folder) is selected and neither
    // it nor an ancestor is excluded — mirrors the tree component's own visual rule.
    private collectTreeSelectedIds(nodes: ZipTreeNode[], out: string[]) {
        for (const node of nodes) {
            if (node.type === 'file') {
                const selected = (node.isSelected() || node.isParentSelected())
                    && !node.isExcluded()
                    && !node.isParentExcluded();

                if (selected)
                    out.push(node.id);
            } else {
                this.collectTreeSelectedIds(node.children, out);
            }
        }
    }

    // --- formatting --------------------------------------------------------------------

    formatPath(item: TrashItemDto): string {
        if (!item.originalFolderPath || item.originalFolderPath.length === 0)
            return 'Workspace root';

        return item.originalFolderPath.join('  /  ');
    }

    formatSize(bytes: number): string {
        if (bytes < 1024)
            return `${bytes} B`;

        const units = ['KB', 'MB', 'GB', 'TB'];
        let value = bytes / 1024;
        let unitIndex = 0;

        while (value >= 1024 && unitIndex < units.length - 1) {
            value /= 1024;
            unitIndex++;
        }

        return `${value.toFixed(value >= 100 ? 0 : 1)} ${units[unitIndex]}`;
    }

    // --- bulk operations ---------------------------------------------------------------

    async restoreSelected() {
        const ids = this.selectedExternalIds();

        if (!this._workspaceExternalId || ids.length === 0)
            return;

        // The dialog lets the user pick the destination: original location or a chosen folder.
        const choice = await firstValueFrom(this._dialog
            .open<RestoreFromTrashDialogComponent, RestoreFromTrashDialogData, RestoreFromTrashDialogResult>(
                RestoreFromTrashDialogComponent, {
                    width: '700px',
                    maxHeight: '600px',
                    position: { top: '100px' },
                    data: { count: ids.length, workspaceExternalId: this._workspaceExternalId }
                })
            .afterClosed());

        if (!choice)
            return;

        try {
            this.isLoading.set(true);

            const response = await this._trashApi.restore(this._workspaceExternalId, {
                items: ids.map(id => ({
                    fileExternalId: id,
                    mode: choice.mode,
                    targetFolderExternalId: choice.targetFolderExternalId
                }))
            });

            const restored = response.results.filter(r => r?.status === 'restored').length;
            const failed = ids.length - restored;

            if (restored > 0)
                this._toastr.success(`${restored} item(s) restored.`);

            if (failed > 0)
                this._toastr.error(`${failed} item(s) could not be restored.`);

            await this.load();
        } catch (error) {
            console.error('Failed to restore from trash', error);
            this._toastr.error('Restore failed.');
        } finally {
            this.isLoading.set(false);
        }
    }

    async deleteSelectedForever() {
        const ids = this.selectedExternalIds();

        if (!this._workspaceExternalId || ids.length === 0)
            return;

        const confirmed = await firstValueFrom(this._genericDialog.openGenericMessageDialog({
            title: 'Delete forever',
            message: `${ids.length} item(s) will be permanently deleted. This action cannot be undone.`,
            confirmButtonText: 'Delete forever',
            showCancelButton: true,
            isDanger: true
        }));

        if (!confirmed)
            return;

        try {
            this.isLoading.set(true);

            await this._trashApi.deleteForever(this._workspaceExternalId, {
                fileExternalIds: ids
            });

            this._toastr.success(`${ids.length} item(s) permanently deleted.`);
            this._dataStore.clearWorkspaceDetails(this._workspaceExternalId);
            await this.load();
        } catch (error) {
            console.error('Failed to delete items forever', error);
            this._toastr.error('Could not delete the selected items.');
        } finally {
            this.isLoading.set(false);
        }
    }

    async emptyTrash() {
        if (!this._workspaceExternalId || this.isEmpty())
            return;

        const count = this.items().length;

        const confirmed = await firstValueFrom(this._genericDialog.openGenericMessageDialog({
            title: 'Empty trash',
            message: `All ${count} item(s) in the trash will be permanently deleted. This action cannot be undone.`,
            confirmButtonText: 'Empty trash',
            showCancelButton: true,
            isDanger: true
        }));

        if (!confirmed)
            return;

        try {
            this.isLoading.set(true);

            const response = await this._trashApi.emptyTrash(this._workspaceExternalId);

            this._toastr.success(`Trash emptied — ${response.deletedCount} item(s) permanently deleted.`);
            this._dataStore.clearWorkspaceDetails(this._workspaceExternalId);
            await this.load();
        } catch (error) {
            console.error('Failed to empty the trash', error);
            this._toastr.error('Could not empty the trash.');
        } finally {
            this.isLoading.set(false);
        }
    }
}
