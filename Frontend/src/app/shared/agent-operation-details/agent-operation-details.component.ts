import { Component, input } from '@angular/core';
import { AgentOperationDetails } from '../../services/agents.api';
import { BulkDeleteOperationDetailsComponent } from './bulk-delete-operation-details/bulk-delete-operation-details.component';
import { DeleteShareLinkOperationDetailsComponent } from './delete-share-link-operation-details/delete-share-link-operation-details.component';
import { RenameFolderOperationDetailsComponent } from './rename-folder-operation-details/rename-folder-operation-details.component';
import { RenameFileOperationDetailsComponent } from './rename-file-operation-details/rename-file-operation-details.component';
import { CreateFolderOperationDetailsComponent } from './create-folder-operation-details/create-folder-operation-details.component';
import { MoveItemsOperationDetailsComponent } from './move-items-operation-details/move-items-operation-details.component';
import { CreateFileOperationDetailsComponent } from './create-file-operation-details/create-file-operation-details.component';
import { RenameWorkspaceOperationDetailsComponent } from './rename-workspace-operation-details/rename-workspace-operation-details.component';
import { CreateShareLinkOperationDetailsComponent } from './create-share-link-operation-details/create-share-link-operation-details.component';
import { UpdateShareLinkOperationDetailsComponent } from './update-share-link-operation-details/update-share-link-operation-details.component';
import { CreateWorkspaceOperationDetailsComponent } from './create-workspace-operation-details/create-workspace-operation-details.component';
import { ReadFileOperationDetailsComponent } from './read-file-operation-details/read-file-operation-details.component';
import { GetFileOperationDetailsComponent } from './get-file-operation-details/get-file-operation-details.component';
import { GetFileDownloadLinkOperationDetailsComponent } from './get-file-download-link-operation-details/get-file-download-link-operation-details.component';
import { ListWorkspacesOperationDetailsComponent } from './list-workspaces-operation-details/list-workspaces-operation-details.component';
import { ListStoragesOperationDetailsComponent } from './list-storages-operation-details/list-storages-operation-details.component';
import { ListShareLinksOperationDetailsComponent } from './list-share-links-operation-details/list-share-links-operation-details.component';
import { GetShareLinkOperationDetailsComponent } from './get-share-link-operation-details/get-share-link-operation-details.component';
import { SearchOperationDetailsComponent } from './search-operation-details/search-operation-details.component';
import { ListWorkspaceContentOperationDetailsComponent } from './list-workspace-content-operation-details/list-workspace-content-operation-details.component';
import { GetBulkDownloadLinkOperationDetailsComponent } from './get-bulk-download-link-operation-details/get-bulk-download-link-operation-details.component';
import { ListWorkspaceMembersOperationDetailsComponent } from './list-workspace-members-operation-details/list-workspace-members-operation-details.component';
import { InviteWorkspaceMembersOperationDetailsComponent } from './invite-workspace-members-operation-details/invite-workspace-members-operation-details.component';
import { UpdateWorkspaceMemberPermissionsOperationDetailsComponent } from './update-workspace-member-permissions-operation-details/update-workspace-member-permissions-operation-details.component';
import { RevokeWorkspaceMemberOperationDetailsComponent } from './revoke-workspace-member-operation-details/revoke-workspace-member-operation-details.component';
import { ListBoxesOperationDetailsComponent } from './list-boxes-operation-details/list-boxes-operation-details.component';
import { GetBoxOperationDetailsComponent } from './get-box-operation-details/get-box-operation-details.component';
import { CreateBoxOperationDetailsComponent } from './create-box-operation-details/create-box-operation-details.component';
import { UpdateBoxOperationDetailsComponent } from './update-box-operation-details/update-box-operation-details.component';
import { DeleteBoxOperationDetailsComponent } from './delete-box-operation-details/delete-box-operation-details.component';
import { ListBoxLinksOperationDetailsComponent } from './list-box-links-operation-details/list-box-links-operation-details.component';
import { CreateBoxLinkOperationDetailsComponent } from './create-box-link-operation-details/create-box-link-operation-details.component';
import { UpdateBoxLinkOperationDetailsComponent } from './update-box-link-operation-details/update-box-link-operation-details.component';
import { DeleteBoxLinkOperationDetailsComponent } from './delete-box-link-operation-details/delete-box-link-operation-details.component';
import { RegenerateBoxLinkAccessCodeOperationDetailsComponent } from './regenerate-box-link-access-code-operation-details/regenerate-box-link-access-code-operation-details.component';
import { ListBoxMembersOperationDetailsComponent } from './list-box-members-operation-details/list-box-members-operation-details.component';
import { InviteBoxMembersOperationDetailsComponent } from './invite-box-members-operation-details/invite-box-members-operation-details.component';
import { UpdateBoxMemberPermissionsOperationDetailsComponent } from './update-box-member-permissions-operation-details/update-box-member-permissions-operation-details.component';
import { RevokeBoxMemberOperationDetailsComponent } from './revoke-box-member-operation-details/revoke-box-member-operation-details.component';

