import { Component, computed, OnDestroy, OnInit, signal } from "@angular/core";
import { FormsModule, ReactiveFormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { ExternalBoxFileSearchItem, ExternalBoxSearchGroup, SearchResult, SearchService, WorkspaceFileSearchItem, WorkspaceGroupSearchItem, WorkspaceSearchGroup } from "../../services/search.service";
import { Subscription } from "rxjs";
import { WorkspaceItemComponent } from "../workspace-item/workspace-item.component";
import { BoxItemComponent } from "../box-item/box-item.component";
import { FolderItemComponent } from "../folder-item/folder-item.component";
import { FileItemComponent } from "../file-item/file-item.component";
import { ExternalBoxItemComponent } from "../external-box-item/external-box-item.component";
import { Router } from "@angular/router";

@Component({
    selector: 'app-search',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ReactiveFormsModule,
        MatButtonModule,
        WorkspaceItemComponent,
        BoxItemComponent,
        FolderItemComponent,
        FileItemComponent,
        ExternalBoxItemComponent
    ],
    templateUrl: './search.component.html',
    styleUrl: './search.component.scss'
})
export class SearchComponent implements OnInit, OnDestroy {

    private _subscription: Subscription | null = null;

    results = signal<SearchResult[]>([]);
    hasAnyResults = computed(() => this.results().length > 0);

    constructor(
        private _router: Router,
        private _searchService: SearchService) { }

    ngOnInit(): void {
        this._subscription = this._searchService.searchResults$.subscribe(result => {
            if(!result) {
                this.results.set([]);
            } else {
                this.results.set([result]);
            }
        });
    }

    ngOnDestroy(): void {
        this._subscription?.unsubscribe();
    }

    workspaceFilePreview(file: WorkspaceFileSearchItem, workspace: WorkspaceSearchGroup) {
        this._router.navigate(
            [`workspaces/${workspace.externalId}/explorer/${file.file.folderExternalId ?? ''}`],
            {
                queryParams: { fileId: file.file.externalId }
            }
        );
    }

    boxFilePreview(file: ExternalBoxFileSearchItem, externalBox: ExternalBoxSearchGroup) {
        this._router.navigate(
            [`box/${externalBox.externalId}/${file.file.folderExternalId ?? ''}`],
            {
                queryParams: { fileId: file.file.externalId }
            }
        );
    }
       
    onGroupItemDeleted(group: WorkspaceSearchGroup, item: WorkspaceGroupSearchItem) {
        const index = group.items.indexOf(item);

        if(index == -1)
            return;

        group.items.splice(index,1);
    }
}