import { Component, OnDestroy, OnInit, WritableSignal, computed, signal } from '@angular/core';
import { BoxPermissions, BoxesSetApi } from '../../../services/boxes.api';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { FolderPickerComponent } from '../folder-picker/folder-picker.component';
import { FilesExplorerApi, FilesExplorerComponent } from '../../../files-explorer/files-explorer.component';
import { FoldersAndFilesGetApi, FoldersAndFilesSetApi, GetFolderResponse } from '../../../services/folders-and-files.api';
import { EmailPickerComponent } from '../../../shared/email-picker/email-picker.component';
import { AuthService } from '../../../services/auth.service';
import { Subscription, filter } from 'rxjs';
import { AppBoxPermissions, mapPermissionsToDto } from '../../../shared/box-permissions/box-permissions-list.component';
import { EditableTxtComponent } from '../../../shared/editable-txt/editable-txt.component';
import { toggle } from '../../../shared/signal-utils';
import { ConfirmOperationDirective } from '../../../shared/operation-confirm/confirm-operation.directive';
import { DataStore } from '../../../services/data-store.service';
import { PrefetchDirective } from '../../../shared/prefetch.directive';
import { WorkspaceFilesExplorerApi } from '../../../services/workspace-files-explorer-api';
import { AppBoxLink, BoxLinkItemComponent } from './box-link-item/box-link-item.component';
import { AppFolderItem } from '../../../shared/folder-item/folder-item.component';
import { AppBoxTeamMember, BoxTeamMemberComponent } from './box-team-member/box-team-member.component';
import { AppBoxTeamInvitation, BoxTeamInvitationComponent } from './box-team-invitation/box-team-invitation.component';
import { ItemButtonComponent } from '../../../shared/buttons/item-btn/item-btn.component';
import { ActionButtonComponent } from '../../../shared/buttons/action-btn/action-btn.component';
import { AppBoxRichTextItem, BoxRichTextEditorComponent } from './box-rich-text-editor/box-rich-text-editor.component';
import { WorkspacesApi } from '../../../services/workspaces.api';
import { GenericDialogService } from '../../../shared/generic-message-dialog/generic-dialog-service';
import { FileLockService } from '../../../services/file-lock.service';

type BoxFolder = {
    externalId: string;
    name: string;
    ancestors: {
        externalId: string;
        name: string;
    }[];
}

@Component({
    selector: 'app-box-details',
    imports: [
        MatSlideToggleModule,
        MatButtonModule,
        MatTooltipModule,
        FilesExplorerComponent,
        EditableTxtComponent,
        ConfirmOperationDirective,
        PrefetchDirective,
        BoxLinkItemComponent,
        BoxTeamMemberComponent,
        BoxTeamInvitationComponent,
        ItemButtonComponent,
        ActionButtonComponent,
        BoxRichTextEditorComponent
    ],
    templateUrl: './box-details.component.html',
    styleUrl: './box-details.component.scss'
})
export class BoxDetailsComponent implements OnInit, OnDestroy {
    
    private _boxExternalId: string | null = null;
    private _workspaceExternalId: string | null = null;

    private get workspaceExternalIdValue() {
        if(!this._workspaceExternalId) {
            throw new Error('Workspace external id is not set.');
        }

        return this._workspaceExternalId;
    }

    isLoadingBox = signal(false);
    isDeleting = signal(false);
    isBoxLoaded = signal(false);
    isBoxHeaderLoading = signal(false);
    isBoxFooterLoading = signal(false);
    isLoading = computed(() => this.isLoadingBox() || this.isDeleting() || this.isBoxHeaderLoading() || this.isBoxFooterLoading());
    areActionsVisible = signal(false);

    name: WritableSignal<string> = signal('');
    isEnabled: WritableSignal<boolean> = signal(false);
    nameToDisplay = computed(() => this.name() + (this.isEnabled() ? '' : ' (disabled)'));

