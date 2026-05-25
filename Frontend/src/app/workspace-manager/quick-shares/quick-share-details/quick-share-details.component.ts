import { Component, OnInit, WritableSignal, computed, signal, viewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Clipboard, ClipboardModule } from '@angular/cdk/clipboard';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ToastrService } from 'ngx-toastr';
import { ActionButtonComponent } from '../../../shared/buttons/action-btn/action-btn.component';
import { ConfigCardComponent } from '../../../shared/config-card/config-card.component';
import { EditableTxtComponent } from '../../../shared/editable-txt/editable-txt.component';
import { ConfirmOperationDirective } from '../../../shared/operation-confirm/confirm-operation.directive';
import { GetQuickShareResponse, QuickShareMode, QuickSharesApi } from '../../../services/quick-shares.api';
import { DataStore } from '../../../services/data-store.service';
import { HttpErrorResponse } from '@angular/common/http';
import { getBase62Guid } from '../../../services/guid-base-62';
import { FoldersAndFilesGetApi, mapFileDtosToItems, mapFolderDtosToItems } from '../../../services/folders-and-files.api';
import { AppTreeItem } from '../../../shared/file-tree-view/tree-item';
import { FileTreeSelectionState, FileTreeViewComponent, LoadFolderNodeRequest, areFileTreeSelectionsEqual } from '../../../shared/file-tree-view/file-tree-view.component';

const SLUG_REGEX = /^[a-zA-Z0-9][a-zA-Z0-9-]{1,98}[a-zA-Z0-9]$/;

@Component({
    selector: 'app-quick-share-details',
    imports: [
        FormsModule,
        ClipboardModule,
        MatButtonModule,
        MatCheckboxModule,
        MatFormFieldModule,
        MatInputModule,
        MatRadioModule,
        MatSlideToggleModule,
        MatTooltipModule,
        ActionButtonComponent,
        ConfigCardComponent,
        EditableTxtComponent,
        ConfirmOperationDirective,
        FileTreeViewComponent
    ],
    templateUrl: './quick-share-details.component.html',
    styleUrl: './quick-share-details.component.scss'
})
export class QuickShareDetailsComponent implements OnInit {
    private _workspaceExternalId: string | null = null;
    private _externalId: string | null = null;

    isLoading = signal(true);
    notFound = signal(false);
    quickShare: WritableSignal<GetQuickShareResponse | null> = signal(null);

    areActionsVisible = signal(false);

    isNameEditing = signal(false);
    nameValue = signal('');

    slugValue = signal('');
    private _lastSavedSlug = signal('');
    isSlugValid = computed(() => {
        const s = this.slugValue().trim();
        return s.length >= 3 && s.length <= 100 && SLUG_REGEX.test(s);
    });
    hasPendingSlugChange = computed(() => {
        const current = this.slugValue().trim();
        return current.length > 0 && current !== this._lastSavedSlug();
    });

    mode = signal<QuickShareMode>('browser');
    allowIndividualFileDownload = signal(true);

    hasExpiration = signal(false);
    expiresAtIso = signal('');
    private _lastSavedExpiresAtIso = '';

    hasPassword = signal(false);
    passwordValue = signal('');
    private _lastSavedHasPassword = signal(false);

    hasPendingPasswordChange = computed(() => {
        // Toggle-off auto-saves (handled in onHasPasswordChanged), so pending only when the
        // toggle is ON and the user has typed a new password — covers both initial set and
        // change-existing.
        return this.hasPassword() && this.passwordValue().trim().length > 0;
    });

    passwordChangeConfirmSubtitle = computed(() =>
        this._lastSavedHasPassword()
            ? 'Anyone using the current password will lose access until they receive the new one.'
            : 'The link will become password-protected. Share the password with anyone who should be able to open it.');

    hasMaxDownloads = signal(false);
    maxDownloadsValue: WritableSignal<number | null> = signal(null);
    private _lastSavedMaxDownloads: number | null = null;

