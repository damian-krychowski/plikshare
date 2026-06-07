import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { Subscription, filter } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { BatchProgress, ImageDimensionsPolicyDto, TrashPolicyDto, WorkspacesApi } from '../../services/workspaces.api';
import { BatchProgressComponent } from '../../shared/batch-progress/batch-progress.component';
import { DataStore } from '../../services/data-store.service';
import { AuthService } from '../../services/auth.service';
import { WorkspaceContextService } from '../workspace-context.service';
import { WorkspaceMaxSizeInBytesChangedEvent, WorkspaceSizeConfigComponent } from '../../shared/workspace-size-config/workspace-size-config.component';
import { Debouncer } from '../../services/debouncer';
import { WorkspaceMaxTeamMembersChangedEvent, WorkspaceTeamConfigComponent } from '../../shared/workspace-team-config/workspace-team-config.component';
import { ActionTextButtonComponent } from '../../shared/buttons/action-text-btn/action-text-btn.component';
import { AuditLogPolicyApi } from '../../account/audit-log/policy/audit-log-policy.api';
import { TrashPolicyConfigChangedEvent, TrashPolicyConfigComponent } from '../../shared/trash-policy-config/trash-policy-config.component';
import { ImageDimensionsPolicyConfigChangedEvent, ImageDimensionsPolicyConfigComponent } from '../../shared/image-dimensions-policy-config/image-dimensions-policy-config.component';
import { ConfigCardComponent } from '../../shared/config-card/config-card.component';
import { GenericDialogService } from '../../shared/generic-message-dialog/generic-dialog-service';
import { AppCapabilitiesService } from '../../services/app-capabilities.service';



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
        ImageDimensionsPolicyConfigComponent,
        ConfigCardComponent,
        ActionTextButtonComponent,
        BatchProgressComponent
    ],
    templateUrl: './workspace-config.component.html',
    styleUrl: './workspace-config.component.scss'
})
export class WorkspaceConfigComponent implements OnInit, OnDestroy {
    isLoading = signal(false);

    public maxSizeInBytes = signal<number|null>(null);
    public maxTeamMembers = signal<number|null>(null);
    public trashPolicy = signal<TrashPolicyDto|null>(null);
    public imageDimensionsPolicy = signal<ImageDimensionsPolicyDto|null>(null);

    // Live progress of the image-dimensions backfill batch (null = nothing running). Sourced from
    // the server (queue), so it survives reloads and shows to any user viewing these settings.
    public backfillProgress = signal<BatchProgress | null>(null);
    private _backfillBatchId: string | null = null;
    private _backfillUnsub: (() => void) | null = null;

