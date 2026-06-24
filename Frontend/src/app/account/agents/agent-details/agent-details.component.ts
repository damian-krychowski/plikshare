import { ActivatedRoute, Router } from "@angular/router";
import { Component, OnDestroy, OnInit, computed, signal, WritableSignal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatDialog } from "@angular/material/dialog";
import { Subscription } from "rxjs";
import { AuthService } from "../../../services/auth.service";
import { AgentsApi, GetAgentDetailsResponse } from "../../../services/agents.api";
import { ConfirmOperationDirective } from "../../../shared/operation-confirm/confirm-operation.directive";
import { ActionButtonComponent } from "../../../shared/buttons/action-btn/action-btn.component";
import { ConfigCardComponent } from "../../../shared/config-card/config-card.component";
import { RelativeTimeComponent } from "../../../shared/relative-time/relative-time.component";
import { WorkspacePickerComponent } from "../../../shared/workspace-picker/workspace-picker.component";
import { AdminWorkspaceListItem } from "../../../services/workspaces.api";
import { StorageNameItem, StoragesApi, AppStorageEncryptionType } from "../../../services/storages.api";
import { UserStorageAccessMode } from "../../../services/general-settings.api";
import { WorkspaceMaxNumberChangedEvent, WorkspaceNumberConfigComponent } from "../../../shared/workspace-number-config/workspace-number-config.component";
import { WorkspaceMaxSizeInBytesChangedEvent, WorkspaceSizeConfigComponent } from "../../../shared/workspace-size-config/workspace-size-config.component";
import { WorkspaceMaxTeamMembersChangedEvent, WorkspaceTeamConfigComponent } from "../../../shared/workspace-team-config/workspace-team-config.component";
import { StorageAccessChangedEvent, StorageAccessConfigComponent } from "../../../shared/storage-access-config/storage-access-config.component";
import { AppWorkspace, WorkspaceItemComponent } from "../../../shared/workspace-item/workspace-item.component";
import { AgentTokenDialogComponent, AgentTokenDialogData } from "../agent-token-dialog/agent-token-dialog.component";
import { AgentToolsConfigComponent } from "../../../shared/agent-tools-config/agent-tools-config.component";
import { AgentWorkspaceToolsDialogComponent, AgentWorkspaceToolsDialogData } from "../agent-workspace-tools-dialog/agent-workspace-tools-dialog.component";
import { GrantBoxAccessDialogComponent, GrantBoxAccessResult } from "../grant-box-access-dialog/grant-box-access-dialog.component";
import { AgentBoxToolsDialogComponent, AgentBoxToolsDialogData } from "../agent-box-tools-dialog/agent-box-tools-dialog.component";

@Component({
    selector: 'app-agent-details',
    imports: [
        MatButtonModule,
        MatTooltipModule,
        ConfirmOperationDirective,
        ActionButtonComponent,
        ConfigCardComponent,
        RelativeTimeComponent,
        WorkspaceNumberConfigComponent,
        WorkspaceSizeConfigComponent,
        WorkspaceTeamConfigComponent,
        StorageAccessConfigComponent,
        WorkspaceItemComponent,
        AgentToolsConfigComponent
    ],
    templateUrl: './agent-details.component.html',
    styleUrl: './agent-details.component.scss'
})
export class AgentDetailsComponent implements OnInit, OnDestroy {
    isLoading = signal(false);
    agent: WritableSignal<GetAgentDetailsResponse | null> = signal(null);

    availableStorages = signal<StorageNameItem[]>([]);

    maxWorkspaceNumber = computed(() => this.agent()?.agent.maxWorkspaceNumber ?? null);
    defaultMaxWorkspaceSizeInBytes = computed(() => this.agent()?.agent.defaultMaxWorkspaceSizeInBytes ?? null);
    defaultMaxWorkspaceTeamMembers = computed(() => this.agent()?.agent.defaultMaxWorkspaceTeamMembers ?? null);
    storageAccessMode = computed<UserStorageAccessMode>(() => this.agent()?.agent.storageAccess.mode ?? 'all');
    storageAccessExternalIds = computed(() => this.agent()?.agent.storageAccess.storageExternalIds ?? []);

    hasOwnedWorkspaces = computed(() => (this.agent()?.ownedWorkspaces.length ?? 0) > 0);

