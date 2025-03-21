import { Injectable } from "@angular/core";
import { AuthService } from "./auth.service";
import { DataStore } from "./data-store.service";
import { Router } from "@angular/router";

@Injectable({
    providedIn: 'root',
})
export class SignOutService {
    constructor(
        private _auth: AuthService,
        private _dataStore: DataStore,
        private _router: Router
    ) {}

    async signOut() {
        await this.executeSignOut();
        await this._router.navigate(['']);
    }

    async signOutAndNavigateByUrl(url: string) {
        await this.executeSignOut();
        await this._router.navigateByUrl(url)
    }

    private async executeSignOut() {
        await this._auth.signOut();
        this._dataStore.clear();        
    }
}