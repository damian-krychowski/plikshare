import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface GetAuthProvidersResponse {
    items: GetAuthProvidersResponseItem[];
}

export interface GetAuthProvidersResponseItem {
    externalId: string,
    name: string,
    type: string,
    isActive: boolean,
    clientId: string,
    issuerUrl: string
}

export interface CreateOidcAuthProviderRequest {
    name: string;
    clientId: string;
    clientSecret: string;
    issuerUrl: string;
}

export interface CreateOidcAuthProviderResponse {
    externalId: string;
}

export interface UpdateAuthProviderRequest {
    name: string;
    clientId: string;
    clientSecret: string;
    issuerUrl: string;
}

export interface TestAuthProviderConfigurationRequest {
    issuerUrl: string;
    clientId: string;
    clientSecret: string;
}

export interface TestAuthProviderConfigurationResponse {
    code: string;
    details: string;
}

@Injectable({
    providedIn: 'root'
})
export class AuthProvidersApi {
    constructor(
        private _http: HttpClient) {
    }

    public async getAuthProviders(): Promise<GetAuthProvidersResponse> {
        const call = this
            ._http
            .get<GetAuthProvidersResponse>(
                `/api/auth-providers`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async createOidcAuthProvider(request: CreateOidcAuthProviderRequest): Promise<CreateOidcAuthProviderResponse> {
        const call = this
            ._http
            .post<CreateOidcAuthProviderResponse>(
                `/api/auth-providers/oidc`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async updateAuthProvider(externalId: string, request: UpdateAuthProviderRequest): Promise<void> {
        const call = this
            ._http
            .put(
                `/api/auth-providers/${externalId}`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async testConfiguration(request: TestAuthProviderConfigurationRequest): Promise<TestAuthProviderConfigurationResponse> {
        const call = this
            ._http
            .post<TestAuthProviderConfigurationResponse>(
                `/api/auth-providers/test-configuration`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async deleteAuthProvider(externalId: string) {
        const call = this
            ._http
            .delete(
                `/api/auth-providers/${externalId}`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

         await firstValueFrom(call);
    }

    public async activate(externalId: string): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/auth-providers/${externalId}/activate`, {}, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async deactivate(externalId: string): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/auth-providers/${externalId}/deactivate`, {}, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }
}