    private _capabilities = inject(AppCapabilitiesService);
    public isFfmpegAvailable = computed(() => this._capabilities.capabilities().isFfmpegAvailable);

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
        private _auditLogPolicyApi: AuditLogPolicyApi,
        private _genericDialog: GenericDialogService)
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
        this.stopBackfillTracking();
    }

    private async loadBackfillStatus(workspaceExternalId: string) {
        try {
            const status = await this._workspacesApi.getImageDimensionsBackfillStatus(
                workspaceExternalId);

            if (status.batchId) {
                this.startBackfillTracking(workspaceExternalId, status.batchId, status);
            } else {
                this.stopBackfillTracking();
            }
        } catch (err) {
            console.error('Failed to load image-dimensions backfill status', err);
        }
    }

    private startBackfillTracking(
        workspaceExternalId: string,
        batchId: string,
        initial: BatchProgress) {

        // Already streaming this batch — just refresh the snapshot, don't reopen the SSE.
        if (this._backfillBatchId === batchId) {
            this.backfillProgress.set(initial);
            return;
        }

        this.stopBackfillTracking();

        this._backfillBatchId = batchId;
        this.backfillProgress.set(initial);

        this._backfillUnsub = this._workspacesApi.subscribeImageDimensionsBatch(
            workspaceExternalId,
            batchId,
            progress => {
                this.backfillProgress.set(progress);

                if (progress.pending === 0)
                    this.stopBackfillTracking();
            });
    }

    private stopBackfillTracking() {
        this._backfillUnsub?.();
        this._backfillUnsub = null;
        this._backfillBatchId = null;
        this.backfillProgress.set(null);
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
            this.imageDimensionsPolicy.set(workspace.mediaProcessingPolicy.imageDimensions);

            // Fire-and-forget; the chip / bar pop in once they resolve. Errors are logged, not surfaced.
            this.loadAuditLogSummary(workspaceExternalId);
            this.loadBackfillStatus(workspaceExternalId);
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
        const previous = this.trashPolicy();
        const next = event.trashPolicy;

        // Turning trash off purges whatever is already in it at the next cleanup sweep —
        // make the admin confirm before that happens.
        const isDisablingTrash = !!previous?.enabled && !next.enabled;

        if (isDisablingTrash) {
            this._genericDialog.openGenericMessageDialog({
                title: 'Disable trash?',
                message: "Any files currently in this workspace's trash will be permanently "
                    + 'deleted at the next scheduled cleanup, which runs about once an hour. '
                    + 'This cannot be undone.',
                confirmButtonText: 'Disable trash',
                showCancelButton: true,
                cancelButtonText: 'Keep trash on',
                isDanger: true
            }).subscribe(confirmed => {
                if (confirmed) {
                    this.trashPolicy.set(next);
                    this._trashPolicyDebouncer.debounceAsync(() => this.saveTrashPolicy());
                } else {
                    // Revert the picker — a fresh object reference forces the child to re-init.
                    this.trashPolicy.set(previous ? { ...previous } : previous);
                }
            });
            return;
        }

        this.trashPolicy.set(next);
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

    private _imageDimensionsPolicyDebouncer = new Debouncer(500);
    onImageDimensionsPolicyChange(event: ImageDimensionsPolicyConfigChangedEvent) {
        const previous = this.imageDimensionsPolicy();
        const next = event.imageDimensions;

        const wasEnabled = !!previous?.extractOnUpload;
        const willEnable = next.extractOnUpload;

        if (willEnable && !wasEnabled) {
            this.confirmEnableImageDimensions(next);
        } else if (!willEnable && wasEnabled) {
            this.confirmDisableImageDimensions(next);
        } else {
            this.applyImageDimensionsPolicy(next);
        }
    }

    private async confirmEnableImageDimensions(next: ImageDimensionsPolicyDto) {
        const workspaceExternalId = this._currentWorkspaceExternalId;
        if (!workspaceExternalId) return;

        let count = 0;

        try {
            const result = await this._workspacesApi.getImageDimensionsBackfillCount(workspaceExternalId);
            count = result.fileCount;
        } catch (err) {
            console.error('Failed to count images to backfill', err);
        }

        // No existing images to process — enabling still applies to future uploads, so just do it.
        if (count === 0) {
            this.applyImageDimensionsPolicy(next);
            return;
        }

        this._genericDialog.openGenericMessageDialog({
            title: 'Extract image dimensions?',
            message: `Image dimensions will be extracted for ${count} existing image(s) in this workspace `
                + 'now, and for every image uploaded from now on.',
            confirmButtonText: 'Extract dimensions',
            showCancelButton: true,
            cancelButtonText: 'Cancel'
        }).subscribe(confirmed => {
            if (confirmed) {
                this.applyImageDimensionsPolicy(next);
            } else {
                this.revertImageDimensionsPolicy();
            }
        });
    }

    private confirmDisableImageDimensions(next: ImageDimensionsPolicyDto) {
        const hasActiveBackfill = this.backfillProgress() !== null;

        const message = hasActiveBackfill
            ? 'Disabling will cancel image-dimension extraction for any images not yet processed. '
                + 'Dimensions already extracted are kept.'
            : 'New uploads will no longer have their image dimensions extracted.';

        this._genericDialog.openGenericMessageDialog({
            title: 'Disable image dimensions?',
            message,
            confirmButtonText: 'Disable',
            showCancelButton: true,
            cancelButtonText: 'Keep enabled',
            isDanger: hasActiveBackfill
        }).subscribe(confirmed => {
            if (confirmed) {
                this.applyImageDimensionsPolicy(next);
            } else {
                this.revertImageDimensionsPolicy();
            }
        });
    }

    private applyImageDimensionsPolicy(next: ImageDimensionsPolicyDto) {
        this.imageDimensionsPolicy.set(next);
        this._imageDimensionsPolicyDebouncer.debounceAsync(() => this.saveImageDimensionsPolicy());
    }

    private revertImageDimensionsPolicy() {
        // The parent signal still holds the old value (we never set the new one). Push a fresh
        // object reference so the child dropdown re-inits and snaps back to it.
        const current = this.imageDimensionsPolicy();
        this.imageDimensionsPolicy.set(current ? { ...current } : current);
    }

    private async saveImageDimensionsPolicy(){
        if(!this._currentWorkspaceExternalId)
            return;

        const policy = this.imageDimensionsPolicy();
        if(!policy)
            return;

        try {
            this.isLoading.set(true);

            const response = await this._workspacesApi.updateImageDimensionsPolicy(
                this._currentWorkspaceExternalId,
                { extractOnUpload: policy.extractOnUpload });

            if (response.batchId) {
                this.startBackfillTracking(
                    this._currentWorkspaceExternalId,
                    response.batchId,
                    {
                        total: response.totalFiles,
                        completed: 0,
                        failed: 0,
                        pending: response.totalFiles
                    });
            } else if (!policy.extractOnUpload) {
                // Disabling cancels any running backfill server-side — drop the bar.
                this.stopBackfillTracking();
            }

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