    toolsCountOf(workspaceExternalId: string | null): number {
        if (!workspaceExternalId)
            return 0;

        const agent = this.agent();

        if (!agent)
            return 0;

        const owned = agent.ownedWorkspaces.find(w => w.externalId === workspaceExternalId);

        if (owned)
            return owned.overriddenToolsCount;

        return agent.sharedWorkspaces.find(w => w.externalId === workspaceExternalId)?.overriddenToolsCount ?? 0;
    }

    openToolsDialog(workspace: AppWorkspace) {
        const workspaceExternalId = workspace.externalId();

        if (!this._agentExternalId || !workspaceExternalId)
            return;

        const dialogRef = this._dialog.open(AgentWorkspaceToolsDialogComponent, {
            width: '720px',
            maxWidth: '95vw',
            maxHeight: '85vh',
            position: { top: '60px' },
            data: {
                agentExternalId: this._agentExternalId,
                workspaceExternalId,
                workspaceName: workspace.name()
            } as AgentWorkspaceToolsDialogData
        });

        dialogRef.afterClosed().subscribe(async () => {
            await this.loadAgent();
        });
    }

    sharedWorkspaceItems: WritableSignal<AppWorkspace[]> = signal([]);

    private _agentExternalId: string | null = null;
    private _subscription: Subscription | null = null;

    constructor(
        public auth: AuthService,
        private _agentsApi: AgentsApi,
        private _storagesApi: StoragesApi,
        private _router: Router,
        private _activatedRoute: ActivatedRoute,
        private _dialog: MatDialog
    ) {}

    async ngOnInit(): Promise<void> {
        this._subscription = this._activatedRoute.params.subscribe(async (params) => {
            this._agentExternalId = params['agentExternalId'] || null;

            await Promise.all([
                this.loadAgent(),
                this.loadStorages()
            ]);
        });
    }

    ngOnDestroy(): void {
        this._subscription?.unsubscribe();
    }

    private async loadStorages() {
        try {
            const result = await this._storagesApi.getStorageNames();
            this.availableStorages.set(result.items);
        } catch (error) {
            console.error(error);
        }
    }