    boxHeader: WritableSignal<AppBoxRichTextItem> = signal({
        isEnabled: signal(false),
        json: signal(null),
        operations: {
            update: async (json: string, html: string) => {},
            updateIsEnabled: async (isEnabled: boolean) => {},
        }
    });


    boxFooter: WritableSignal<AppBoxRichTextItem> = signal({
        isEnabled: signal(false),
        json: signal(null),
        operations: {
            update: async (json: string, html: string) => {},
            updateIsEnabled: async (isEnabled: boolean) => {},
        }
    });

    folder: WritableSignal<BoxFolder | null> = signal(null);

    initialBoxContent: WritableSignal<GetFolderResponse | null> = signal(null);

    isNameEditing = signal(false);

    links: WritableSignal<AppBoxLink[]> = signal([]);
    invitations: WritableSignal<AppBoxTeamInvitation[]> = signal([]);
    members: WritableSignal<AppBoxTeamMember[]> = signal([]);

    hasAnyInvitations = computed(() => this.invitations().length > 0);
    linksCount = computed(() => this.links().length);
    teamsCount = computed(() => this.members().length + this.invitations().length);

    filesApi: WritableSignal<FilesExplorerApi | null> = signal(null);
    uploadsApi = signal(null);

    activeTab: WritableSignal<'layout' | 'team' | 'links'> = signal('layout');

    isLayoutAcitve = computed(() => this.activeTab() === 'layout');
    isTeamActive = computed(() => this.activeTab() === 'team');
    isLinksActive = computed(() => this.activeTab() === 'links');

    private _routerSubscription: Subscription | null = null;

    constructor(
        private _setApi: FoldersAndFilesSetApi,
        private _getApi: FoldersAndFilesGetApi,
        private _router: Router,
        private _activatedRoute: ActivatedRoute,
        private _boxesApi: BoxesSetApi,
        private _dialog: MatDialog,
        private _auth: AuthService, 
        private _dataStore: DataStore,        
        private _genericDialogService: GenericDialogService,
        private _fileLockService: FileLockService
    ) {
     }

    async ngOnInit() {
        this.load();
                
        this._routerSubscription = this._router.events
            .pipe(filter(event => event instanceof NavigationEnd))
            .subscribe(() => this.load());
    }

    private async load() {
        const boxExternalId = this._activatedRoute.snapshot.params['boxExternalId'];
        const workspaceExternalId = this._activatedRoute.parent?.snapshot.params['workspaceExternalId'];

        const tab = this._activatedRoute.snapshot.params['tab'];
        
        if(tab === 'team')
            this.activeTab.set('team');
        else if(tab === 'links')
            this.activeTab.set('links');
        else
            this.activeTab.set('layout');

        if(boxExternalId == this._boxExternalId && workspaceExternalId == this._workspaceExternalId){
            return;
        }

        this._workspaceExternalId = workspaceExternalId;
        this._boxExternalId = boxExternalId;

        this.filesApi.set(new WorkspaceFilesExplorerApi(
            this._setApi,
            this._getApi,
            this._dataStore,
            this._fileLockService,
            this.workspaceExternalIdValue
        ));

        await this.loadBox(
            workspaceExternalId,
            boxExternalId);
    }
    

    ngOnDestroy(): void {
        this._routerSubscription?.unsubscribe();        
    }


