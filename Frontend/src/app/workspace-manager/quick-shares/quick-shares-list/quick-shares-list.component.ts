import { Component, OnDestroy, OnInit, WritableSignal, computed, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { DataStore } from '../../../services/data-store.service';
import { AppQuickShare, QuickShareItemComponent } from '../quick-share-item/quick-share-item.component';
import { ItemButtonComponent } from '../../../shared/buttons/item-btn/item-btn.component';

@Component({
    selector: 'app-quick-shares-list',
    imports: [
        QuickShareItemComponent,
        ItemButtonComponent
    ],
    templateUrl: './quick-shares-list.component.html',
    styleUrl: './quick-shares-list.component.scss'
})
export class QuickSharesListComponent implements OnInit, OnDestroy {
    isLoading = signal(false);
    quickShares: WritableSignal<AppQuickShare[]> = signal([]);
    hasNoShares = computed(() => !this.isLoading() && this.quickShares().length === 0);

    private _routerSubscription: Subscription | null = null;
    private _currentWorkspaceExternalId: string | null = null;

    constructor(
        private _router: Router,
        private _activatedRoute: ActivatedRoute,
        private _dataStore: DataStore
    ) {
    }

    async ngOnInit() {
        await this.load();

        this._routerSubscription = this._router.events
            .pipe(filter(e => e instanceof NavigationEnd))
            .subscribe(() => this.load());
    }

    ngOnDestroy(): void {
        this._routerSubscription?.unsubscribe();
    }

    private async load() {
        const workspaceExternalId = this._activatedRoute.parent?.snapshot.params['workspaceExternalId'];

        if (!workspaceExternalId)
            throw new Error('workspaceExternalId is missing');

        if (this._currentWorkspaceExternalId === workspaceExternalId && this.quickShares().length > 0)
            return;

        this._currentWorkspaceExternalId = workspaceExternalId;

        try {
            this.isLoading.set(true);

            const response = await this._dataStore.getQuickShares(workspaceExternalId);

            this.quickShares.set(response.items.map(item => ({
                externalId: item.externalId,
                workspaceExternalId,
                name: signal(item.name),
                createdAt: new Date(item.createdAt),
                expiresAt: signal(item.expiresAt ? new Date(item.expiresAt) : null),
                hasPassword: signal(item.hasPassword),
                maxDownloads: signal(item.maxDownloads),
                downloadsCount: signal(item.downloadsCount),
                mode: signal(item.mode),
                allowIndividualFileDownload: signal(item.allowIndividualFileDownload),
                lastAccessedAt: signal(item.lastAccessedAt ? new Date(item.lastAccessedAt) : null),
                slug: signal(item.slug),
                hasSecret: item.hasSecret,
                url: signal(item.url),
                selectedFilesCount: item.selectedFilesCount,
                selectedFoldersCount: item.selectedFoldersCount,
                excludedFilesCount: item.excludedFilesCount,
                excludedFoldersCount: item.excludedFoldersCount,
                isNameEditing: signal(false)
            })));
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    onShareDeleted(share: AppQuickShare) {
        this.quickShares.update(values => values.filter(s => s.externalId !== share.externalId));
    }

    goToExplorer() {
        if (!this._currentWorkspaceExternalId) return;
        this._router.navigate([`workspaces/${this._currentWorkspaceExternalId}/explorer`]);
    }
}