    private async loadAgent() {
        if (!this._agentExternalId)
            return;

        this.isLoading.set(true);

        try {
            const result = await this._agentsApi.getAgentDetails(this._agentExternalId);
            this.agent.set(result);

            this.sharedWorkspaceItems.set(result.sharedWorkspaces.map(w => this.toAppWorkspace(w)));
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    private toAppWorkspace(w: GetAgentDetailsResponse['sharedWorkspaces'][number]): AppWorkspace {
        return {
            type: 'app-workspace',
            externalId: signal(w.externalId),
            name: signal(w.name),
            currentSizeInBytes: signal(w.currentSizeInBytes),
            maxSizeInBytes: signal(w.maxSizeInBytes),
            owner: signal({ email: signal(w.owner.email), externalId: w.owner.externalId }),
            wasUserInvited: signal(true),
            permissions: { allowShare: false },
            isUsedByIntegration: false,
            isBucketCreated: signal(w.isBucketCreated),
            storageName: signal(w.storageName),
            storageExternalId: w.storageExternalId,
            storageEncryptionType: w.storageEncryptionType as AppStorageEncryptionType,
            isNameEditing: signal(false),
            isHighlighted: signal(false),
            isPendingKeyGrant: signal(false)
        };
    }

    goToAgents() {
        this._router.navigate(['settings/agents']);
    }

    async onMaxWorkspaceNumberChange(event: WorkspaceMaxNumberChangedEvent) {
        if (!this._agentExternalId)
            return;

        try {
            await this._agentsApi.updateMaxWorkspaceNumber(this._agentExternalId, event.maxNumber);
        } catch (error) {
            console.error(error);
        }
    }

    async onDefaultMaxWorkspaceSizeChange(event: WorkspaceMaxSizeInBytesChangedEvent) {
        if (!this._agentExternalId)
            return;

        try {
            await this._agentsApi.updateDefaultMaxWorkspaceSize(this._agentExternalId, event.maxSizeInBytes);
        } catch (error) {
            console.error(error);
        }
    }

    async onDefaultMaxWorkspaceTeamMembersChange(event: WorkspaceMaxTeamMembersChangedEvent) {
        if (!this._agentExternalId)
            return;

        try {
            await this._agentsApi.updateDefaultMaxWorkspaceTeamMembers(this._agentExternalId, event.maxTeamMembers);
        } catch (error) {
            console.error(error);
        }
    }

    async onStorageAccessChange(event: StorageAccessChangedEvent) {
        if (!this._agentExternalId)
            return;

        try {
            await this._agentsApi.updateStorageAccess(this._agentExternalId, event.mode, event.storageExternalIds);
        } catch (error) {
            console.error(error);
        }
    }

    async onRotateToken() {
        if (!this._agentExternalId)
            return;

        try {
            const result = await this._agentsApi.rotateToken(this._agentExternalId);

            const data: AgentTokenDialogData = {
                title: 'Token rotated',
                token: result.token
            };

            this._dialog.open(AgentTokenDialogComponent, {
                width: '700px',
                maxWidth: '95vw',
                position: { top: '80px' },
                disableClose: true,
                data
            });

            await this.loadAgent();
        } catch (error) {
            console.error(error);
        }
    }

    async onDelete() {
        if (!this._agentExternalId)
            return;

        try {
            await this._agentsApi.deleteAgent(this._agentExternalId);
            this.goToAgents();
        } catch (error) {
            console.error(error);
        }
    }

    onGrantWorkspace() {
        const alreadyGrantedExternalIds = [
            ...this.sharedWorkspaceItems().map(w => w.externalId()).filter((id): id is string => id !== null),
            ...(this.agent()?.ownedWorkspaces.map(w => w.externalId) ?? [])
        ];

        const dialogRef = this._dialog.open(WorkspacePickerComponent, {
            width: '500px',
            position: { top: '100px' },
            data: {
                subtitle: 'The selected workspace will become fully accessible to this agent.',
                alreadyGrantedExternalIds
            }
        });

        dialogRef.afterClosed().subscribe(async (workspace: AdminWorkspaceListItem | undefined) => {
            if (!workspace || !this._agentExternalId)
                return;

            try {
                await this._agentsApi.grantWorkspaceAccess(this._agentExternalId, workspace.externalId);
                await this.loadAgent();
            } catch (error) {
                console.error(error);
            }
        });
    }

    async onSharedWorkspaceAccessRevoked(workspace: AppWorkspace) {
        const externalId = workspace.externalId();

        if (!this._agentExternalId || !externalId)
            return;

        try {
            await this._agentsApi.revokeWorkspaceAccess(this._agentExternalId, externalId);
            await this.loadAgent();
        } catch (error) {
            console.error(error);
        }
    }

    onGrantBox() {
        const alreadyGrantedBoxExternalIds = this.agent()?.sharedBoxes.map(b => b.boxExternalId) ?? [];

        const dialogRef = this._dialog.open(GrantBoxAccessDialogComponent, {
            width: '560px',
            maxWidth: '95vw',
            position: { top: '100px' },
            data: {
                alreadyGrantedBoxExternalIds
            }
        });

        dialogRef.afterClosed().subscribe(async (result: GrantBoxAccessResult | undefined) => {
            if (!result || !this._agentExternalId)
                return;

            try {
                await this._agentsApi.grantBoxAccess(this._agentExternalId, result.boxExternalId);
                await this.loadAgent();
            } catch (error) {
                console.error(error);
            }
        });
    }

    async onRevokeBox(box: GetAgentDetailsResponse['sharedBoxes'][number]) {
        if (!this._agentExternalId)
            return;

        try {
            await this._agentsApi.revokeBoxAccess(this._agentExternalId, box.boxExternalId);
            await this.loadAgent();
        } catch (error) {
            console.error(error);
        }
    }

    openBox(box: GetAgentDetailsResponse['sharedBoxes'][number]) {
        this._router.navigate([`workspaces/${box.workspaceExternalId}/boxes/${box.boxExternalId}`]);
    }

    openBoxToolsDialog(box: GetAgentDetailsResponse['sharedBoxes'][number]) {
        if (!this._agentExternalId)
            return;

        const dialogRef = this._dialog.open(AgentBoxToolsDialogComponent, {
            width: '720px',
            maxWidth: '95vw',
            maxHeight: '85vh',
            position: { top: '60px' },
            data: {
                agentExternalId: this._agentExternalId,
                boxExternalId: box.boxExternalId,
                boxName: box.boxName
            } as AgentBoxToolsDialogData
        });

        dialogRef.afterClosed().subscribe(async () => {
            await this.loadAgent();
        });
    }
}