    private async loadBox(workspaceExternalId: string, boxExternalId: string) {
        try {
            this.isLoadingBox.set(true);

            const response = await this
                ._dataStore
                .getBox(workspaceExternalId, boxExternalId);

            this._boxExternalId = response.details.externalId;
            this.name.set(response.details.name);
            this.isEnabled.set(response.details.isEnabled);

            const boxFolder = this.mapBoxFolder(
                response.details.folderPath);

            this.folder.set(boxFolder);

            this.boxHeader.set({
                 isEnabled: signal(response.details.header.isEnabled),
                 json: signal(response.details.header.json),
                 operations: {
                    update: (json: string, html: string) => this._boxesApi.updateBoxHeader(
                        workspaceExternalId, 
                        boxExternalId,
                        {json: json, html: html}),

                    updateIsEnabled: (isEnabled: boolean) => this._boxesApi.updateBoxHeaderIsEnabled(
                        workspaceExternalId,
                        boxExternalId,
                        {isEnabled: isEnabled})
                 }
            });

            this.boxFooter.set({
                isEnabled: signal(response.details.footer.isEnabled),
                json: signal(response.details.footer.json),
                operations: {
                   update: (json: string, html: string) => this._boxesApi.updateBoxFooter(
                       workspaceExternalId, 
                       boxExternalId,
                       {json: json, html: html}),

                   updateIsEnabled: (isEnabled: boolean) => this._boxesApi.updateBoxFooterIsEnabled(
                       workspaceExternalId,
                       boxExternalId,
                       {isEnabled: isEnabled})
                }
           });

            this.initialBoxContent.set(boxFolder 
                ? {
                    folder: boxFolder,
                    subfolders: response.subfolders,
                    files: response.files,
                    uploads: []
                }
                : null
            );

            this.links.set(response.links.map((link) => {
                const appLink: AppBoxLink = {
                    externalId: signal(link.externalId),
                    workspaceExternalId: workspaceExternalId,
                    accessCode: signal(link.accessCode),
                    isEnabled: signal(link.isEnabled),
                    name: signal(link.name), 
                    widgetOrigins: signal(link.widgetOrigins),
                    permissions: this.mapDtoToPermissions(link.permissions),
                    isNameEditing: signal(false)
                };

                return appLink;
            }));

            this.invitations.set(response
                .members
                .filter(member => !member.wasInvitationAccepted)
                .map(item => {
                    const invitation: AppBoxTeamInvitation = {
                        memberExternalId: signal(item.memberExternalId),
                        inviterEmail: item.inviterEmail,
                        email: item.memberEmail,
                        permissions: this.mapDtoToPermissions(item.permissions)
                    };

                    return invitation;
                }));

            this.members.set(response
                .members
                .filter(member => member.wasInvitationAccepted)
                .map(item => {
                    const member: AppBoxTeamMember = {
                        memberExternalId: signal(item.memberExternalId),
                        email: item.memberEmail,
                        permissions: this.mapDtoToPermissions(item.permissions)
                    };

                    return member;
                }));
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoadingBox.set(false);
            this.isBoxLoaded.set(true);
        }
    }

    private mapBoxFolder(folderPath: {name: string, externalId: string}[]): BoxFolder | null {
        if(folderPath.length == 0)
            return null;

        const topFolder = folderPath[folderPath.length - 1];

        return {
            externalId: topFolder.externalId,
            name: topFolder.name,
            ancestors: folderPath.slice(0, -1)
        };
    }

    private mapDtoToPermissions(permissions: BoxPermissions): AppBoxPermissions {
        return {
            allowUpload: signal(permissions.allowUpload),
            allowList: signal(permissions.allowList),
            allowDownload: signal(permissions.allowDownload),
            allowDeleteFile: signal(permissions.allowDeleteFile),
            allowDeleteFolder: signal(permissions.allowDeleteFolder),
            allowRenameFile: signal(permissions.allowRenameFile),
            allowRenameFolder: signal(permissions.allowRenameFolder),
            allowMoveItems: signal(permissions.allowMoveItems),
            allowCreateFolder: signal(permissions.allowCreateFolder)
        };
    }

