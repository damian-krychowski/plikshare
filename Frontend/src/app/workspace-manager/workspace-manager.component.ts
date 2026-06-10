import { Component, OnDestroy, OnInit, ViewEncapsulation, computed, effect, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, Router, RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { WorkspaceContextService } from './workspace-context.service';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PrefetchDirective } from '../shared/prefetch.directive';
import { DataStore } from '../services/data-store.service';
import { SearchInputComponent } from '../shared/search-input/search-input.component';
import { SearchComponent } from '../shared/search/search.component';
import { AuthService } from '../services/auth.service';
import { FileUploadManager } from '../services/file-upload-manager/file-upload-manager';
import {MatBadgeModule} from '@angular/material/badge';
import { SettingsMenuBtnComponent } from '../shared/setting-menu-btn/settings-menu-btn.component';
import { FullEncryptionSessionsBtnComponent } from '../shared/full-encryption-sessions-btn/full-encryption-sessions-btn.component';
import { SignOutService } from '../services/sign-out.service';
import { FooterComponent } from '../static-pages/shared/footer/footer.component';
import { WorkspaceSizeComponent } from '../shared/workspace-size/workspace-size.component';
import { AppCapabilitiesService } from '../services/app-capabilities.service';

@Component({
    selector: 'app-workspace-manager',
    imports: [
        MatButtonModule,
        RouterOutlet,
        MatTooltipModule,
        PrefetchDirective,
        SearchInputComponent,
        SearchComponent,
        MatBadgeModule,
        SettingsMenuBtnComponent,
        FullEncryptionSessionsBtnComponent,
        FooterComponent,
        WorkspaceSizeComponent
    ],
    templateUrl: './workspace-manager.component.html',
    styleUrl: './workspace-manager.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class WorkspaceManagerComponent implements OnInit, OnDestroy  {    
    private _workspaceExternalId: string = '';

    isFirstWorkspaceLoaded = signal(false);
    isMenuOpen = signal(false);
    pendingUploadCount = signal(0);

    anyPendingUploads = computed(() => this.pendingUploadCount() > 0);

    workspaceName = computed(() => this.context.workspace()?.name);
    currentSizeInBytes = computed(() => this.context.workspace()?.currentSizeInBytes ?? 0);
    maxSizeInBytes = computed(() => this.context.workspace()?.maxSizeInBytes ?? null);

    isFullEncryption = computed(() => this.context.workspace()?.storageEncryptionType === 'full');
    areBoxesSupported = computed(() => !this.isFullEncryption());
    areQuickSharesSupported = computed(() => !this.isFullEncryption());
    isTrashEnabled = computed(() => this.context.workspace()?.trashPolicy?.enabled ?? false);

    allowShare = computed(() => this.context.workspace()?.permissions?.allowShare ?? false);
    isTeamVisible = computed(() => {
        const allowShare = this.allowShare();

        if(!allowShare)
            return false;

        const worksapce = this.context.workspace();

        if(!worksapce)
            return false;

        if(worksapce.maxTeamMembers == 0 && worksapce.currentTeamMembersCount == 0)
            return false;

        return true;
    });


    private _uploadsCountChangedSubscription: Subscription | null = null;

    constructor(
        public auth: AuthService,
        private _signOutService: SignOutService,
        private _router: Router,
        private _activatedRoute: ActivatedRoute,
        private _fileUploadManager: FileUploadManager,
        public context: WorkspaceContextService,
        public dataStore: DataStore,
        private _capabilities: AppCapabilitiesService
    ) {
        effect(() => {
            const workspace = this.context.workspace();
            const isUnlocked = this.auth.isEncryptionUnlocked();

            if (workspace?.storageEncryptionType === 'full' && !isUnlocked) {
                this._router.navigate(['workspaces']);
            }
        });
    }

    private _subscription: Subscription | null = null;

    async ngOnInit(): Promise<void> {
        await this.auth.initiateSessionIfNeeded();

        // App-wide capability flags (ffmpeg presence) gate features across all workspace child
        // views (explorer media actions, config thumbnail/dimensions cards). Loaded once here so
        // a hard refresh on any child view has them; the call is idempotent.
        this._capabilities.ensureLoaded();

        this._subscription = this._activatedRoute.params.subscribe(async (params) => {
            this._workspaceExternalId = params['workspaceExternalId'] || null;
            
            await this.loadWorkspaceDetailsIfMissing();
        });

        this._uploadsCountChangedSubscription = this
            ._fileUploadManager
            .uploadsCountChanged$
            .subscribe(async (count) => this.pendingUploadCount.set(count));
    }

    private async loadWorkspaceDetailsIfMissing() {
        let currentWorkspace = this
            .context
            .workspace();

        if (currentWorkspace?.externalId !== this._workspaceExternalId) {
            if (this._workspaceExternalId) {
                currentWorkspace = await this
                    .dataStore
                    .getWorkspaceDetails(this._workspaceExternalId);

                this.context.workspace.set(currentWorkspace);
            }
        }

        this.pendingUploadCount.set(currentWorkspace!.pendingUploadsCount);
        this.isFirstWorkspaceLoaded.set(true);
    }

    ngOnDestroy(): void {
        this._subscription?.unsubscribe();
        this._uploadsCountChangedSubscription?.unsubscribe();
    }

    goToDashboard() {
        this._router.navigate(['workspaces']);
        this.isMenuOpen.set(false);
    }

    goToExplorer() {
        this._router.navigate(['/workspaces/' + this._workspaceExternalId + '/explorer']);
        this.isMenuOpen.set(false);
    }

    goToBoxes() {
        this._router.navigate(['/workspaces/' + this._workspaceExternalId + '/boxes']);
        this.isMenuOpen.set(false);
    }

    goToQuickShares() {
        this._router.navigate(['/workspaces/' + this._workspaceExternalId + '/quick-shares']);
        this.isMenuOpen.set(false);
    }

    goToUploads() {
        this._router.navigate(['/workspaces/' + this._workspaceExternalId + '/uploads']);
        this.isMenuOpen.set(false);
    }

    goToTeam() {
        this._router.navigate(['/workspaces/' + this._workspaceExternalId + '/team']);
        this.isMenuOpen.set(false);
    }
    
    goToWorkspaceConfig() {
        this._router.navigate(['/workspaces/' + this._workspaceExternalId + '/config']);
        this.isMenuOpen.set(false);
    }

    goToTrash() {
        this._router.navigate(['/workspaces/' + this._workspaceExternalId + '/trash']);
        this.isMenuOpen.set(false);
    }
    
    goToAccount() {
        this._router.navigate(['/account']);
        this.isMenuOpen.set(false);
    }

    async signOut() {
        await this._signOutService.signOut();
    }

    isExplorerActive() {        
        const explorerUrl = `/workspaces/${this._workspaceExternalId}/explorer`;
        return this.isUrlActive(explorerUrl);
    }
    
    isBoxesActive() {
        const boxesUrl = `/workspaces/${this._workspaceExternalId}/boxes`;
        return this.isUrlActive(boxesUrl);
    }

    isQuickSharesActive() {
        const url = `/workspaces/${this._workspaceExternalId}/quick-shares`;
        return this.isUrlActive(url);
    }
    
    isUploadsActive() {
        const uploadsUrl = `/workspaces/${this._workspaceExternalId}/uploads`;
        return this.isUrlActive(uploadsUrl);
    }
    
    isTeamActive() {
        const teamUrl = `/workspaces/${this._workspaceExternalId}/team`;
        return this.isUrlActive(teamUrl);
    }

    isWorkspaceConfigActive() {
        const configUrl = `/workspaces/${this._workspaceExternalId}/config`;
        return this.isUrlActive(configUrl);
    }

    isTrashActive() {
        const trashUrl = `/workspaces/${this._workspaceExternalId}/trash`;
        return this.isUrlActive(trashUrl);
    }

    private isUrlActive(url: string) {
        return this._router.isActive(url, {
            paths: 'subset',
            queryParams: 'ignored',
            fragment: 'ignored',
            matrixParams: 'ignored'
        });
    }

    prefetchWorkspaceTopFolders() {
        if(this.isExplorerActive())
            return;

        this.dataStore.prefetchWorkspaceTopFolders(this._workspaceExternalId);
    }

    prefetchBoxes() {
        if(this.isBoxesActive())
            return;

        this.dataStore.prefetchBoxes(this._workspaceExternalId);
    }

    prefetchQuickShares() {
        if(this.isQuickSharesActive())
            return;

        this.dataStore.prefetchQuickShares(this._workspaceExternalId);
    }

    prefetchUploads() {
        if(this.isUploadsActive())
            return;

        this.dataStore.prefetchUploads(this._workspaceExternalId);
    }

    prefetchWorkspaceMemberList() {
        if(this.isTeamActive())
            return;

        this.dataStore.prefetchWorkspaceMemberList(this._workspaceExternalId);
    }

    prefetchTrash() {
        if(this.isTrashActive())
            return;

        this.dataStore.prefetchTrash(this._workspaceExternalId);
    }

    toggleMenu() {
        this.isMenuOpen.set(!this.isMenuOpen());
    }
}
