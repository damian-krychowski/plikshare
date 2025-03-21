import { HttpClient, HttpEvent, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom, Observable } from "rxjs";

export type ApplicationSingUp = 'everyone' | 'only-invited-users';

export interface GetApplicationSettingsResponse {
    applicationSignUp: ApplicationSingUp;
    termsOfService: string | null;
    privacyPolicy: string | null;
    applicationName: string | null;
}

export interface SetApplicationSignUpRequest {
    value: ApplicationSingUp;
}

export interface SetApplicationNameRequest {
    value: string | null;
}

@Injectable({
    providedIn: 'root'
})
export class GeneralSettingsApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async getAppSettings(): Promise<GetApplicationSettingsResponse> {
        const call = this
            ._http
            .get<GetApplicationSettingsResponse>(
                `/api/general-settings`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async setApplicationSingUp(request: SetApplicationSignUpRequest) {
        const call = this
            ._http
            .patch(
                `/api/general-settings/application-sign-up`,request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async setApplicationName(request: SetApplicationNameRequest) {
        const call = this
            ._http
            .patch(
                `/api/general-settings/application-name`,request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public uploadTermsOfService(file: File): Observable<HttpEvent<Object>> {
        const formData = new FormData();

        formData.append('file', file, file.name);

        return this
            ._http
            .post(
                `/api/general-settings/terms-of-service`,formData, {
                reportProgress: true,
                observe: "events"
            });
    }

    public async deleteTermsOfService(): Promise<void> {
        const call = this
            ._http
            .delete(`/api/general-settings/terms-of-service`);

        await firstValueFrom(call);
    }

    public uploadPrivacyPolicy(file: File): Observable<HttpEvent<Object>> {
        const formData = new FormData();

        formData.append('file', file, file.name);

        return this
            ._http
            .post(
                `/api/general-settings/privacy-policy`,formData, {
                reportProgress: true,
                observe: "events"
            });
    }

    public async deletePrivacyPolicy(): Promise<void> {
        const call = this
            ._http
            .delete(`/api/general-settings/privacy-policy`);

        await firstValueFrom(call);
    }
}