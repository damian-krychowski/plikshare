import { Component, computed, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { Subscription, filter } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { WorkspacesApi } from '../../services/workspaces.api';
import { DataStore } from '../../services/data-store.service';
import { WorkspaceContextService } from '../workspace-context.service';
import { WorkspaceMaxSizeInBytesChangedEvent, WorkspaceSizeConfigComponent } from '../../shared/workspace-size-config/workspace-size-config.component';
import { Debouncer } from '../../services/debouncer';
import { WorkspaceMaxTeamMembersChangedEvent, WorkspaceTeamConfigComponent } from '../../shared/workspace-team-config/workspace-team-config.component';



@Component({
    selector: 'app-workspace-config',
    imports: [
        MatFormFieldModule,
        MatInputModule,
        MatCheckboxModule,
        ReactiveFormsModule,
        MatButtonModule,
        WorkspaceSizeConfigComponent,
        WorkspaceTeamConfigComponent
    ],
    templateUrl: './workspace-config.component.html',
    styleUrl: './workspace-config.component.scss'
})
export class WorkspaceConfigComponent implements OnInit, OnDestroy {
    isLoading = signal(false);
   
    public maxSizeInBytes = signal<number|null>(null);
    public maxTeamMembers = signal<number|null>(null);

    private _currentWorkspaceExternalId: string | null = null;
    private _routerSubscription: Subscription | null = null;

    constructor(
        private _workspaceContext: WorkspaceContextService,
        private _workspacesApi: WorkspacesApi,
        private _activatedRoute: ActivatedRoute,
        private _router: Router,
        private _dataStore: DataStore) 
    {    
    }

    async ngOnInit() {
        this.load();
                
        this._routerSubscription = this._router.events
            .pipe(filter(event => event instanceof NavigationEnd))
            .subscribe(() => this.load());
    }

    ngOnDestroy(): void {
        this._routerSubscription?.unsubscribe();
    }

    private async load() {
        try {
            this.isLoading.set(true);
            
            const workspaceExternalId = this._activatedRoute.parent?.snapshot.params['workspaceExternalId'];

            if (!workspaceExternalId)
                throw new Error('workspaceExternalId is missing');
                
            this._currentWorkspaceExternalId = workspaceExternalId;
            
            const workspace = await this
                ._workspacesApi
                .getWorkspace(workspaceExternalId);

            //we refresh current state of workspace inside the service
            //to have the most recent version there
            this._workspaceContext.workspace.set(workspace); 

            this.maxSizeInBytes.set(workspace.maxSizeInBytes);
            this.maxTeamMembers.set(workspace.maxTeamMembers);
        } catch (error) {
            console.error('Failed to load workspace configuration', error);
        } finally {
            this.isLoading.set(false);
        }
    }
    
    private _maxSizeDebouncer = new Debouncer(500);
    onMaxSizeInBytesChange(event: WorkspaceMaxSizeInBytesChangedEvent) {
        this.maxSizeInBytes.set(event.maxSizeInBytes);
        this._maxSizeDebouncer.debounceAsync(() => this.saveMaxSizeInBytes());
    }

    private async saveMaxSizeInBytes(){
        if(!this._currentWorkspaceExternalId)
            return;

        try {
            this.isLoading.set(true);
            
            await this._workspacesApi.updateMaxSize(this._currentWorkspaceExternalId, {
                maxSizeInBytes: this.maxSizeInBytes()
            });

            const workspace = await this
                ._workspacesApi
                .getWorkspace(this._currentWorkspaceExternalId);

            this._dataStore.clearWorkspaceDetails(this._currentWorkspaceExternalId);
            this._workspaceContext.workspace.set(workspace);
        } catch (error) {
            console.error('Failed to save workspace configuration', error);
        } finally {
            this.isLoading.set(false);
        }
    }
    
    private _maxTeamMembersDebouncer = new Debouncer(500);
    onMaxTeamMembersChange(event: WorkspaceMaxTeamMembersChangedEvent) {
        this.maxTeamMembers.set(event.maxTeamMembers);
        this._maxTeamMembersDebouncer.debounceAsync(() => this.saveMaxTeamMembers());
    }

    private async saveMaxTeamMembers(){
        if(!this._currentWorkspaceExternalId)
            return;

        try {
            this.isLoading.set(true);
            
            await this._workspacesApi.updateMaxTeamMembers(this._currentWorkspaceExternalId, {
                maxTeamMembers: this.maxTeamMembers()
            });

            const workspace = await this
                ._workspacesApi
                .getWorkspace(this._currentWorkspaceExternalId);

            this._dataStore.clearWorkspaceDetails(this._currentWorkspaceExternalId);
            this._workspaceContext.workspace.set(workspace);
        } catch (error) {
            console.error('Failed to save workspace configuration', error);
        } finally {
            this.isLoading.set(false);
        }
    }
}