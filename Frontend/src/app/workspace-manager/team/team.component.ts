import { Component, OnDestroy, OnInit, WritableSignal, computed, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { WorkspacesApi } from '../../services/workspaces.api';
import { EmailPickerComponent } from '../../shared/email-picker/email-picker.component';
import { MatDialog } from '@angular/material/dialog';
import { AuthService } from '../../services/auth.service';
import { Subscription, filter } from 'rxjs';
import { InAppSharing } from '../../services/in-app-sharing.service';
import { DataStore } from '../../services/data-store.service';
import { insertItem, pushItems, removeItem, removeItems } from '../../shared/signal-utils';
import { AppWorkspaceTeamMember, WorkspaceTeamMemberComponent } from './workspace-team-member/workspace-team-member.component';
import { AppWorkspaceTeamInvitation, WorkspaceTeamInvitationComponent } from './workspace-team-invitation/workspace-team-invitation.component';
import { ItemButtonComponent } from '../../shared/buttons/item-btn/item-btn.component';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { WorkspaceContextService } from '../workspace-context.service';
import { GenericDialogService } from '../../shared/generic-message-dialog/generic-dialog-service';


@Component({
    selector: 'app-team',
    imports: [
        WorkspaceTeamMemberComponent,
        WorkspaceTeamInvitationComponent,
        ItemButtonComponent,
        ActionButtonComponent
    ],
    templateUrl: './team.component.html',
    styleUrl: './team.component.scss'
})
export class TeamComponent implements OnInit, OnDestroy {
    isLoading = signal(false);

    workspaceInvitations: WritableSignal<AppWorkspaceTeamInvitation[]> = signal([]);
    workspaceMembers: WritableSignal<AppWorkspaceTeamMember[]> = signal([]);

    hasAnyInvitation = computed(() => this.workspaceInvitations().length > 0);
    canInviteMoreMembers = computed(() => {
        const workspace = this.context.workspace();

        if(!workspace)
            return false;

        if(workspace.maxTeamMembers == null)
            return true;

        const totalTeamMembersCount = workspace.currentBoxesTeamMembersCount + workspace.currentTeamMembersCount;
        
        const left = workspace.maxTeamMembers - totalTeamMembersCount;

        return left > 0;
    });

    private _currentWorkspaceExternalId: string | null = null;
    private _routerSubscription: Subscription | null = null;

    constructor(
        private _auth: AuthService,
        private _workspaceApi: WorkspacesApi,
        private _activatedRoute: ActivatedRoute,
        private _dialog: MatDialog,
        private _router: Router,
        private _inAppSharing: InAppSharing,
        private _dataStore: DataStore,
        private _genericDialogService: GenericDialogService,
        public context: WorkspaceContextService) 
        { }

    async ngOnInit() {
        this.load();
                
        this._routerSubscription = this._router.events
            .pipe(filter(event => event instanceof NavigationEnd))
            .subscribe(() => this.load());
    }

    private async load() {
        const workspaceExternalId = this._activatedRoute.parent?.snapshot.params['workspaceExternalId'];

        if (!workspaceExternalId)
            throw new Error('workspaceExternalId is missing');

        await this.loadTeamIfNeeded(workspaceExternalId);
        await this.tryConsumeNavigationState();
    }

    private async tryConsumeNavigationState() {
        const navigation = this._router.lastSuccessfulNavigation;

        if(!navigation?.extras.state)
            return;

        const emailsToInvite = navigation
            .extras
            .state['emailsToInvite'] as string;

        if(!emailsToInvite)
            return;

        const emails = this._inAppSharing.pop(emailsToInvite) as string[];

        if(!emails)
            return;

        await this.inviteMembers(emails);
    }

    ngOnDestroy(): void {
        this._routerSubscription?.unsubscribe();
    }

    private async loadTeamIfNeeded(workspaceExternalId: string) {
        if(this._currentWorkspaceExternalId === workspaceExternalId)
            return;

        this._currentWorkspaceExternalId = workspaceExternalId;

        try {
            this.isLoading.set(true);

            const response = await this._dataStore.getWorkspaceMemberList(
                workspaceExternalId);

            this.workspaceInvitations.set(response
                .items
                .filter(member => !member.wasInvitationAccepted)
                .map(item => {
                    const invitation: AppWorkspaceTeamInvitation = {
                        memberExternalId: signal(item.memberExternalId),
                        inviterEmail: signal(item.inviterEmail),
                        email: signal(item.memberEmail)
                    };

                    return invitation;
                }));

            this.workspaceMembers.set(response
                .items
                .filter(member => member.wasInvitationAccepted)
                .map(item => {
                    const member: AppWorkspaceTeamMember = {
                        memberExternalId: signal(item.memberExternalId),
                        email: signal(item.memberEmail),
                        permissions: {
                            allowShare: signal(item.permissions.allowShare)
                        }
                    };

                    return member;
                }));

        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    async cancelInvitation(invitation: AppWorkspaceTeamInvitation) {
        const memberExternalId = invitation.memberExternalId();

        if (!memberExternalId)
            return;

        const deletedItem = removeItem(
            this.workspaceInvitations, 
            invitation);

        try {
            this.isLoading.set(true);

            await this._workspaceApi.revokeWorkspaceMember(
                this._currentWorkspaceExternalId!,
                memberExternalId);
                
            await this.refreshWorkspaceContext();
        } catch (error) {
            console.error(error);

            insertItem(
                this.workspaceInvitations, 
                invitation, 
                deletedItem.index);
        } finally {
            this.isLoading.set(false);
        }
    }

    async revokeMember(member: AppWorkspaceTeamMember) {
        const deletedItem = removeItem(
            this.workspaceMembers, 
            member);

        try {
            this.isLoading.set(true);

            await this._workspaceApi.revokeWorkspaceMember(
                this._currentWorkspaceExternalId!,
                member.memberExternalId());

            await this.refreshWorkspaceContext();
        } catch (error) {
            console.error(error);

            insertItem(
                this.workspaceMembers, 
                member, 
                deletedItem.index);
        } finally {
            this.isLoading.set(false);
        }
    }

    async createInvitation() {
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

        const inviterEmail = await this._auth.getUserEmail();

        const newEmails = inviteeEmails
            .filter(email => !this.workspaceInvitations().some(invitation => invitation.email() === email))
            .filter(email => !this.workspaceMembers().some(member => member.email() === email));

        const invitations: AppWorkspaceTeamInvitation[] = newEmails.map(email => ({
            memberExternalId: signal(null),
            inviterEmail: signal(inviterEmail),
            email: signal(email)
        }));        

        pushItems(this.workspaceInvitations, ...invitations);

        try {
            this.isLoading.set(true);

            const response = await this._workspaceApi.createMemberInvitation(
                this._currentWorkspaceExternalId!, {
                memberEmails: inviteeEmails
            });

            await this.refreshWorkspaceContext();

            for (const newMember of response.members) {
                const newInvitation = invitations
                    .find(invitation => invitation.email().toLowerCase() === newMember.email.toLowerCase());
                
                if(newInvitation) {
                    newInvitation.memberExternalId.set(newMember.externalId);
                }
            }
        } catch (error:any) {
            removeItems(this.workspaceInvitations, ...invitations);

            if(error?.error?.code === 'max-team-members-exceeded') {
                this._genericDialogService.openMaxTeamMembersReachedDialog();
            } else {
                console.error(error);
            }

        } finally {
            this.isLoading.set(false);
        }
    }

    private async refreshWorkspaceContext() {
        const workspace = await this
            ._workspaceApi
            .getWorkspace(this._currentWorkspaceExternalId!);

        this.context.workspace.set(workspace); 
    }
}