    copied = signal(false);

    treeItems: WritableSignal<AppTreeItem[]> = signal([]);
    savedItemsSelection = signal<FileTreeSelectionState>({
        selectedFolderExternalIds: [],
        selectedFileExternalIds: [],
        excludedFolderExternalIds: [],
        excludedFileExternalIds: []
    });
    autoExpandFolderIds = signal<string[]>([]);
    initiallyExcludedExternalIds = signal<string[]>([]);

    fileTreeView = viewChild(FileTreeViewComponent);

    // Counter of in-flight workspace-tree fetches (initial + lazy loads triggered by auto-expand).
    // While >0, the tree's selectionState is still converging toward saved — suppress
    // change-detection so confirm/discard buttons don't flicker during initialization.
    private _pendingTreeLoads = signal(0);

    hasPendingItemsChange = computed(() => {
        if (this._pendingTreeLoads() > 0) return false;

        const tree = this.fileTreeView();
        if (!tree) return false;

        return !areFileTreeSelectionsEqual(tree.selectionState(), this.savedItemsSelection());
    });

    hasAnyItemsSelected = computed(() => {
        const tree = this.fileTreeView();
        const state = tree ? tree.selectionState() : this.savedItemsSelection();
        return state.selectedFolderExternalIds.length > 0 || state.selectedFileExternalIds.length > 0;
    });

    url = computed(() => this.quickShare()?.url ?? null);
    downloadsCount = computed(() => this.quickShare()?.downloadsCount ?? 0);
    createdAt = computed(() => {
        const created = this.quickShare()?.createdAt;
        return created ? new Date(created) : null;
    });
    lastAccessedAt = computed(() => {
        const last = this.quickShare()?.lastAccessedAt;
        return last ? new Date(last) : null;
    });

    constructor(
        private _route: ActivatedRoute,
        private _router: Router,
        private _api: QuickSharesApi,
        private _foldersApi: FoldersAndFilesGetApi,
        private _dataStore: DataStore,
        private _clipboard: Clipboard,
        private _snackBar: MatSnackBar,
        private _toastr: ToastrService
    ) {
    }

    async ngOnInit() {
        this._workspaceExternalId = this._route.parent?.snapshot.params['workspaceExternalId'] || null;
        this._externalId = this._route.snapshot.params['quickShareExternalId'] || null;

        if (!this._workspaceExternalId || !this._externalId) {
            this.notFound.set(true);
            this.isLoading.set(false);
            return;
        }

        await this.load();
    }

    private async load() {
        try {
            this.isLoading.set(true);
            const response = await this._api.getQuickShare(this._workspaceExternalId!, this._externalId!);
            this.applyResponse(response);
        } catch (error) {
            console.error(error);
            this.notFound.set(true);
        } finally {
            this.isLoading.set(false);
        }
    }

    private applyResponse(response: GetQuickShareResponse) {
        this.quickShare.set(response);
        this.nameValue.set(response.name);
        this.slugValue.set(response.slug);
        this._lastSavedSlug.set(response.slug);

        this.mode.set(response.mode);
        this.allowIndividualFileDownload.set(response.allowIndividualFileDownload);

        this.hasExpiration.set(response.expiresAt !== null);
        const iso = response.expiresAt ? this.toLocalInputValue(new Date(response.expiresAt)) : '';
        this.expiresAtIso.set(iso);
        this._lastSavedExpiresAtIso = iso;

        this.hasPassword.set(response.hasPassword);
        this._lastSavedHasPassword.set(response.hasPassword);
        this.passwordValue.set('');

        this.hasMaxDownloads.set(response.maxDownloads !== null);
        this.maxDownloadsValue.set(response.maxDownloads);
        this._lastSavedMaxDownloads = response.maxDownloads;

        this.savedItemsSelection.set({
            selectedFolderExternalIds: [...response.items.selectedFolders],
            selectedFileExternalIds: [...response.items.selectedFiles],
            excludedFolderExternalIds: [...response.items.excludedFolders],
            excludedFileExternalIds: [...response.items.excludedFiles]
        });

        // Flatten the BE-supplied folder paths into a unique set the tree consumes. Each path
        // is root→deepest; the tree's auto-expand uses a Set lookup, so duplicates across paths
        // (shared ancestors) collapse naturally.
        const expandIds = new Set<string>();
        for (const path of response.items.foldersToExpand) {
            for (const id of path.folderExternalIds) {
                expandIds.add(id);
            }
        }
        this.autoExpandFolderIds.set([...expandIds]);

        this.initiallyExcludedExternalIds.set([
            ...response.items.excludedFolders,
            ...response.items.excludedFiles
        ]);

        this.loadTopFolders();
    }

