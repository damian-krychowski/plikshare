import { Component, computed, input } from "@angular/core";
import { AppUser } from "../app-user";
import { AuthService } from "../../services/auth.service";
import { DataStore } from "../../services/data-store.service";
import { Router } from "@angular/router";
import { PrefetchDirective } from "../prefetch.directive";

@Component({
    selector: 'app-user-link',
    imports: [
        PrefetchDirective
    ],
    templateUrl: './user-link.component.html',
    styleUrl: './user-link.component.scss'
})
export class UserLinkComponenet {
    prefix = input.required<string>();
    user = input.required<AppUser>();
    
    email = computed(() => this.user().email());
    isClickable = computed(() => this.auth.canManageUsers() && this.user().externalId !== this.auth.userExternalId())

    constructor(
        public auth: AuthService,
        public dataStore: DataStore,
        private _router: Router
    ) {}

    onPrefetch() {
        if(!this.isClickable())
            return;

        this.dataStore.prefetchUserDetails(
            this.user().externalId);
    }

    onClick(event: any){
        if(!this.isClickable())
            return;

        event.stopPropagation();

        this._router.navigate([`/settings/users/${this.user().externalId}`])
    }
}