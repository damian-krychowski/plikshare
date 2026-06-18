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
        GetBulkDownloadLinkOperationDetailsComponent
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
                    [details]="$any(details())">
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
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
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
                    [details]="$any(details())"
                    [workspaceExternalId]="workspaceExternalId()">
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
                    [details]="$any(details())">
                </app-update-share-link-operation-details>
            }
            @case('create_workspace') {
                <app-create-workspace-operation-details
                    [details]="$any(details())">
                </app-create-workspace-operation-details>
            }
            @case('read_file') {
                <app-read-file-operation-details
                    [details]="$any(details())">
                </app-read-file-operation-details>
            }
            @case('get_file') {
                <app-get-file-operation-details
                    [details]="$any(details())">
                </app-get-file-operation-details>
            }
            @case('get_file_download_link') {
                <app-get-file-download-link-operation-details
                    [details]="$any(details())">
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
                    [details]="$any(details())">
                </app-get-share-link-operation-details>
            }
            @case('search') {
                <app-search-operation-details
                    [details]="$any(details())">
                </app-search-operation-details>
            }
            @case('list_workspace_content') {
                <app-list-workspace-content-operation-details
                    [details]="$any(details())">
                </app-list-workspace-content-operation-details>
            }
            @case('get_bulk_download_link') {
                <app-get-bulk-download-link-operation-details
                    [details]="$any(details())">
                </app-get-bulk-download-link-operation-details>
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
