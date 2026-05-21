import { Component, computed, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { Subscription, filter } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { TrashPolicyDto, WorkspacesApi } from '../../services/workspaces.api';
import { DataStore } from '../../services/data-store.service';
import { AuthService } from '../../services/auth.service';
import { WorkspaceContextService } from '../workspace-context.service';
import { WorkspaceMaxSizeInBytesChangedEvent, WorkspaceSizeConfigComponent } from '../../shared/workspace-size-config/workspace-size-config.component';
import { Debouncer } from '../../services/debouncer';
import { WorkspaceMaxTeamMembersChangedEvent, WorkspaceTeamConfigComponent } from '../../shared/workspace-team-config/workspace-team-config.component';
import { ActionTextButtonComponent } from '../../shared/buttons/action-text-btn/action-text-btn.component';
import { AuditLogPolicyApi } from '../../account/audit-log/policy/audit-log-policy.api';
import { TrashPolicyConfigChangedEvent, TrashPolicyConfigComponent } from '../../shared/trash-policy-config/trash-policy-config.component';
import { ConfigCardComponent } from '../../shared/config-card/config-card.component';



@Component({
    selector: 'app-workspace-config',
    imports: [
        MatFormFieldModule,
        MatInputModule,
        MatCheckboxModule,
        ReactiveFormsModule,
        MatButtonModule,
        WorkspaceSizeConfigComponent,
        WorkspaceTeamConfigComponent,
        TrashPolicyConfigComponent,
        ConfigCardComponent,
        ActionTextButtonComponent
    ],
    templateUrl: './workspace-config.component.html',
    styleUrl: './workspace-config.component.scss'
})
export class WorkspaceConfigComponent implements OnInit, OnDestroy {
    isLoading = signal(false);

    public maxSizeInBytes = signal<number|null>(null);
    public maxTeamMembers = signal<number|null>(null);
    public trashPolicy = signal<TrashPolicyDto|null>(null);

    // The audit-log section is admin-only — same authorization as the backend endpoint
    // (RequireAdminPermissionEndpointFilter(Permissions.ManageAuditLog), which bypasses the
    // permission check for the app owner).
    public canConfigureAuditLog = computed(() =>
        this.auth.isAppOwner() || this.auth.canManageAuditLog());

    // Audit-log policy summary for THIS workspace — shown as a chip so admin sees at a glance
    // whether the workspace is on global defaults or has been customized. Counts null until the
    // first load (or hidden entirely when the chip is not authorized).
    public auditLogDisabledCount = signal<number | null>(null);
    public auditLogSeverityOverrideCount = signal<number | null>(null);

    public auditLogSummaryText = computed(() => {
        const disabled = this.auditLogDisabledCount();
        const overrides = this.auditLogSeverityOverrideCount();
        if (disabled === null || overrides === null) return null;
        if (disabled === 0 && overrides === 0) return 'defaults';

        const parts: string[] = [];
        if (disabled > 0) parts.push(`${disabled} disabled`);
        if (overrides > 0) parts.push(`${overrides} severity ${overrides === 1 ? 'override' : 'overrides'}`);
        return parts.join(' · ');
    });

    public isAuditLogCustomized = computed(() => {
        const disabled = this.auditLogDisabledCount();
        const overrides = this.auditLogSeverityOverrideCount();
        return (disabled ?? 0) > 0 || (overrides ?? 0) > 0;
    });

    private _currentWorkspaceExternalId: string | null = null;
    private _routerSubscription: Subscription | null = null;

    constructor(
        private _workspaceContext: WorkspaceContextService,
        private _workspacesApi: WorkspacesApi,
        private _activatedRoute: ActivatedRoute,
        private _router: Router,
        private _dataStore: DataStore,
        public auth: AuthService,
        private _auditLogPolicyApi: AuditLogPolicyApi)
    {
    }

    goToAuditLogPolicy() {
        const externalId = this._currentWorkspaceExternalId;
        if (!externalId) return;
        this._router.navigate(['settings/audit-log/policy/workspaces', externalId]);
    }

    private async loadAuditLogSummary(workspaceExternalId: string) {
        // Skip the round-trip when the user can't see the section anyway.
        if (!this.canConfigureAuditLog()) return;

        try {
            const policy = await this._auditLogPolicyApi.getWorkspacePolicy(workspaceExternalId);
            this.auditLogDisabledCount.set(policy.disabledEventTypes.length);
            this.auditLogSeverityOverrideCount.set(
                policy.severityOverrides ? Object.keys(policy.severityOverrides).length : 0);
        } catch (err) {
            console.error('Failed to load audit-log policy summary', err);
        }
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
            this.trashPolicy.set(workspace.trashPolicy);

            // Fire-and-forget; the chip pops in once it resolves. Errors are logged, not surfaced.
            this.loadAuditLogSummary(workspaceExternalId);
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

    private _trashPolicyDebouncer = new Debouncer(500);
    onTrashPolicyChange(event: TrashPolicyConfigChangedEvent) {
        this.trashPolicy.set(event.trashPolicy);
        this._trashPolicyDebouncer.debounceAsync(() => this.saveTrashPolicy());
    }

    private async saveTrashPolicy(){
        if(!this._currentWorkspaceExternalId)
            return;

        const trashPolicy = this.trashPolicy();
        if(!trashPolicy)
            return;

        try {
            this.isLoading.set(true);

            await this._workspacesApi.updateTrashPolicy(this._currentWorkspaceExternalId, {
                enabled: trashPolicy.enabled,
                retentionDays: trashPolicy.retentionDays
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