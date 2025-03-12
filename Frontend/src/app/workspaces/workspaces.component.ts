import { Component, OnDestroy, OnInit, WritableSignal, computed, signal } from '@angular/core';
import { WorkspacesApi } from '../services/workspaces.api';
import { NavigationEnd, NavigationExtras, Router } from '@angular/router';
import { AppWorkspace, WorkspaceItemComponent } from '../shared/workspace-item/workspace-item.component';
import { AuthService } from '../services/auth.service';
import { BoxExternalAccessApi } from '../services/box-external-access.api';
import { DataStore } from '../services/data-store.service';
import { SearchComponent, SearchSlideAnimation } from '../shared/search/search.component';
import { SearchInputComponent } from '../shared/search-input/search-input.component';
import { Subscription, filter } from 'rxjs';
import { InAppSharing } from '../services/in-app-sharing.service';
import { AppExternalBox, ExternalBoxItemComponent } from '../shared/external-box-item/external-box-item.component';
import { MatDialog } from '@angular/material/dialog';
import { SettingsMenuBtnComponent } from '../shared/setting-menu-btn/settings-menu-btn.component';
import { AppStorage } from '../shared/storage-item/storage-item.component';
import { StoragePickerComponent } from '../shared/storage-picker/storage-picker.component';
import { AppWorkspaceInvitation, WorkspaceInvitationItemComponent } from '../shared/workspace-invitation-item/workspace-invitation-item.component';
import { AppBoxInvitation, BoxInvitationItemComponent } from '../shared/box-invitation-item/box-invitation-item.component';
import { mapDtoToPermissions } from '../shared/box-permissions/box-permissions-list.component';
import { ItemButtonComponent } from '../shared/buttons/item-btn/item-btn.component';
import { ActionButtonComponent } from '../shared/buttons/action-btn/action-btn.component';
import { removeItems, unshiftItems } from '../shared/signal-utils';
import { FooterComponent } from '../static-pages/shared/footer/footer.component';


@Component({
    selector: 'app-workspaces',
    imports: [
        WorkspaceItemComponent,
        SearchInputComponent,
        SearchComponent,
        ExternalBoxItemComponent,
        SettingsMenuBtnComponent,
        WorkspaceInvitationItemComponent,
        BoxInvitationItemComponent,
        ItemButtonComponent,
        ActionButtonComponent,
        FooterComponent
    ],
    templateUrl: './workspaces.component.html',
    styleUrl: './workspaces.component.scss',
    animations: [SearchSlideAnimation]
})
export class WorkspacesComponent implements OnInit, OnDestroy {
    isLoading = signal(false);

    storages: WritableSignal<AppStorage[]> = signal([]);
    workspaces: WritableSignal<AppWorkspace[]> = signal([]);
    sharedWorkspaces: WritableSignal<AppWorkspace[]> = signal([]);
    integrationWorkspaces: WritableSignal<AppWorkspace[]> = signal([]);
    sharedBoxes: WritableSignal<AppExternalBox[]> = signal([]);
    workspaceInvitations: WritableSignal<AppWorkspaceInvitation[]> = signal([]);
    boxInvitations: WritableSignal<AppBoxInvitation[]> = signal([]);
    otherWorkspaces: WritableSignal<AppWorkspace[]> = signal([]);

    hasAnyStorage = computed(() => this.storages().length > 0);
    hasAnyWorkspace = computed(() => this.workspaces().length > 0);
    hasAnySharedWorkspace = computed(() => this.sharedWorkspaces().length > 0);
    hasAnyIntegrationWorkspace = computed(() => this.integrationWorkspaces().length > 0);
    hasAnySharedBox = computed(() => this.sharedBoxes().length > 0);
    hasAnyWorkspaceInvitation = computed(() => this.workspaceInvitations().length > 0);
    hasAnyBoxInvitation = computed(() => this.boxInvitations().length > 0);
    hasAnyInvitation = computed(() => this.hasAnyWorkspaceInvitation() || this.hasAnyBoxInvitation());
    hasAnyOtherWorkspace = computed(() => this.otherWorkspaces().length > 0);

    canAddWorkspace = computed(() => this.auth.canAddWorkspace() && this.hasAnyStorage());
    