    private async loadTopFolders() {
        this._pendingTreeLoads.update(n => n + 1);
        try {
            const response = await this._foldersApi.getTopFolders(this._workspaceExternalId!);
            const folders = mapFolderDtosToItems(response.subfolders, []);
            const files = mapFileDtosToItems(response.files, response.folder?.externalId ?? null);
            this.applySelectedFlagFromSaved(folders, files);
            this.treeItems.set([...folders, ...files]);
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to load workspace tree');
        } finally {
            this._pendingTreeLoads.update(n => n - 1);
        }
    }

    async onFolderTreeLoadRequested(request: LoadFolderNodeRequest) {
        this._pendingTreeLoads.update(n => n + 1);
        try {
            const response = await this._foldersApi.getFolder(
                this._workspaceExternalId!,
                request.folder.externalId);
            const ancestors = response.folder
                ? [...(response.folder.ancestors ?? []), { externalId: response.folder.externalId, name: response.folder.name }]
                : [];
            const folders = mapFolderDtosToItems(response.subfolders, ancestors);
            const files = mapFileDtosToItems(response.files, response.folder?.externalId ?? null);
            this.applySelectedFlagFromSaved(folders, files);
            request.folderLoadedCallback([...folders, ...files]);
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to load folder content');
        } finally {
            this._pendingTreeLoads.update(n => n - 1);
        }
    }

    // Items come from the workspace API with isSelected=false. Flip the bit to true on the ones
    // the share already includes BEFORE handing them to the tree, so the tree wrappers reflect
    // the saved selection out of the box — no second-pass mutation required.
    private applySelectedFlagFromSaved(
        folders: { externalId: string; isSelected: WritableSignal<boolean> }[],
        files: { externalId: string; isSelected: WritableSignal<boolean> }[]) {
        const saved = this.savedItemsSelection();
        const selectedFolderIds = new Set(saved.selectedFolderExternalIds);
        const selectedFileIds = new Set(saved.selectedFileExternalIds);

        for (const f of folders) {
            if (selectedFolderIds.has(f.externalId)) f.isSelected.set(true);
        }
        for (const f of files) {
            if (selectedFileIds.has(f.externalId)) f.isSelected.set(true);
        }
    }

    revertItems() {
        this.treeItems.set([]);
        this.loadTopFolders();
    }

    async saveItems() {
        const tree = this.fileTreeView();
        if (!tree) return;

        const current = tree.selectionState();

        if (current.selectedFolderExternalIds.length === 0 && current.selectedFileExternalIds.length === 0) {
            this._toastr.error('Select at least one file or folder');
            return;
        }

        try {
            await this._api.updateQuickShareItems(
                this._workspaceExternalId!,
                this._externalId!,
                {
                    selectedFiles: current.selectedFileExternalIds,
                    selectedFolders: current.selectedFolderExternalIds,
                    excludedFiles: current.excludedFileExternalIds,
                    excludedFolders: current.excludedFolderExternalIds
                });

            this.savedItemsSelection.set({
                selectedFolderExternalIds: [...current.selectedFolderExternalIds],
                selectedFileExternalIds: [...current.selectedFileExternalIds],
                excludedFolderExternalIds: [...current.excludedFolderExternalIds],
                excludedFileExternalIds: [...current.excludedFileExternalIds]
            });

            const qs = this.quickShare();
            if (qs) this.quickShare.set({
                ...qs,
                items: {
                    ...qs.items,
                    selectedFiles: [...current.selectedFileExternalIds],
                    selectedFolders: [...current.selectedFolderExternalIds],
                    excludedFiles: [...current.excludedFileExternalIds],
                    excludedFolders: [...current.excludedFolderExternalIds]
                }
            });

            this._dataStore.invalidateQuickShares(this._workspaceExternalId!);
            this._toastr.success('Shared items updated');
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to update shared items');
        }
    }

