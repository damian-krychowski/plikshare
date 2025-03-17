import { Component, OnDestroy, OnInit, Signal, WritableSignal, computed, input, output, signal } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { NavigationExtras, Router } from '@angular/router';
import { StorageSizePipe } from '../storage-size.pipe';
import { WorkspacesApi } from '../../services/workspaces.api';
import { WorkspaceContextService } from '../../workspace-manager/workspace-context.service';
import { EditableTxtComponent } from '../editable-txt/editable-txt.component';
import { ConfirmOperationDirective } from '../operation-confirm/confirm-operation.directive';
import { InAppSharing } from '../../services/in-app-sharing.service';
import { MatDialog } from '@angular/material/dialog';
import { EmailPickerComponent } from '../email-picker/email-picker.component';
import { PrefetchDirective } from '../prefetch.directive';
import { DataStore } from '../../services/data-store.service';
import { AuthService } from '../../services/auth.service';
import { AppUser } from '../app-user';
import { UserLinkComponenet } from '../user-link/user-link.component';
import { toggle } from '../signal-utils';
import { ActionButtonComponent } from '../buttons/action-btn/action-btn.component';
import { UserPickerComponent } from '../user-picker/user-picker.component';
import { AppUserDetails } from '../user-item/app-user';
import { observeIsHighlighted } from '../../services/is-highlighted-utils';

export type AppWorkspace = {
    type: 'app-workspace';
    
    externalId: WritableSignal<string | null>;
    name: WritableSignal<string>;
    currentSizeInBytes: WritableSignal<number>;
    owner: WritableSignal<AppUser>;
    wasUserInvited: Signal<boolean>;
    permissions: {
        allowShare: boolean;
    };    
    isUsedByIntegration: boolean;
    isBucketCreated: WritableSignal<boolean>;
    storageName: Signal<string | null>;
    isNameEditing: WritableSignal<boolean>;
    isHighlighted: WritableSignal<boolean>;
};

@Component({
    selector: 'app-workspace-item',
    imports: [
        MatProgressBarModule,
        StorageSizePipe,
        EditableTxtComponent,
        ConfirmOperationDirective,
        PrefetchDirective,
        UserLinkComponenet,
        ActionButtonComponent
    ],
    templateUrl: './workspace-item.component.html',
    styleUrl: './workspace-item.component.scss'
})
export class WorkspaceItemComponent implements OnInit, OnDestroy {

    workspace = input.required<AppWorkspace>();

    canOpen = input(false);
    canLocate = input(false);
    isAdminView = input(false);

    left = output<void>();
    deleted = output<void>();
    accessRevoked = output<void>();
    ownerChanged = output<void>();

    externalId = computed(() => this.workspace().externalId());
    name = computed(() => this.workspace().name());
    currentSizeInBytes = computed(() => this.workspace().currentSizeInBytes());
    storageName = computed(() => this.workspace().storageName());
    owner = computed(() => this.workspace().owner());

    isNameEditing = computed(() => this.workspace().isNameEditing());    
    isHighlighted = observeIsHighlighted(this.workspace);

    isOwnedByUser = computed(() => this.workspace().owner().externalId == this.auth.userExternalId());
    hasCurrentSizeInBytes = computed(() => this.currentSizeInBytes() != null);
    wasUserInvited = computed(() => this.workspace().wasUserInvited());
    isUsedByIntegration = computed(() => this.workspace().isUsedByIntegration);
    isBucketCreated = computed(() => this.workspace().isBucketCreated());

    canLeave = computed(() => this.wasUserInvited() && !this.isAdminView());
    canRevokeAccess = computed(() => this.wasUserInvited() && this.isAdminView());
    canShare = computed(() => this.workspace().permissions.allowShare || this.isAdminView());

    canDelete = computed(() => this.isOwnedByUser() || this.auth.isAdmin());
    canChangeOwner = computed(() => this.auth.canManageUsers());

    areActionsVisible = signal(false);

    private _pollingInterval: any;

    constructor(
        public auth: AuthService,
        private _workspacesApi: WorkspacesApi,
        private _router: Router,
        private _inAppSharing: InAppSharing,
        private _dialog: MatDialog,
        public dataStore: DataStore
    ) {

    }

    ngOnInit(): void {
        this.initBucketPolling();
    }