    showWorkspacesSection = computed(() => this.hasAnyWorkspace() || this.canAddWorkspace() );

    isAnyNameEditing = computed(() => 
        this.workspaces().some((b) => b.isNameEditing()) 
        || this.sharedWorkspaces().some((b) => b.isNameEditing())
        || this.otherWorkspaces().some((b) => b.isNameEditing())
        || this.integrationWorkspaces().some((b) => b.isNameEditing()));

    private _routerSubscription: Subscription | null = null;

    constructor(
        public auth: AuthService,
        private _router: Router,
        private _workspacesApi: WorkspacesApi,
        private _boxExternalAccessApi: BoxExternalAccessApi,
        private _inAppSharing: InAppSharing,
        private _dataStore: DataStore,
        private _dialog: MatDialog
    ) { 
    }

    async ngOnInit() {
        await this.loadDashboard();
        await this.tryConsumeNavigationState();

        this._routerSubscription = this._router.events
            .pipe(filter(event => event instanceof NavigationEnd))
            .subscribe(() => {
                this.tryConsumeNavigationState();
            });
    }

    ngOnDestroy(): void {
        this._routerSubscription?.unsubscribe();
    }

    private async tryConsumeNavigationState() {
        const navigation = this._router.lastSuccessfulNavigation;

        if(!navigation?.extras.state)
            return;

        this.tryHighlightWorkspace(navigation.extras);  
        this.tryHighlightExternalBox(navigation.extras);   
    }

    private tryHighlightWorkspace(extras: NavigationExtras) {
        if(!extras.state)
            return;

        const workspaceToHighlight = extras.state['workspaceToHighlight'] as string;

        const workspaceExternalId = this
            ._inAppSharing
            .pop(workspaceToHighlight);

        if(!workspaceExternalId)
            return;

        const workspace = this
            .workspaces()
            .find(w => w.externalId() === workspaceExternalId);

        if(workspace) {
            workspace.isHighlighted.set(true);
        }

        const sharedWorkspace = this
            .sharedWorkspaces()
            .find(w => w.externalId() === workspaceExternalId);

        if(sharedWorkspace) {
            sharedWorkspace.isHighlighted.set(true);
        }

        const integrationWorkspace = this
            .integrationWorkspaces()
            .find(w => w.externalId() === workspaceExternalId);

        if(integrationWorkspace) {
            integrationWorkspace.isHighlighted.set(true);
        }
    }

    private tryHighlightExternalBox(extras: NavigationExtras) {
        if(!extras.state)
            return;

        const externalBoxToHighlight = extras.state['externalBoxToHighlight'] as string;

        const boxExternalId = this
            ._inAppSharing
            .pop(externalBoxToHighlight);

        if(!boxExternalId)
            return;

        const externalBox = this
            .sharedBoxes()
            .find(b => b.boxExternalId() === boxExternalId);

        if(externalBox) {
            externalBox.isHighlighted.set(true);
        }
    }

