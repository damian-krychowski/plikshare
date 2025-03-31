import { computed, Injectable, signal, WritableSignal } from '@angular/core';
import { AccountApi, GetAccountDetailsResponse } from './account.api';
import { AppUser } from '../shared/app-user';
import { AntiforgeryApi } from './antiforgery.api';

@Injectable({
    providedIn: 'root',
})
export class AuthService {
    public userDetails: WritableSignal<GetAccountDetailsResponse | null> = signal(null);

    public userEmail = computed(() => this.userDetails()?.email ?? '');
    public userExternalId = computed(() => this.userDetails()?.externalId ?? '')

    public isAppOwner = computed(() => this.userDetails()?.roles?.isAppOwner ?? false);
    public isAdmin = computed(() => (this.isAppOwner() || this.userDetails()?.roles?.isAdmin) ?? false);

    public canAddWorkspace = computed(() => this.userDetails()?.permissions?.canAddWorkspace ?? false);
    public canManageGeneralSettings = computed(() => this.userDetails()?.permissions?.canManageGeneralSettings ?? false);
    public canManageUsers = computed(() => this.userDetails()?.permissions?.canManageUsers ?? false);
    public canManageStorages = computed(() => this.userDetails()?.permissions?.canManageStorages ?? false);  
    public canManageEmailProviders = computed(() => this.userDetails()?.permissions?.canManageEmailProviders ?? false);

    public maxWorkspaceNumber = computed(() => this.userDetails()?.maxWorkspaceNumber ?? null);

    public canManageAnything = computed(() => 
        this.canManageGeneralSettings() 
        || this.canManageUsers()
        || this.canManageStorages()
        || this.canManageEmailProviders());

    private _userDetails: Promise<GetAccountDetailsResponse> | null = null;

    constructor(
        private _accountApi: AccountApi,
        private _antiforgeryApi: AntiforgeryApi) {
    }

    public async isAuthenticatedAsync(): Promise<boolean> {
        try {
            const detailsPromise = this._accountApi.getDetailsNoInterceptor();
            
            const details = await detailsPromise;

            this._userDetails = detailsPromise;

            this.userDetails.set(details);
            
            return true;
        } catch (error) {
            return false;
        }
    }

    public async initiateSessionIfNeeded() {
        if(!this._userDetails) {
            await this.initiateSession();
        }
    }

    public async initiateSession() {        
        await this._antiforgeryApi.fetchForAnonymousOrInternal();
        this._userDetails = this._accountApi.getDetails();

        const details = await this._userDetails;        

        this.userDetails.set(details);
    }

    public async signOut() {
        await this._accountApi.signOut();
        await this._antiforgeryApi.fetchForAnonymousOrInternal();
        this._userDetails = null;
    }

    public async getUserEmail(): Promise<string> {
        try {
            await this.initiateSessionIfNeeded();            
            return (await this._userDetails)!.email;
        } catch (error) {
            console.error("Error getting user email", error);
            throw error;
        }
    }

    public async getUser(): Promise<AppUser> {
        try {
            await this.initiateSessionIfNeeded();         
            const details = await this._userDetails;
            
            if(!details)
                throw new Error("Could not intiate session");

            return {
                email: signal(details.email),
                externalId: details.externalId
            };
        } catch (error) {
            console.error("Error getting user email", error);
            throw error;
        }
    }

    public changePassword(args: { oldPassword: string, newPassword: string }) {
        return this._accountApi.changePassword({
            currentPassword: args.oldPassword,
            newPassword: args.newPassword
        });
    }

    public async isSocialSignIn(): Promise<boolean> {
        // const { accessToken, idToken } = (await fetchAuthSession()).tokens ?? {};

        // if (!idToken) {
        //     return false;
        // }

        // const cognitoUsername = idToken.payload['cognito:username'] as string;

        // return cognitoUsername.startsWith('google_');

        return false;
    }

    public async hasAcceptedTerms(): Promise<boolean> {
        // try {
        //     const { accessToken, idToken } = (await fetchAuthSession()).tokens ?? {};

        //     if (!idToken) {
        //         return false;
        //     }

        //     const acceptTermsDate = idToken.payload['custom:accept_terms_date'];

        //     return !!acceptTermsDate;
        // } catch (error) {
        //     console.error(error);
        //     return false;
        // }

        return true;
    }
}