    private toLocalInputValue(date: Date): string {
        const offset = date.getTimezoneOffset();
        const localMs = date.getTime() - offset * 60_000;
        return new Date(localMs).toISOString().slice(0, 16);
    }

    goBack() {
        this._router.navigate([`/workspaces/${this._workspaceExternalId}/quick-shares`]);
    }

    previewShare() {
        const url = this.url();
        if (!url) return;

        const parsed = new URL(url);
        this._router.navigateByUrl(parsed.pathname + parsed.search);
    }

    editName() {
        this.isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }

    openPicker(input: HTMLInputElement) {
        input.showPicker?.();
    }

    toggleActions() {
        this.areActionsVisible.update(v => !v);
    }

    copyLink() {
        const link = this.url();
        if (!link) return;

        if (this._clipboard.copy(link)) {
            this.copied.set(true);
            this._snackBar.open('Link copied to clipboard', 'Close', { duration: 2000 });
            setTimeout(() => this.copied.set(false), 2000);
        }
    }

    regenerateSlug() {
        this.slugValue.set(getBase62Guid());
        this.saveSlugChange();
    }

    revertSlug() {
        this.slugValue.set(this._lastSavedSlug());
    }

    async saveSlugChange() {
        const slug = this.slugValue().trim();
        if (!slug || slug === this._lastSavedSlug()) return;
        if (!this.isSlugValid()) {
            this._toastr.error('Slug format invalid');
            return;
        }

        try {
            await this._api.updateQuickShareSlug(
                this._workspaceExternalId!,
                this._externalId!,
                { slug });

            this._lastSavedSlug.set(slug);
            this.slugValue.set(slug);

            const current = this.quickShare();
            if (current) {
                const newUrl = current.url ? current.url.replace(/\/share\/[^?]*/, `/share/${slug}`) : null;
                this.quickShare.set({ ...current, slug, url: newUrl });
            }

            this._dataStore.invalidateQuickShares(this._workspaceExternalId!);
            this._toastr.success('Custom URL updated');
        } catch (error) {
            this.slugValue.set(this._lastSavedSlug());

            if (error instanceof HttpErrorResponse && error.status === 409) {
                this._toastr.error('This URL is already taken');
            } else {
                console.error(error);
                this._toastr.error('Failed to update URL');
            }
        }
    }

    async saveName(newName: string) {
        const trimmed = newName.trim();
        if (!trimmed) return;

        try {
            await this._api.updateQuickShareName(
                this._workspaceExternalId!,
                this._externalId!,
                { name: trimmed });

            this.nameValue.set(trimmed);
            const current = this.quickShare();
            if (current) this.quickShare.set({ ...current, name: trimmed });
            this._dataStore.invalidateQuickShares(this._workspaceExternalId!);
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to save name');
        }
    }

    onModeChanged(value: QuickShareMode) {
        this.mode.set(value);
        if (value !== 'browser') {
            this.allowIndividualFileDownload.set(false);
        }
        this.saveMode();
    }

    onAllowIndividualFileDownloadChanged(checked: boolean) {
        this.allowIndividualFileDownload.set(checked);
        this.saveMode();
    }