    private async loadDashboard() {
        try {
            this.isLoading.set(true);

            const response = await this._dataStore.getDashboardData();
            const userEmail = await this.auth.getUserEmail();

            const allWorkspaces = response.workspaces.map((item) => {
                const isOwnedByUser = item.owner.email.toLowerCase() === userEmail.toLowerCase();

                const workspace: AppWorkspace = {
                    type: 'app-workspace',
                    externalId: signal(item.externalId),
                    name: signal(item.name),
                    currentSizeInBytes: signal(item.currentSizeInBytes),
                    owner: signal({
                        email: signal(item.owner.email),
                        externalId: item.owner.externalId
                    }),
                    wasUserInvited: signal(!isOwnedByUser),
                    isUsedByIntegration: item.isUsedByIntegration,
                    isBucketCreated: signal(item.isBucketCreated),
                    permissions: {
                        allowShare: item.permissions.allowShare
                    },
                    storageName: signal(item.storageName),
                    isNameEditing: signal(false),
                    isHighlighted: signal(false)
                };

                return workspace;
            });

            this.workspaces.set(allWorkspaces
                .filter(w => !w.isUsedByIntegration)
                .filter(w => w.owner().externalId == this.auth.userExternalId())
            );

            this.sharedWorkspaces.set(allWorkspaces                
                .filter(w => !w.isUsedByIntegration)
                .filter(w => w.owner().externalId != this.auth.userExternalId())
            );

            this.integrationWorkspaces.set(allWorkspaces
                .filter(w => w.isUsedByIntegration)
            );

            this.otherWorkspaces.set(response.otherWorkspaces.map((item) => ({
                type: 'app-workspace',
                externalId: signal(item.externalId),
                name: signal(item.name),
                currentSizeInBytes: signal(item.currentSizeInBytes),
                owner: signal({
                    email: signal(item.owner.email),
                    externalId: item.owner.externalId
                }),
                wasUserInvited: signal(false),
                isUsedByIntegration: item.isUsedByIntegration,
                isBucketCreated: signal(item.isBucketCreated),
                permissions: {
                    allowShare: item.permissions.allowShare,
                },
                storageName: signal(item.storageName),
                isNameEditing: signal(false),
                isHighlighted: signal(false)
            })));

            this.sharedBoxes.set(response.boxes.map((item) => ({
                type: 'app-external-box',
                boxExternalId: signal(item.boxExternalId),
                boxName: signal(item.boxName),
                owner: signal({
                    email: signal(item.owner.email),
                    externalId: item.owner.externalId
                }),
                isHighlighted: signal(false),
                permissions: signal(
                    mapDtoToPermissions(item.permissions)
                ),
                workspace: signal(undefined)
            })));

            this.workspaceInvitations.set(response.workspaceInvitations.map((item) => ({
                type: 'app-workspace-invitation',
                externalId: signal(item.workspaceExternalId),
                name: item.workspaceName,
                isUsedByIntegration: item.isUsedByIntegration,
                isBucketCreated: item.isBucketCreated,
                owner: {
                    email: signal(item.owner.email),
                    externalId: item.owner.externalId
                },
                inviter: {
                    email: signal(item.inviter.email),
                    externalId: item.inviter.externalId
                },                
                permissions: item.permissions,
            })));

            this.boxInvitations.set(response.boxInvitations.map((item) => ({
                type: 'app-box-invitation',
                boxExternalId: signal(item.boxExternalId),
                boxName: signal(item.boxName),
                owner: signal({
                    email: signal(item.owner.email),
                    externalId: item.owner.externalId
                }),
                inviter: signal({
                    email: signal(item.inviter.email),
                    externalId: item.inviter.externalId
                }),
                permissions: signal(
                    mapDtoToPermissions(item.permissions)
                ),
                workspace: signal(undefined)
            })));

            this.storages.set(response.storages.map((item)=> ({
                externalId: item.externalId,
                name: signal(item.name),
                type: item.type,
                details: null,
                encryptionType: item.encryptionType,
                workspacesCount: item.workspacesCount ?? 0,
                isHighlighted: signal(false),
                isNameEditing: signal(false)
            })));
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }
   
    async createNewWorkspace() {
        const storages = this.storages();

        if(storages.length > 1) {
            const dialogRef = this._dialog.open(StoragePickerComponent, {
                width: '500px',
                data: {
                    storages: this.storages()
                },
                position: {
                    top: '100px'
                }
            });
    
            dialogRef
                .afterClosed()
                .subscribe(async (storage: AppStorage) => await this.onStoragePicked(storage));       
        } else {
            await this.onStoragePicked(storages[0]);
        }
    }

    private async onStoragePicked(storage: AppStorage){
        if(!storage)
            return;

        try {
            this.isLoading.set(true);

            const workspace: AppWorkspace = {
                type: 'app-workspace',
                externalId: signal(null),
                name: signal('Untitled workspace'),
                owner: signal(await this.auth.getUser()),
                currentSizeInBytes: signal(0),
                wasUserInvited: signal(false),
                isUsedByIntegration: false,
                isBucketCreated: signal(false),
                permissions: {
                    allowShare: true
                },
                storageName: signal(storage.name()),
                isNameEditing: signal(true),
                isHighlighted: signal(false)
            };

            this.workspaces.update(values => [...values,workspace]);

            const response = await this._workspacesApi.createWorkspace({
                storageExternalId: storage.externalId,
                name: 'Untitled workspace'
            });

            workspace.externalId.set(response.externalId);
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    async acceptWorkspaceInvitation(invitation: AppWorkspaceInvitation) {
        try {
            this.isLoading.set(true);

            const workspace: AppWorkspace = {
                type: 'app-workspace',
                externalId: invitation.externalId,
                name: signal(invitation.name),
                currentSizeInBytes: signal(0),
                owner: signal(invitation.owner), 
                wasUserInvited: signal(true),
                permissions: invitation.permissions,
                isUsedByIntegration: invitation.isUsedByIntegration,
                isBucketCreated: signal(invitation.isBucketCreated),
                storageName: signal(null),
                isNameEditing: signal(false),
                isHighlighted: signal(false)
            }

            if(invitation.isUsedByIntegration) {
                this.integrationWorkspaces.update(values => [workspace, ...values]);
            } else {
                this.sharedWorkspaces.update(values => [workspace, ...values]);
            }

            this.workspaceInvitations.update(values => values.filter(i => i.externalId() !== invitation.externalId()));

            const response = await this._workspacesApi.acceptWorkspaceInvitation(
                invitation.externalId()
            );

            workspace.currentSizeInBytes.set(response.workspaceCurrentSizeInBytes);
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    async rejectWorkspaceInvitation(invitation: AppWorkspaceInvitation) {
        try {
            this.isLoading.set(true);

            this.workspaceInvitations.update(values => values.filter(i => i.externalId() !== invitation.externalId()));

            await this._workspacesApi.rejectWorkspaceInvitation(
                invitation.externalId()
            );
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    onWorkspaceDelete(workspace: AppWorkspace) {
        this.workspaces.update(values => values.filter(w => w.externalId() !== workspace.externalId()));
    }

    onSharedWorkspaceLeft(workspace: AppWorkspace) {
        this.sharedWorkspaces.update(values => values.filter(w => w.externalId() !== workspace.externalId()));
    }

    onSharedWorkspaceDelete(workspace: AppWorkspace) {
        this.sharedWorkspaces.update(values => values.filter(w => w.externalId() !== workspace.externalId()));
    }

    onIntegrationWorkspaceLeft(workspace: AppWorkspace) {
        this.integrationWorkspaces.update(values => values.filter(w => w.externalId() !== workspace.externalId()));
    }

    onIntegrationWorkspaceDelete(workspace: AppWorkspace) {
        this.integrationWorkspaces.update(values => values.filter(w => w.externalId() !== workspace.externalId()));
    }

    onOtherWorkspaceDelete(workspace: AppWorkspace) {
        this.otherWorkspaces.update(values => values.filter(w => w.externalId() !== workspace.externalId()));
    }

    onExternalBoxLeft(box: AppExternalBox) {
        this.sharedBoxes.update(values => values.filter(b => b.boxExternalId() !== box.boxExternalId()));
    }

    onWorkspaceOwnerChange(workspace: AppWorkspace) {
        removeItems(this.workspaces, workspace);
        removeItems(this.otherWorkspaces, workspace);
        removeItems(this.sharedWorkspaces, workspace);
        removeItems(this.integrationWorkspaces, workspace);

        const owner = workspace.owner();

        if(owner.externalId == this.auth.userExternalId()) {
            unshiftItems(this.workspaces, workspace);
        } else if(this.auth.isAdmin()) {
            unshiftItems(this.otherWorkspaces, workspace);
        }
    }

    async acceptBoxInvitation(invitation: AppBoxInvitation) {
        try {
            this.isLoading.set(true);

            const box: AppExternalBox = {
                type: 'app-external-box',
                boxExternalId: invitation.boxExternalId,
                boxName: invitation.boxName,
                owner: invitation.owner,
                isHighlighted: signal(false),
                permissions: invitation.permissions,
                workspace: invitation.workspace
            }

            this.sharedBoxes.update(values => [box, ...values]);
            this.boxInvitations.update(values => values.filter(i => i.boxExternalId() !== invitation.boxExternalId()));

            await this._boxExternalAccessApi.acceptBoxInvitation(
                invitation.boxExternalId()
            );
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    async rejectBoxInvitation(invitation: AppBoxInvitation) {
        try {
            this.isLoading.set(true);

            this.boxInvitations.update(values => values.filter(i => i.boxExternalId() !== invitation.boxExternalId()));

            await this._boxExternalAccessApi.rejectBoxInvitation(
                invitation.boxExternalId()
            );
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }
}
