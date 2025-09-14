import { Component, OnDestroy, OnInit, ViewEncapsulation, computed, signal } from '@angular/core';
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
import { SignOutService } from '../services/sign-out.service';
import { FooterComponent } from '../static-pages/shared/footer/footer.component';
import { WorkspaceSizeComponent } from '../shared/workspace-size/workspace-size.component';

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
        public dataStore: DataStore
    ) {
    }

    private _subscription: Subscription | null = null;

    async ngOnInit(): Promise<void> {
        await this.auth.initiateSessionIfNeeded();

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

    toggleMenu() {
        this.isMenuOpen.set(!this.isMenuOpen());
    }
}