    private async saveMode() {
        try {
            await this._api.updateQuickShareMode(
                this._workspaceExternalId!,
                this._externalId!,
                {
                    mode: this.mode(),
                    allowIndividualFileDownload: this.allowIndividualFileDownload()
                });

            const current = this.quickShare();
            if (current) this.quickShare.set({
                ...current,
                mode: this.mode(),
                allowIndividualFileDownload: this.allowIndividualFileDownload()
            });
            this._dataStore.invalidateQuickShares(this._workspaceExternalId!);
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to save mode');
        }
    }

    onHasExpirationChanged(checked: boolean) {
        this.hasExpiration.set(checked);

        if (!checked) {
            this.expiresAtIso.set('');
            this.saveExpiration(null);
        }
    }

    onExpirationBlur() {
        if (!this.hasExpiration()) return;

        const iso = this.expiresAtIso();
        if (!iso || iso === this._lastSavedExpiresAtIso) return;

        const isoUtc = new Date(iso).toISOString();
        this.saveExpiration(isoUtc);
    }

    private async saveExpiration(expiresAt: string | null) {
        try {
            await this._api.updateQuickShareExpiration(
                this._workspaceExternalId!,
                this._externalId!,
                { expiresAt });

            this._lastSavedExpiresAtIso = expiresAt ? this.expiresAtIso() : '';
            const current = this.quickShare();
            if (current) this.quickShare.set({ ...current, expiresAt });
            this._dataStore.invalidateQuickShares(this._workspaceExternalId!);
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to save expiration');
        }
    }

    onHasPasswordChanged(checked: boolean) {
        this.hasPassword.set(checked);
        this.passwordValue.set('');

        // Toggling OFF removes the password — apply immediately, no confirm step. Setting or
        // changing a password requires typing one + confirm, so toggling ON is just UI state.
        if (!checked && this._lastSavedHasPassword()) {
            this.applyPassword(null);
        }
    }

    revertPassword() {
        this.hasPassword.set(this._lastSavedHasPassword());
        this.passwordValue.set('');
    }

    async savePasswordChange() {
        const password = this.passwordValue().trim();

        if (!password) {
            this._toastr.error('Type a password');
            return;
        }

        await this.applyPassword(password);
    }

    private async applyPassword(password: string | null) {
        try {
            await this._api.updateQuickSharePassword(
                this._workspaceExternalId!,
                this._externalId!,
                { password });

            this._lastSavedHasPassword.set(password !== null);
            this.passwordValue.set('');

            const current = this.quickShare();
            if (current) this.quickShare.set({ ...current, hasPassword: password !== null });
            this._dataStore.invalidateQuickShares(this._workspaceExternalId!);
            this._toastr.success(password !== null ? 'Password updated' : 'Password removed');
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to save password');
        }
    }

    onHasMaxDownloadsChanged(checked: boolean) {
        this.hasMaxDownloads.set(checked);

        if (!checked) {
            this.maxDownloadsValue.set(null);
            this.saveMaxDownloads(null);
        }
    }

    onMaxDownloadsBlur() {
        if (!this.hasMaxDownloads()) return;

        const value = this.maxDownloadsValue();
        if (value === null || value <= 0) return;
        if (value === this._lastSavedMaxDownloads) return;

        this.saveMaxDownloads(value);
    }

    private async saveMaxDownloads(maxDownloads: number | null) {
        try {
            await this._api.updateQuickShareMaxDownloads(
                this._workspaceExternalId!,
                this._externalId!,
                { maxDownloads });

            this._lastSavedMaxDownloads = maxDownloads;
            const current = this.quickShare();
            if (current) this.quickShare.set({ ...current, maxDownloads });
            this._dataStore.invalidateQuickShares(this._workspaceExternalId!);
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to save max downloads');
        }
    }

    async deleteShare() {
        try {
            await this._api.deleteQuickShare(
                this._workspaceExternalId!,
                this._externalId!);

            this._dataStore.invalidateQuickShares(this._workspaceExternalId!);
            this.goBack();
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to delete');
        }
    }
}
