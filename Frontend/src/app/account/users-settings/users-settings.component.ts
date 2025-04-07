import { Router } from "@angular/router";
import { Component, computed, OnInit, Signal, signal, WritableSignal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { AuthService } from "../../services/auth.service";
import { UserItemComponent } from "../../shared/user-item/user-item.component";
import { UsersApi } from "../../services/users.api";
import { MatRadioModule } from "@angular/material/radio";
import { MatDialog } from "@angular/material/dialog";
import { EmailPickerComponent } from "../../shared/email-picker/email-picker.component";
import { DataStore } from "../../services/data-store.service";
import { AppUserDetails } from "../../shared/user-item/app-user";
import { ActionButtonComponent } from "../../shared/buttons/action-btn/action-btn.component";
import { ItemButtonComponent } from "../../shared/buttons/item-btn/item-btn.component";
import { OptimisticOperation } from "../../services/optimistic-operation";
import { insertItem, removeItem } from "../../shared/signal-utils";
import { ItemSearchComponent } from "../../shared/item-search/item-search.component";

@Component({
    selector: 'app-users-settings',
    imports: [
        MatButtonModule,
        MatTooltipModule,
        UserItemComponent,
        MatRadioModule,
        ActionButtonComponent,
        ItemButtonComponent,
        ItemSearchComponent
    ],
    templateUrl: './users-settings.component.html',
    styleUrl: './users-settings.component.scss'
})
export class UsersSettingsComponent implements OnInit {       
    isLoading = signal(false);
    
    private _allUsers: WritableSignal<AppUserDetails[]> = signal([]);

    loggedInUser = computed(() => this._allUsers().find(user => user.externalId() == this.auth.userExternalId()))

    users: Signal<AppUserDetails[]> = computed(() => {
        const query = this.searchPhrase();
        
        return this
            ._allUsers()
            .filter(u => u.email().includes(query) && u.externalId() != this.auth.userExternalId());
    });

    searchPhrase = signal('');

    constructor(
        public auth: AuthService,
        private _router: Router,
        private _usersApi: UsersApi,
        private _dataStore: DataStore,
        private _dialog: MatDialog
    ) {
    }

    async ngOnInit() {
        this.isLoading.set(true);

        try {
            const loadings = [
                this.loadUsers()
            ];

            await Promise.all(loadings);
        } catch (error) {
            console.error(error);    
        } finally {
            this.isLoading.set(false);
        }
    }

    private async loadUsers() {
        const result = await this._dataStore.getUsers();

        this._allUsers.set(result.items.map(u => {
            const user: AppUserDetails = {
                externalId: signal(u.externalId),
                email: signal(u.email),
                isEmailConfirmed: signal(u.isEmailConfirmed),
                workspacesCount: signal(u.workspacesCount),
                roles: {
                    isAppOwner: signal(u.roles.isAppOwner),
                    isAdmin: signal(u.roles.isAdmin)
                }, 
                permissions: {
                    canAddWorkspace: signal(u.permissions.canAddWorkspace),
                    canManageGeneralSettings: signal(u.permissions.canManageGeneralSettings),
                    canManageUsers: signal(u.permissions.canManageUsers),
                    canManageStorages: signal(u.permissions.canManageStorages),
                    canManageEmailProviders: signal(u.permissions.canManageEmailProviders)
                },
                maxWorkspaceNumber: signal(u.maxWorkspaceNumber),
                defaultMaxWorkspaceSizeInBytes: signal(u.defaultMaxWorkspaceSizeInBytes),
                defaultMaxWorkspaceTeamMembers: signal(u.defaultMaxWorkspaceTeamMembers),

                isHighlighted: signal(false),            
            };

            return user;
        }));
    }    

    goToAccount() {
        this._router.navigate(['account']);
    }

    async onInviteUsers(){
        const dialogRef = this._dialog.open(EmailPickerComponent, {
            width: '500px',
            maxHeight: '80vh',
            position: {
                top: '100px'
            }
        });

        dialogRef.afterClosed().subscribe(
            (inviteeEmails: string[]) => this.inviteMembers(inviteeEmails));
    }

    async inviteMembers(inviteeEmails: string[]) {
        if (!inviteeEmails || inviteeEmails.length === 0)
            return;


        const newEmails = inviteeEmails
            .filter(email => !this.users().some(user => user.email() === email));

        const newUsers = newEmails.map(email => {
            const newUser: AppUserDetails = {
                email: signal(email),
                externalId: signal(null),
                isEmailConfirmed: signal(false),
                isHighlighted: signal(false),
                permissions: {
                    canAddWorkspace: signal(false),
                    canManageGeneralSettings: signal(false),
                    canManageUsers: signal(false),
                    canManageStorages: signal(false),
                    canManageEmailProviders: signal(false)
                },
                roles: {
                    isAdmin: signal(false),
                    isAppOwner: signal(false)
                },
                workspacesCount: signal(0),
                maxWorkspaceNumber: signal(null),
                defaultMaxWorkspaceSizeInBytes: signal(null),
                defaultMaxWorkspaceTeamMembers: signal(null)
            };

            return newUser;
        });
        
        this._allUsers.update(values => [...values, ...newUsers]);

        try {
            this.isLoading.set(true);

            const response = await this._usersApi.inviteUsers({
                emails: newEmails
            });

            for (const userResponse of response.users) {
                const user = newUsers
                    .find(invitation => invitation.email().toLowerCase() === userResponse.email.toLowerCase());
                
                if(user) {
                    user.externalId.set(userResponse.externalId);
                    user.maxWorkspaceNumber.set(userResponse.maxWorkspaceNumber);
                    user.defaultMaxWorkspaceSizeInBytes.set(userResponse.defaultMaxWorkspaceSizeInBytes);
                    user.defaultMaxWorkspaceTeamMembers.set(userResponse.defaultMaxWorkspaceTeamMembers)

                    user.roles.isAdmin.set(userResponse.permissionsAndRoles.isAdmin);
                    user.permissions.canAddWorkspace.set(userResponse.permissionsAndRoles.canAddWorkspace);
                    user.permissions.canManageEmailProviders.set(userResponse.permissionsAndRoles.canManageEmailProviders);
                    user.permissions.canManageGeneralSettings.set(userResponse.permissionsAndRoles.canManageGeneralSettings);
                    user.permissions.canManageStorages.set(userResponse.permissionsAndRoles.canManageStorages);
                    user.permissions.canManageUsers.set(userResponse.permissionsAndRoles.canManageUsers);
                }
            }
        } catch (error) {
            this._allUsers.update(values => values.filter(user => !newUsers.some(newUser => newUser == user)));          
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    goToUserDetails(user: AppUserDetails) {
        const externalId = user.externalId();

        if(!externalId)
            return;

        this._router.navigate([`settings/users/${externalId}`])
    }

    async onUserDeleted(operation: OptimisticOperation, user: AppUserDetails) {
        const itemRemoved = removeItem(this._allUsers, user);

        const result = await operation.wait();

        if(result.type === 'failure') {
            insertItem(this._allUsers, user, itemRemoved.index);
        }
    }
}