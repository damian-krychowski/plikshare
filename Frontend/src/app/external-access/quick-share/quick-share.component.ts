import { Component, OnInit, WritableSignal, computed, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ToastrService } from 'ngx-toastr';
import { HttpErrorResponse } from '@angular/common/http';
import { GetQuickShareContentResponse, GetQuickShareInfoResponse, QuickShareContentFile, QuickShareExternalAccessApi } from '../../services/quick-share-external-access.api';
import { ZipFileNode, ZipFileTreeViewComponent, ZipFolderNode, ZipTreeNode } from '../../shared/zip-file-tree-view/zip-file-tree-view.component';
import { StorageSizePipe } from '../../shared/storage-size.pipe';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { ActionTextButtonComponent } from '../../shared/buttons/action-text-btn/action-text-btn.component';

@Component({
    selector: 'app-quick-share',
    imports: [
        DatePipe,
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ZipFileTreeViewComponent,
        StorageSizePipe,
        ActionButtonComponent,
        ActionTextButtonComponent
    ],
    templateUrl: './quick-share.component.html',
    styleUrl: './quick-share.component.scss'
})
export class QuickShareComponent implements OnInit {
    slug: string | null = null;
    token: string | null = null;

    isLoading = signal(true);
    notFound = signal(false);
    info: WritableSignal<GetQuickShareInfoResponse | null> = signal(null);
    content: WritableSignal<GetQuickShareContentResponse | null> = signal(null);
    fileTree: WritableSignal<ZipTreeNode[]> = signal([]);

    passwordInput = signal('');
    isUnlocking = signal(false);
    isDownloading = signal(false);

    name = computed(() => this.info()?.name ?? '');
    mode = computed(() => this.info()?.mode ?? 'browser');
    requiresPassword = computed(() => this.info()?.requiresPassword ?? false);
    isUnlocked = computed(() => this.info()?.isUnlocked ?? false);
    isExpired = computed(() => this.info()?.isExpired ?? false);
    isExhausted = computed(() => this.info()?.isExhausted ?? false);
    isOwnerPreview = computed(() => this.info()?.isOwnerPreview ?? false);
    allowIndividualFileDownload = computed(() => this.info()?.allowIndividualFileDownload ?? false);
    totalSizeInBytes = computed(() => this.content()?.totalSizeInBytes ?? 0);
    filesCount = computed(() => this.content()?.files.length ?? 0);

    isReady = computed(() => {
        const info = this.info();
        if (!info) return false;
        if (info.isExpired || info.isExhausted) return false;
        if (info.requiresPassword && !info.isUnlocked) return false;
        return true;
    });

    constructor(
        private _route: ActivatedRoute,
        private _api: QuickShareExternalAccessApi,
        private _toastr: ToastrService
    ) {
    }

    async ngOnInit() {
        this.slug = this._route.snapshot.params['slug'] || null;
        this.token = this._route.snapshot.queryParamMap.get('token');

        if (!this.slug) {
            this.notFound.set(true);
            this.isLoading.set(false);
            return;
        }

        await this.loadInfo();

        if (this.isReady() && this.mode() === 'direct') {
            await this.downloadAll();
        } else if (this.isReady()) {
            await this.loadContent();
        }
    }

    private async loadInfo() {
        try {
            this.isLoading.set(true);
            const info = await this._api.getInfo(this.slug!, this.token);
            this.info.set(info);
        } catch (error) {
            if (error instanceof HttpErrorResponse && error.status === 404) {
                this.notFound.set(true);
            } else {
                console.error(error);
                this._toastr.error('Failed to load quick share');
            }
        } finally {
            this.isLoading.set(false);
        }
    }

    private async loadContent() {
        try {
            const content = await this._api.getContent(this.slug!, this.token);
            this.content.set(content);
            this.fileTree.set(this.buildTree(content.files));
        } catch (error) {
            console.error(error);
            this._toastr.error('Failed to load quick share content');
        }
    }

    async unlock() {
        if (this.isUnlocking() || !this.passwordInput().trim()) return;

        try {
            this.isUnlocking.set(true);
            await this._api.unlock(this.slug!, this.token, { password: this.passwordInput() });
            this.passwordInput.set('');

            await this.loadInfo();

            if (this.isReady() && this.mode() === 'direct') {
                await this.downloadAll();
            } else if (this.isReady()) {
                await this.loadContent();
            }
        } catch (error) {
            if (error instanceof HttpErrorResponse && error.status === 401) {
                this._toastr.error('Wrong password');
            } else {
                console.error(error);
                this._toastr.error('Unlock failed');
            }
        } finally {
            this.isUnlocking.set(false);
        }
    }

    async downloadAll() {
        if (this.isDownloading()) return;

        try {
            this.isDownloading.set(true);
            const result = await this._api.getBulkDownloadLink(this.slug!, this.token);

            const link = document.createElement('a');
            link.href = result.preSignedUrl;
            link.click();
            link.remove();

            await this.loadInfo();
        } catch (error) {
            if (error instanceof HttpErrorResponse && error.status === 410) {
                this._toastr.error('Download limit reached');
                await this.loadInfo();
            } else {
                console.error(error);
                this._toastr.error('Download failed');
            }
        } finally {
            this.isDownloading.set(false);
        }
    }

    async onFileDownloadClicked(file: ZipFileNode) {
        if (!this.allowIndividualFileDownload()) return;

        try {
            const result = await this._api.getFileDownloadLink(
                this.slug!, this.token,
                file.id,
                'attachment');

            const link = document.createElement('a');
            link.href = result.downloadPreSignedUrl;
            link.click();
            link.remove();

            await this.loadInfo();
        } catch (error) {
            if (error instanceof HttpErrorResponse && error.status === 410) {
                this._toastr.error('Download limit reached');
                await this.loadInfo();
            } else {
                console.error(error);
                this._toastr.error('Download failed');
            }
        }
    }

    private buildTree(files: QuickShareContentFile[]): ZipTreeNode[] {
        const root: ZipTreeNode[] = [];
        const folderMap = new Map<string, ZipFolderNode>();

        for (const file of files) {
            const segments = file.filePath.split('/').filter(s => s.length > 0);
            const fileSegments = segments.slice(0, -1);

            let currentChildren = root;
            let pathSoFar = '';

            for (const segment of fileSegments) {
                pathSoFar = pathSoFar ? `${pathSoFar}/${segment}` : segment;

                let folder = folderMap.get(pathSoFar);
                if (!folder) {
                    folder = {
                        type: 'folder',
                        id: pathSoFar,
                        name: segment,
                        children: [],
                        isExpanded: signal(true),
                        isVisible: signal(true),
                        wasRendered: signal(true),
                        wasLoaded: true
                    };
                    folderMap.set(pathSoFar, folder);
                    currentChildren.push(folder);
                }
                currentChildren = folder.children;
            }

            const fileNode: ZipFileNode = {
                type: 'file',
                id: file.externalId,
                extension: file.extension,
                fullName: file.name + file.extension,
                fullNameLower: (file.name + file.extension).toLowerCase(),
                sizeInBytes: file.sizeInBytes,
                isVisible: signal(true)
            };
            currentChildren.push(fileNode);
        }

        return root;
    }
}