    async changeBoxFolder() {
        const dialogRef = this._dialog.open(FolderPickerComponent, {
            width: '700px',
            data: {
                workspaceExternalId: this._workspaceExternalId,
            },
            maxHeight: '600px',
            position: {
                top: '100px'
            }
        });

        dialogRef.afterClosed().subscribe(async (folderToShare: AppFolderItem) => {
            if(!folderToShare)
                return;

            const folder = this.folder();

            if(folder && folder.externalId === folderToShare.externalId)
                return;

            this.folder.set({
                externalId: folderToShare.externalId,
                name: folderToShare.name(),
                ancestors: folderToShare.ancestors.slice()
            });

            if(this._boxExternalId)
                await this._boxesApi.updateBoxFolder(this.workspaceExternalIdValue, this._boxExternalId, {
                    folderExternalId: folderToShare.externalId
                });                
        });
    }

    async changeBoxIsEnabled() {
        if(!this._boxExternalId)
            return;

        toggle(this.isEnabled);

        await this._boxesApi.updateBoxIsEnabled(this.workspaceExternalIdValue, this._boxExternalId, {
            isEnabled: this.isEnabled()
        });            
    }

    async deleteBox() {       
        if (this._boxExternalId)
            await this._boxesApi.deleteBox(this.workspaceExternalIdValue, this._boxExternalId);

        
        this._router.navigate([`workspaces/${this._workspaceExternalId}/boxes`]);
    }

    async saveBoxName(newName: string) {
        if(!this._boxExternalId)
            return;

        this.name.set(newName);

        await this._boxesApi.updateBoxName(this.workspaceExternalIdValue, this._boxExternalId, {
            name: this.name()
        });            
    }

    async createNewLink() {
        if(!this._boxExternalId)
            return;

        const newLink: AppBoxLink = {
            externalId: signal(null),
            accessCode: signal(null),
            workspaceExternalId: this.workspaceExternalIdValue,

            name: signal('untitled link'),
            isEnabled: signal(true),
            widgetOrigins: signal([]),
            
            permissions: {
                allowList: signal(true),
                allowDownload: signal(false),
                allowUpload: signal(false),
                allowDeleteFile: signal(false),
                allowDeleteFolder: signal(false),
                allowRenameFile: signal(false),
                allowRenameFolder: signal(false),
                allowMoveItems: signal(false),
                allowCreateFolder: signal(false),        
            },    
            isNameEditing: signal(true)
        };

        this.links.update(values => [...values, newLink]);

        const result = await this._boxesApi.createBoxLink(this.workspaceExternalIdValue, this._boxExternalId, {
            name: newLink.name()
        });

        newLink.externalId.set(result.externalId);
        newLink.accessCode.set(result.accessCode);
    }

    async changeMemberPermissions(item: AppBoxTeamInvitation | AppBoxTeamMember) {
        const memberExternalId = item.memberExternalId();

        if(!memberExternalId)
            return;

        if(!this._boxExternalId)
            return;

        await this._boxesApi.updateBoxMemberPermissions(
            this.workspaceExternalIdValue, 
            this._boxExternalId, 
            memberExternalId, 
            mapPermissionsToDto(item.permissions));        
    }    

