import { Router, RouterModule } from "@angular/router";
import { Component, OnInit, ViewEncapsulation, signal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { PrefetchDirective } from "../shared/prefetch.directive";
import { AuthService } from "../services/auth.service";
import { DataStore } from "../services/data-store.service";
import { MatDialog } from "@angular/material/dialog";
import { ChangePasswordComponent } from "./change-password/change-password.component";
import {MatRadioModule} from '@angular/material/radio';
import { SignOutService } from "../services/sign-out.service";
import { ApplicationSettingsApi } from "../services/application-settings.api";

@Component({
    selector: 'app-account',
    imports: [
        RouterModule,
        MatButtonModule,
        MatTooltipModule,
        PrefetchDirective,
        MatRadioModule
    ],
    templateUrl: './account.component.html',
    styleUrl: './account.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class AccountComponent implements OnInit {       
    isLoading = signal(false);

    isEmailConfigNeeded = signal(false);
    isStorageConfigNeeded = signal(false);

    constructor(
        private _dialog: MatDialog,
        public auth: AuthService,
        private _signOutService: SignOutService,
        private _router: Router,
        public dataStore: DataStore,
        private _applicationSettingsApi: ApplicationSettingsApi
    ) {
    }

    async ngOnInit() {
        await this.auth.initiateSessionIfNeeded();
        await this.loadStatus();   
    }

    private async loadStatus() {
        if(!this.auth.isAdmin())
            return;

        try {
            const result = await this._applicationSettingsApi.getStatus();            

            this.isEmailConfigNeeded.set(result.isEmailProviderConfigured === false);
            this.isStorageConfigNeeded.set(result.isStorageConfigured === false);
        } catch (error) {
            console.error(error);            
        }
    }

    async signOut() {
        await this._signOutService.signOut();
    }

    goToDashboard() {
        this._router.navigate(['workspaces']);
    }

    changePassword() {
        const dialogRef = this._dialog.open(ChangePasswordComponent, {
            width: '500px',
            position: {
                top: '100px'
            }
        });
    }

    setupMfa() {
        this._router.navigate(['account/mfa']);
    }

    goToGeneralSettings() {
        this._router.navigate(['settings/general']);
    }

    goToStorageSettings(){
        this._router.navigate(['settings/storage']);
    }

    goToEmailSettings() {
        this._router.navigate(['settings/email']);
    }

    goToUsersSettings(){
        this._router.navigate(['settings/users']);
    }

    goToIntegrations(){
        this._router.navigate(['settings/integrations']);
    }
}