@Component({
    selector: 'app-agent-operation-details',
    standalone: true,
    imports: [
        BulkDeleteOperationDetailsComponent,
        DeleteShareLinkOperationDetailsComponent,
        RenameFolderOperationDetailsComponent,
        RenameFileOperationDetailsComponent,
        CreateFolderOperationDetailsComponent,
        MoveItemsOperationDetailsComponent,
        CreateFileOperationDetailsComponent,
        RenameWorkspaceOperationDetailsComponent,
        CreateShareLinkOperationDetailsComponent,
        UpdateShareLinkOperationDetailsComponent,
        CreateWorkspaceOperationDetailsComponent,
        ReadFileOperationDetailsComponent,
        GetFileOperationDetailsComponent,
        GetFileDownloadLinkOperationDetailsComponent,
        ListWorkspacesOperationDetailsComponent,
        ListStoragesOperationDetailsComponent,
        ListShareLinksOperationDetailsComponent,
        GetShareLinkOperationDetailsComponent,
        SearchOperationDetailsComponent,
        ListWorkspaceContentOperationDetailsComponent,
        GetBulkDownloadLinkOperationDetailsComponent,
        ListWorkspaceMembersOperationDetailsComponent,
        InviteWorkspaceMembersOperationDetailsComponent,
        UpdateWorkspaceMemberPermissionsOperationDetailsComponent,
        RevokeWorkspaceMemberOperationDetailsComponent,
        ListBoxesOperationDetailsComponent,
        GetBoxOperationDetailsComponent,
        CreateBoxOperationDetailsComponent,
        UpdateBoxOperationDetailsComponent,
        DeleteBoxOperationDetailsComponent,
        ListBoxLinksOperationDetailsComponent,
        CreateBoxLinkOperationDetailsComponent,
        UpdateBoxLinkOperationDetailsComponent,
        DeleteBoxLinkOperationDetailsComponent,
        RegenerateBoxLinkAccessCodeOperationDetailsComponent,
        ListBoxMembersOperationDetailsComponent,
        InviteBoxMembersOperationDetailsComponent,
        UpdateBoxMemberPermissionsOperationDetailsComponent,
        RevokeBoxMemberOperationDetailsComponent
    ],
    template: `
        @switch(details().$type) {
            @case('bulk_delete') {
                <app-bulk-delete-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-bulk-delete-operation-details>
            }
            @case('delete_share_link') {
                <app-delete-share-link-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-delete-share-link-operation-details>
            }
            @case('rename_folder') {
                <app-rename-folder-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-rename-folder-operation-details>
            }
            @case('rename_file') {
                <app-rename-file-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-rename-file-operation-details>
            }
            @case('create_folder') {
                <app-create-folder-operation-details
                    [details]="$any(details())">
                </app-create-folder-operation-details>
            }
            @case('move_items') {
                <app-move-items-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-move-items-operation-details>
            }
            @case('create_file') {
                <app-create-file-operation-details
                    [details]="$any(details())">
                </app-create-file-operation-details>
            }
            @case('rename_workspace') {
                <app-rename-workspace-operation-details
                    [details]="$any(details())">
                </app-rename-workspace-operation-details>
            }
            @case('create_share_link') {
                <app-create-share-link-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-create-share-link-operation-details>
            }
            @case('update_share_link') {
                <app-update-share-link-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-update-share-link-operation-details>
            }
            @case('create_workspace') {
                <app-create-workspace-operation-details
                    [details]="$any(details())">
                </app-create-workspace-operation-details>
            }
            @case('read_file') {
                <app-read-file-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-read-file-operation-details>
            }
            @case('get_file') {
                <app-get-file-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-get-file-operation-details>
            }
            @case('get_file_download_link') {
                <app-get-file-download-link-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-get-file-download-link-operation-details>
            }
            @case('list_workspaces') {
                <app-list-workspaces-operation-details
                    [details]="$any(details())">
                </app-list-workspaces-operation-details>
            }
            @case('list_storages') {
                <app-list-storages-operation-details
                    [details]="$any(details())">
                </app-list-storages-operation-details>
            }
            @case('list_share_links') {
                <app-list-share-links-operation-details
                    [details]="$any(details())">
                </app-list-share-links-operation-details>
            }
            @case('get_share_link') {
                <app-get-share-link-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-get-share-link-operation-details>
            }
            @case('search') {
                <app-search-operation-details
                    [details]="$any(details())">
                </app-search-operation-details>
            }
            @case('list_workspace_content') {
                <app-list-workspace-content-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-list-workspace-content-operation-details>
            }
            @case('get_bulk_download_link') {
                <app-get-bulk-download-link-operation-details
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
                </app-get-bulk-download-link-operation-details>
            }
            @case('list_workspace_members') {
                <app-list-workspace-members-operation-details
                    [details]="$any(details())">
                </app-list-workspace-members-operation-details>
            }
            @case('invite_workspace_members') {
                <app-invite-workspace-members-operation-details
                    [details]="$any(details())">
                </app-invite-workspace-members-operation-details>
            }
            @case('update_workspace_member_permissions') {
                <app-update-workspace-member-permissions-operation-details
                    [details]="$any(details())">
                </app-update-workspace-member-permissions-operation-details>
            }
            @case('revoke_workspace_member') {
                <app-revoke-workspace-member-operation-details
                    [details]="$any(details())">
                </app-revoke-workspace-member-operation-details>
            }
            @case('list_boxes') {
                <app-list-boxes-operation-details
                    [details]="$any(details())">
                </app-list-boxes-operation-details>
            }
            @case('get_box') {
                <app-get-box-operation-details
                    [details]="$any(details())">
                </app-get-box-operation-details>
            }
            @case('create_box') {
                <app-create-box-operation-details
                    [details]="$any(details())">
                </app-create-box-operation-details>
            }
            @case('update_box') {
                <app-update-box-operation-details
                    [details]="$any(details())">
                </app-update-box-operation-details>
            }
            @case('delete_box') {
                <app-delete-box-operation-details
                    [details]="$any(details())">
                </app-delete-box-operation-details>
            }
            @case('list_box_links') {
                <app-list-box-links-operation-details
                    [details]="$any(details())">
                </app-list-box-links-operation-details>
            }
            @case('create_box_link') {
                <app-create-box-link-operation-details
                    [details]="$any(details())">
                </app-create-box-link-operation-details>
            }
            @case('update_box_link') {
                <app-update-box-link-operation-details
                    [details]="$any(details())">
                </app-update-box-link-operation-details>
            }
            @case('delete_box_link') {
                <app-delete-box-link-operation-details
                    [details]="$any(details())">
                </app-delete-box-link-operation-details>
            }
            @case('regenerate_box_link_access_code') {
                <app-regenerate-box-link-access-code-operation-details
                    [details]="$any(details())">
                </app-regenerate-box-link-access-code-operation-details>
            }
            @case('list_box_members') {
                <app-list-box-members-operation-details
                    [details]="$any(details())">
                </app-list-box-members-operation-details>
            }
            @case('invite_box_members') {
                <app-invite-box-members-operation-details
                    [details]="$any(details())">
                </app-invite-box-members-operation-details>
            }
            @case('update_box_member_permissions') {
                <app-update-box-member-permissions-operation-details
                    [details]="$any(details())">
                </app-update-box-member-permissions-operation-details>
            }
            @case('revoke_box_member') {
                <app-revoke-box-member-operation-details
                    [details]="$any(details())">
                </app-revoke-box-member-operation-details>
            }
            @default {
                <div class="explanation">No details available for this operation.</div>
            }
        }
    `
})
export class AgentOperationDetailsComponent {
    details = input.required<AgentOperationDetails>();
    workspaceExternalId = input<string | null>(null);
}
