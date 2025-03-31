import { HttpBackend, HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface ChangePasswordResponse {
    code: 'success' | "password-mismatch" | 'failed';
}

export interface Get2FAStatusResponse {
    isEnabled: boolean;
    recoveryCodesLeft: number | null;
    qrCodeUri: string | null;
}

export interface Enable2FaRequest {
    verificationCode: string;
}

export interface Enable2FaResponse {
    code: 'enabled' | 'invalid-verification-code' | 'failed',
    recoveryCodes: string[];
}

export interface Disable2FaResponse {
    code: 'disabled' | 'failed';
}

export interface GenerateRecoveryCodesResponse {
    recoveryCodes: string[];
}

export interface GetAccountDetailsResponse {
    externalId: string,
    email: string,
    roles: AccountRoles,
    permissions: AccountPermissions;
    maxWorkspaceNumber: number | null;
}

export interface AccountRoles {
    isAppOwner: boolean;
    isAdmin: boolean;
}

export interface AccountPermissions {
    canAddWorkspace: boolean;
    canManageGeneralSettings: boolean;
    canManageUsers: boolean;
    canManageStorages: boolean;
    canManageEmailProviders: boolean;
}

export interface GetKnownUsersResponse {
    items: KnownUser[];
}

export interface KnownUser {
    externalId: string;
    email: string;
}

@Injectable({
    providedIn: 'root'
})
export class AccountApi {
    private _httpNoInterceptor: HttpClient;

    constructor(
        private _http: HttpClient,
        httpBacked: HttpBackend) {        
        this._httpNoInterceptor = new HttpClient(httpBacked);
    }

    public async changePassword(args: {currentPassword: string, newPassword: string}) {
        const call = this
            ._http
            .post<ChangePasswordResponse>(`/api/account/change-password`, args);

       return await firstValueFrom(call);
    }

    public async acceptTerms() {
        const call = this
            ._http
            .post(`/api/account/accept-terms`, {});

        await firstValueFrom(call);
    }

    public async signOut() {
        const call = this
            ._http
            .post(`/api/account/sign-out`, {});

        await firstValueFrom(call);
    }

    public async getDetails(): Promise<GetAccountDetailsResponse> {
        const call = this
            ._http
            .get<GetAccountDetailsResponse>(`/api/account/details`);

        return await firstValueFrom(call);
    }

    public async getKnownUsers(): Promise<GetKnownUsersResponse> {
        const call = this
            ._http
            .get<GetKnownUsersResponse>(`/api/account/known-users`);

        return await firstValueFrom(call);
    }

    public async getDetailsNoInterceptor(): Promise<GetAccountDetailsResponse> {
        const call = this
            ._httpNoInterceptor
            .get<GetAccountDetailsResponse>(`/api/account/details`);

        return await firstValueFrom(call);
    }

    public async get2FaStatus(): Promise<Get2FAStatusResponse> {
        const call = this
            ._http
            .get<Get2FAStatusResponse>(`/api/account/2fa/status`);

        return await firstValueFrom(call);
    }

    public async enable2Fa(request: Enable2FaRequest): Promise<Enable2FaResponse> {
        const call = this
            ._http
            .post<Enable2FaResponse>(`/api/account/2fa/enable`, request);

        return await firstValueFrom(call);
    }

    public async disable2Fa(): Promise<Disable2FaResponse> {
        const call = this
            ._http
            .post<Disable2FaResponse>(`/api/account/2fa/disable`, {});

        return await firstValueFrom(call);
    }

    public async generateRecoveryCodes(): Promise<GenerateRecoveryCodesResponse>{
        const call = this
            ._http
            .post<GenerateRecoveryCodesResponse>(`/api/account/2fa/generate-recovery-codes`, {});

        return await firstValueFrom(call);
    }
}