import { ActivatedRoute, Router } from "@angular/router";
import { Component, computed, OnDestroy, OnInit, signal, WritableSignal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { AuthService } from "../../services/auth.service";
import { PrefetchDirective } from "../../shared/prefetch.directive";
import { ApplicationSingUp } from "../../services/general-settings.api";
import { MatRadioModule } from "@angular/material/radio";
import { DataStore } from "../../services/data-store.service";
import { Subscription } from "rxjs";
import { UserPermissionsListComponent } from "../../shared/user-permissions/user-permissions-list.component";
import { ConfirmOperationDirective } from "../../shared/operation-confirm/confirm-operation.directive";
import { AppExternalBox, ExternalBoxItemComponent } from "../../shared/external-box-item/external-box-item.component";
import { AppWorkspace, WorkspaceItemComponent } from "../../shared/workspace-item/workspace-item.component";
import { AppUserDetails } from "../../shared/user-item/app-user";
import { AppBoxInvitation, BoxInvitationItemComponent } from "../../shared/box-invitation-item/box-invitation-item.component";
import { AppWorkspaceInvitation, WorkspaceInvitationItemComponent } from "../../shared/workspace-invitation-item/workspace-invitation-item.component";
import { BoxesSetApi } from "../../services/boxes.api";
import { mapDtoToPermissions, mapPermissionsToDto } from "../../shared/box-permissions/box-permissions-list.component";
import { WorkspacesApi } from "../../services/workspaces.api";
import { ActionButtonComponent } from "../../shared/buttons/action-btn/action-btn.component";
import { pushItems, removeItems } from "../../shared/signal-utils";
import { UsersApi } from "../../services/users.api";



@Component({
    selector: 'app-user-details',
    imports: [
        MatButtonModule,
        MatTooltipModule,
        PrefetchDirective,
        MatRadioModule,
        UserPermissionsListComponent,
        ConfirmOperationDirective,
        WorkspaceItemComponent,
        ExternalBoxItemComponent,
        WorkspaceInvitationItemComponent,
        BoxInvitationItemComponent,
        ActionButtonComponent
    ],
    templateUrl: './user-details.component.html',
    styleUrl: './user-details.component.scss'
})
export class UserDetailsComponent implements OnInit, OnDestroy {       
    isLoading = signal(false);

    applicationSignUp: WritableSignal<ApplicationSingUp> = signal('only-invited-users');

    user: WritableSignal<AppUserDetails | null> = signal(null);
    workspaces: WritableSignal<AppWorkspace[]> = signal([]);
    sharedWorkspaces: WritableSignal<AppWorkspace[]> = signal([]);
    sharedBoxes: WritableSignal<AppExternalBox[]> = signal([]);
    workspaceInvitations: WritableSignal<AppWorkspaceInvitation[]> = signal([]);
    boxInvitations: WritableSignal<AppBoxInvitation[]> = signal([]);

    isUserLoaded = computed(() => this.user() != null);
    workspacesCount = computed(() => this.user()?.workspacesCount() ?? 0);
    email = computed(() => this.user()?.email());
    isEmailConfirmed = computed(() => this.user()?.isEmailConfirmed() ?? false);
    isAppOwner = computed(() => this.user()?.roles.isAppOwner() ?? false);
    isAdmin = computed(() => this.user()?.roles.isAdmin() ?? false);
    isLoggedInUser = computed(() => this.user()?.externalId() == this.auth.userExternalId());

    hasAnyWorkspaces = computed(() => this.workspaces().length > 0);
    hasAnySharedWorkspace = computed(() => this.sharedWorkspaces().length > 0);
    hasAnyWorkspaceInvitation = computed(() => this.workspaceInvitations().length > 0);
    hasAnySharedBox = computed(() => this.sharedBoxes().length > 0);
    hasAnyBoxInvitation = computed(() => this.boxInvitations().length > 0);
    
    canBeDeleted = computed(() => 
        !this.isAppOwner() 
        && !this.isLoggedInUser() 
        && this.auth.canManageUsers()
        && ((this.isAdmin() && this.auth.isAppOwner()) || !this.isAdmin()));

    isAnyNameEditing = computed(() => this.workspaces().some((b) => b.isNameEditing()) || this.sharedWorkspaces().some((b) => b.isNameEditing()));

    private _userExternalId: string | null = null;
    private _subscription: Subscription | null = null;

    constructor(
        public dataStore: DataStore,
        public auth: AuthService,
        private _router: Router,
        private _activatedRoute: ActivatedRoute,
        private _boxesApi: BoxesSetApi,
        private _workspaceApi: WorkspacesApi,
        private _usersApi: UsersApi
    ) {
    }

    async ngOnInit() {
        this._subscription = this._activatedRoute.params.subscribe(async (params) => {
            this._userExternalId = params['userExternalId'] || null;
            
            await this.loadUserDetails();
        });
    }

    ngOnDestroy(): void {
        this._subscription?.unsubscribe();
    }

    private async loadUserDetails() {
        if(!this._userExternalId)
            return;

        this.isLoading.set(true);

        try {
            const userDetails = await this.dataStore.getUserDetails(
                this._userExternalId);

            this.workspaces.set(userDetails
                .workspaces
                .map(w => {
                    const workspace: AppWorkspace = {
                        type: 'app-workspace',
                        externalId: signal(w.externalId),
                        currentSizeInBytes: signal(w.currentSizeInBytes),
                        maxSizeInBytes: w.maxSizeInBytes,
                        isHighlighted: signal(false),
                        isNameEditing: signal(false),
                        name: signal(w.name),
                        owner: signal({
                            email: signal(userDetails.user.email),
                            externalId: userDetails.user.externalId
                        }),
                        permissions: {
                            allowShare: true
                        },
                        storageName: signal(w.storageName),
                        wasUserInvited: signal(false),
                        isUsedByIntegration: w.isUsedByIntegration,
                        isBucketCreated: signal(w.isBucketCreated)
                    };

                    return workspace;
                }));
            
            this.sharedWorkspaces.set(userDetails
                .sharedWorkspaces
                .filter(w => w.wasInvitationAccepted)
                .map(w => {
                    const workspace: AppWorkspace = {
                        type: 'app-workspace',
                        externalId: signal(w.externalId),
                        currentSizeInBytes: signal(w.currentSizeInBytes),
                        maxSizeInBytes: w.maxSizeInBytes,
                        isHighlighted: signal(false),
                        isNameEditing: signal(false),
                        name: signal(w.name),
                        owner: signal({
                            email: signal(w.owner.email),
                            externalId: w.owner.externalId
                        }),
                        permissions: {
                            allowShare: w.permissions.allowShare
                        },
                        storageName: signal(w.storageName),
                        wasUserInvited: signal(true),
                        isUsedByIntegration: w.isUsedByIntegration,
                        isBucketCreated: signal(w.isBucketCreated)
                    };

                    return workspace;
                }));

            this.workspaceInvitations.set(userDetails
                .sharedWorkspaces
                .filter(w => !w.wasInvitationAccepted)
                .map(w => {
                    const invitation: AppWorkspaceInvitation = {
                        type: 'app-workspace-invitation',
                        externalId: signal(w.externalId),
                        name: w.name,
                        owner: {
                            email: signal(w.owner.email),
                            externalId: w.owner.externalId
                        },
                        inviter: {
                            email: signal(w.inviter.email),
                            externalId: w.inviter.externalId
                        },
                        permissions: {
                            allowShare: w.permissions.allowShare
                        },         
                        isUsedByIntegration: w.isUsedByIntegration,
                        isBucketCreated: w.isBucketCreated
                    };

                    return invitation;
                }));

            this.sharedBoxes.set(userDetails
                .sharedBoxes
                .filter(b => b.wasInvitationAccepted)
                .map(b => {
                    const box: AppExternalBox = {
                        type: 'app-external-box',
                        boxExternalId: signal(b.boxExternalId),
                        boxName: signal(b.boxName),
                        isHighlighted: signal(false),
                        owner: signal({
                            email: signal(b.owner.email),
                            externalId: b.owner.externalId
                        }),                        
                        permissions: signal(
                            mapDtoToPermissions(b.permissions)),
                        workspace: signal({
                            externalId: b.workspaceExternalId,
                            name: signal(b.workspaceName),
                            storageName: signal(b.storageName)
                        })      
                    };

                    return box;
                }));

            this.boxInvitations.set(userDetails
                .sharedBoxes
                .filter(b => !b.wasInvitationAccepted)
                .map(b => {
                    const box: AppBoxInvitation = {
                        type: 'app-box-invitation',
                        boxExternalId: signal(b.boxExternalId),
                        boxName: signal(b.boxName),
                        owner: signal({
                            email: signal(b.owner.email),
                            externalId: b.owner.externalId
                        }),  
                        inviter: signal({
                            email: signal(b.inviter.email),
                            externalId: b.inviter.externalId
                        }),
                        permissions: signal(
                            mapDtoToPermissions(b.permissions)),
                        workspace: signal({
                            externalId: b.workspaceExternalId,
                            name: signal(b.workspaceName),
                            storageName: signal(b.storageName)           
                        })
                    };

                    return box;
                }));

            this.user.set({
                externalId: signal(userDetails.user.externalId),
                email: signal(userDetails.user.email),
                isEmailConfirmed: signal(userDetails.user.isEmailConfirmed),
                isHighlighted: signal(false),
                permissions: {
                    canAddWorkspace: signal(userDetails.user.permissions.canAddWorkspace),
                    canManageGeneralSettings: signal(userDetails.user.permissions.canManageGeneralSettings),
                    canManageUsers: signal(userDetails.user.permissions.canManageUsers),
                    canManageStorages: signal(userDetails.user.permissions.canManageStorages),
                    canManageEmailProviders: signal(userDetails.user.permissions.canManageEmailProviders)
                },
                roles: {
                    isAdmin: signal(userDetails.user.roles.isAdmin),
                    isAppOwner: signal(userDetails.user.roles.isAppOwner)
                },
                //that value is computed based on workspaces collection to catch situation when worksapce
                //is deleted or added on that view directly
                workspacesCount: computed(() => this.workspaces().length)
            });
        } catch (error) {
            console.error(error);    
        } finally {
            this.isLoading.set(false);
        }
    }

    goToUsers() {
        this._router.navigate(['/settings/users']);
    }

    onWorkspaceDelete(workspace: AppWorkspace) {
        this.workspaces.update(values => values.filter(w => w.externalId() !== workspace.externalId()));
    }

    onSharedWorkspaceDelete(workspace: AppWorkspace) {
        this.sharedWorkspaces.update(values => values.filter(w => w.externalId() !== workspace.externalId()));
    }

    async onBoxPermissionsChange(box: AppExternalBox | AppBoxInvitation) {
        if(!this._userExternalId)
            return;

        await this._boxesApi.updateBoxMemberPermissions(
            box.workspace()!.externalId, 
            box.boxExternalId(), 
            this._userExternalId, 
            mapPermissionsToDto(box.permissions()));      
    }

    async onWorkspaceAccessRevoked(workspace: AppWorkspace | AppWorkspaceInvitation) {
        if(!this._userExternalId)
            return;

        const workspaceExternalId = workspace.externalId()

        if(!workspaceExternalId)
            return;

        const index = this.removeFromList(workspace);

        try {
            this.isLoading.set(true);

            await this._workspaceApi.revokeWorkspaceMember(
                workspaceExternalId,
                this._userExternalId);
        } catch (error) {
            console.error(error);
            
            this.addBackToList(workspace, index);
        } finally {
            this.isLoading.set(false);
        }
    }

    onWorkspaceOwnerChanged(workspace: AppWorkspace) {
        if(!this._userExternalId)
            return;

        if(workspace.owner().externalId != this._userExternalId) {
            removeItems(this.workspaces, workspace);
        }
    }

    onSharedWorkspaceOwnerChanged(workspace: AppWorkspace) {
        if(!this._userExternalId)
            return;

        removeItems(this.sharedWorkspaces, workspace);

        if(workspace.owner().externalId === this._userExternalId) {
            pushItems(this.workspaces, workspace);
        }
    } 


    async onBoxAccessRevoked(box: AppExternalBox | AppBoxInvitation) {
        if(!this._userExternalId)
            return;

        if(!box.workspace())
            return;

        const index = this.removeFromList(box);

        try {
            this.isLoading.set(true);

            await this._boxesApi.revokeMember(
                box.workspace()!.externalId,
                box.boxExternalId(),
                this._userExternalId);
        } catch (error) {
            console.error(error);
            
            this.addBackToList(box,index);
        } finally {
            this.isLoading.set(false);
        }
    }

    private removeFromList(item: AppExternalBox | AppBoxInvitation | AppWorkspace | AppWorkspaceInvitation) {
        if(item.type === 'app-external-box') {
            const index = this.sharedBoxes().indexOf(item);
            this.sharedBoxes.update(values => values.filter(i => i.boxExternalId() !== item.boxExternalId()));

            return index;
        } else if(item.type === 'app-box-invitation') {
            const index = this.boxInvitations().indexOf(item);
            this.boxInvitations.update(values => values.filter(i => i.boxExternalId() !== item.boxExternalId()));

            return index;
        } else if(item.type === 'app-workspace') {
            const index = this.sharedWorkspaces().indexOf(item);
            this.sharedWorkspaces.update(values => values.filter(i => i.externalId() !== item.externalId()));

            return index;
        }  else if(item.type === 'app-workspace-invitation') {
            const index = this.workspaceInvitations().indexOf(item);
            this.workspaceInvitations.update(values => values.filter(i => i.externalId() !== item.externalId()));

            return index;
        } else {
            throw new Error("Unkown item type");
        }
    }

    private addBackToList(item: AppExternalBox | AppBoxInvitation | AppWorkspace | AppWorkspaceInvitation, index: number) {
        if(item.type === 'app-external-box') {
            this.sharedBoxes.update(values => values.splice(index, 0, item));
        } else if(item.type === 'app-box-invitation') {
            this.boxInvitations.update(values => values.splice(index, 0, item));
        } else if(item.type === 'app-workspace') {
            this.sharedWorkspaces.update(values => values.splice(index, 0, item));
        } else if(item.type === 'app-workspace-invitation') {
            this.workspaceInvitations.update(values => values.splice(index, 0, item));
        } else {
            throw new Error("Unkown item type");
        }
    }

    async deleteUser() {
        if(!this._userExternalId)
            return;

        try {
            await this._usersApi.deleteUser(
                this._userExternalId);

            this.goToUsers();
        } catch (error) {
            console.error(error);
        }
    }
}