    ngOnDestroy(): void {
        if(this._pollingInterval) {
            clearInterval(this._pollingInterval);
        }
    }

    private async initBucketPolling() {
        const workspace = this.workspace();

        if (!workspace.isBucketCreated()) {
            this._pollingInterval = setInterval(async () => {
                const externalId = workspace.externalId();
                if (!externalId) return;

                const status = await this._workspacesApi.getWorkspaceBucketStatus(externalId);
                if (status.isBucketCreated) {
                    workspace.isBucketCreated.set(true);
                    clearInterval(this._pollingInterval);
                    this._pollingInterval = null;
                }
            }, 1000);
        }
    }

    public openWorkspace() {
        const workspace = this.workspace();

        if(workspace.isHighlighted())
            workspace.isHighlighted.set(false);

        const externalId = workspace.externalId();

        if(!this.canOpen() || !externalId || workspace.isNameEditing()) 
            return;

        this._router.navigate([`/workspaces/${externalId}/explorer`]);
    }

    async saveWorkspaceName(newName: string) {
        const workspace = this.workspace();
        const externalId = workspace.externalId();

        if (!externalId)
            return

        workspace.name.set(newName);
        
        await this._workspacesApi.updateName(
            externalId, {
            name: newName
        });

        this.dataStore.clearWorkspaceDetails(externalId);
    }

    shareWorkspace() {
        const externalId = this.externalId();

        if(!externalId)
            return;

        const dialogRef = this._dialog.open(EmailPickerComponent, {
            width: '500px',
            maxHeight: '80vh',
            position: {
                top: '100px'
            }
        });

        dialogRef.afterClosed().subscribe((inviteeEmails: string[]) => {
            if(!inviteeEmails || inviteeEmails.length === 0)
                return;
            
            const temporaryKey = this._inAppSharing.set(
                inviteeEmails);

            const navigationExtras: NavigationExtras = {
                state: {
                    emailsToInvite: temporaryKey
                }
            };

            this._router.navigate(
                [`/workspaces/${externalId}/team`], 
                navigationExtras);     
        });       
    }

    async leaveWorkspace() {
        const externalId = this.externalId();
        
        if(!externalId)
            return;

        this.left.emit();

        await this._workspacesApi.leaveWorkspace(
            externalId);
    }

    async deleteWorkspace() {
        const externalId = this.externalId();

        if(!externalId)
            return;

        this.deleted.emit();

        await this._workspacesApi.deleteWorkspace(
            externalId);
    }

    prefetchDashboard() {
        this.dataStore.prefetchDashboardData();    
    }

    prefetchWorkspace() {
        const externalId = this.externalId();

        if(!externalId)
            return;

        this.dataStore.prefetchWorkspaceTopFolders(
            externalId);

        this.dataStore.prefetchWorkspaceDetails(
            externalId);
    }

    locate() {
        const externalId = this.externalId();

        if(!externalId)
            return;

        const temporaryKey = this._inAppSharing.set(
            externalId);

        const navigationExtras: NavigationExtras = {
            state: {
                workspaceToHighlight: temporaryKey
            }
        };

        this._router.navigate([`/workspaces`], navigationExtras);
    }

    editName() {
        const workspace = this.workspace();

        workspace.isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }

    toggleActions() {
        toggle(this.areActionsVisible);
    }

    prefetchUsers() {
        this.dataStore.prefetchUsers();
    }

    async changeOwner() {
        const workspace = this.workspace();
        const workspaceExternalId = workspace.externalId();

        if(!workspaceExternalId)
            return;

        const dialogRef = this._dialog.open(UserPickerComponent, {
            width: '700px',
            maxHeight: '80vh',
            position: {
                top: '100px'
            }   
        });

        dialogRef.afterClosed().subscribe(async (user: AppUserDetails) => {
            if(!user)
                return;   
            
            const userExternalId = user.externalId();

            if(!userExternalId)
                return;

            const originalOwner = workspace.owner();

            workspace.owner.set({
                email: signal(user.email()),
                externalId: userExternalId
            });

            this.ownerChanged.emit();
            
            try {
                await this._workspacesApi.updateOwner(
                    workspaceExternalId,
                    {
                        newOwnerExternalId: userExternalId
                    });                
            } catch (error) {
                console.error(error)
                workspace.owner.set(originalOwner);
                this.ownerChanged.emit();
            }
        });       
    }
}
