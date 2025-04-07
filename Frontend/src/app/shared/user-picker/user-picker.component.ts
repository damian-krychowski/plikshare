import { Component, computed, OnInit, Signal, signal, WritableSignal } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { UsersApi } from '../../services/users.api';
import { AppUserDetails } from '../user-item/app-user';
import { UserItemComponent } from '../user-item/user-item.component';
import { ItemSearchComponent, ItemSearchCount } from '../item-search/item-search.component';

@Component({
    selector: 'app-user-picker',
    imports: [
        MatButtonModule,
        UserItemComponent,
        ItemSearchComponent
    ],
    templateUrl: './user-picker.component.html',
    styleUrls: ['./user-picker.component.scss']
})
export class UserPickerComponent implements OnInit  {
    private _allUsers: AppUserDetails[] = [];

    users: WritableSignal<AppUserDetails[]> = signal([]);

    searchPhrase = signal('');
    searchCount: Signal<ItemSearchCount> = computed(() => ({
        allItems: this._allUsers.length,
        matchingItems: this.users().length
    }));

    constructor(
        private _usersApi: UsersApi,
        public dialogRef: MatDialogRef<UserPickerComponent>) {    
    }

    async ngOnInit(): Promise<void> {
        const users = await this._usersApi.getUsers();
        
        this._allUsers = users
            .items
            .map(user => {
                const item: AppUserDetails = {
                    externalId: signal(user.externalId),
                    email: signal(user.email),
                    isEmailConfirmed: signal(user.isEmailConfirmed),
                    workspacesCount: signal(user.workspacesCount),
                    roles: {
                        isAppOwner: signal(user.roles.isAppOwner),
                        isAdmin: signal(user.roles.isAdmin)
                    }, 
                    permissions: {
                        canAddWorkspace: signal(user.permissions.canAddWorkspace),
                        canManageGeneralSettings: signal(user.permissions.canManageGeneralSettings),
                        canManageUsers: signal(user.permissions.canManageUsers),
                        canManageStorages: signal(user.permissions.canManageStorages),
                        canManageEmailProviders: signal(user.permissions.canManageEmailProviders)
                    },
                    maxWorkspaceNumber: signal(user.maxWorkspaceNumber),
                    defaultMaxWorkspaceSizeInBytes: signal(user.defaultMaxWorkspaceSizeInBytes),
                    defaultMaxWorkspaceTeamMembers: signal(user.defaultMaxWorkspaceTeamMembers),
                    isHighlighted: signal(false)
                };

                return item;
            });

        this.users.set(this._allUsers);     
    }

    public onUserPicked(user: AppUserDetails) {
        this.dialogRef.close(user);
    }

    public onCancel() {
        this.dialogRef.close();
    }

    performSearch(query: string) {              
        this.searchPhrase.set(query);
        this.users.set(this._allUsers.filter(u => u.email().includes(query)));
    }
}