    async createInvitation() {
        const dialogRef = this._dialog.open(EmailPickerComponent, {
            width: '500px',
            maxHeight: '600px',
            position: {
                top: '100px'
            }
        });

        dialogRef.afterClosed().subscribe(async (inviteeEmails: string[]) => {
            if (!inviteeEmails || inviteeEmails.length === 0)
                return;

            if(!this._boxExternalId)
                return;

            const inviterEmail = await this._auth.getUserEmail();

            const newEmails = inviteeEmails
                .filter(email => !this.invitations().some(invitation => invitation.email === email))
                .filter(email => !this.members().some(member => member.email === email));
            
            const invitations: AppBoxTeamInvitation[] = newEmails.map(email => ({
                memberExternalId: signal(null),
                inviterEmail: inviterEmail,
                email: email,
                permissions: {
                    allowList: signal(true),
                    allowDownload: signal(false),
                    allowUpload: signal(false),
                    allowDeleteFile: signal(false),
                    allowDeleteFolder: signal(false),
                    allowRenameFile: signal(false),
                    allowRenameFolder: signal(false),
                    allowMoveItems: signal(false),
                    allowCreateFolder: signal(false), 
                }
            }));

            this.invitations.update(values => [...values, ...invitations]);

            try {
                this.isLoadingBox.set(true);

                const response = await this._boxesApi.createMemberInvitation(
                    this.workspaceExternalIdValue, this._boxExternalId, {
                    memberEmails: inviteeEmails
                });

                for (const newMember of response.members) {
                    const newInvitation = invitations
                        .find(invitation => invitation.email.toLowerCase() === newMember.email.toLowerCase());
                    
                    if(newInvitation) {
                        newInvitation.memberExternalId.set(newMember.externalId);
                    }
                }
            } catch (error: any) {
                this.invitations.update(values => values.filter(v => !invitations.some(i => i === v)))            

                if(error?.error?.code === 'max-team-members-exceeded') {
                    this._genericDialogService.openMaxTeamMembersReachedDialog();
                } else {
                    console.error(error);
                }
            } finally {
                this.isLoadingBox.set(false);
            }
        });
    }

    async cancelInvitation(invitation: AppBoxTeamInvitation) {
        const memberExternalId = invitation.memberExternalId();

        if (!memberExternalId)
            return;

        if(!this._boxExternalId)
            return;

        const index = this.invitations().indexOf(invitation);

        this.invitations.update(values => values.filter(i => i.memberExternalId() !== invitation.memberExternalId()))

        try {
            this.isLoadingBox.set(true);

            await this._boxesApi.revokeMember(
                this.workspaceExternalIdValue,
                this._boxExternalId,
                memberExternalId);
        } catch (error) {
            console.error(error);

            this.invitations.update(values => values.splice(index, 0, invitation));
        } finally {
            this.isLoadingBox.set(false);
        }
    }

    async revokeMember(member: AppBoxTeamMember) {
        if(!this._boxExternalId)
            return;

        const index = this.members().indexOf(member);

        this.members.update(values => values.filter(i => i.memberExternalId() !== member.memberExternalId()));

        try {
            this.isLoadingBox.set(true);

            await this._boxesApi.revokeMember(
                this.workspaceExternalIdValue,
                this._boxExternalId,
                member.memberExternalId());
        } catch (error) {
            console.error(error);

            this.members.update(values => values.splice(index, 0, member));
        } finally {
            this.isLoadingBox.set(false);
        }
    }

    prefetchExternalBox() {
        if(!this._boxExternalId)
            return;

        this._dataStore.prefetchExternalBoxDetailsAndContent(this._boxExternalId);
    }

    previewBox() {
        if(!this._boxExternalId)
            return;

        this._router.navigate([`box/${this._boxExternalId}`]);
    }

    openLayoutTab() {
        this._router.navigate([`workspaces/${this._workspaceExternalId}/boxes/${this._boxExternalId}/layout`], {
            replaceUrl: true
        });
    }

    openTeamTab() {
        this._router.navigate([`workspaces/${this._workspaceExternalId}/boxes/${this._boxExternalId}/team`], {
            replaceUrl: true
        });
    }

    openLinksTab() {
        this._router.navigate([`workspaces/${this._workspaceExternalId}/boxes/${this._boxExternalId}/links`], {
            replaceUrl: true
        });
    }

    goToBoxes() {
        this._router.navigate([`workspaces/${this._workspaceExternalId}/boxes`]);
    }

    prefetchBoxes() {
        this._dataStore.prefetchBoxes(this.workspaceExternalIdValue);
    }

    editName() {
        if(!this._boxExternalId)
            return;

        this.isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }

    onBoxLinkDeleted(boxLink: AppBoxLink) {
        this.links.update(values => values.filter(link => link !== boxLink));
    }

    toggleActions() {
        this.areActionsVisible.set(!this.areActionsVisible());
    }
}
