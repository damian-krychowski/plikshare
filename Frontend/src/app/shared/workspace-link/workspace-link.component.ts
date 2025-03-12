import { Component, computed, input } from "@angular/core";
import { AppUser } from "../app-user";
import { AuthService } from "../../services/auth.service";
import { DataStore } from "../../services/data-store.service";
import { Router } from "@angular/router";
import { PrefetchDirective } from "../prefetch.directive";

export type AppWorkspaceLink = {
    externalId: string;
    name: string;
}

@Component({
    selector: 'app-workspace-link',
    imports: [
        PrefetchDirective
    ],
    templateUrl: './workspace-link.component.html',
    styleUrl: './workspace-link.component.scss'
})
export class WorkspaceLinkComponenet {
    workspace = input.required<AppWorkspaceLink>();
    
    constructor(
        public auth: AuthService,
        public dataStore: DataStore,
        private _router: Router
    ) {}

    onPrefetch() {
        this.dataStore.prefetchWorkspace(
            this.workspace().externalId);
    }

    onClick(event: any){
        event.stopPropagation();

        this._router.navigate([`/workspaces/${this.workspace().externalId}/explorer`]);
    }
}