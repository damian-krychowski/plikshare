import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface GetApplicationSettingsStatusResponse {
    isEmailProviderConfigured: boolean | null;
    isStorageConfigured: boolean | null;
}


@Injectable({
    providedIn: 'root'
})
export class ApplicationSettingsApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async getStatus(): Promise<GetApplicationSettingsStatusResponse> {
        const call = this
            ._http
            .get<GetApplicationSettingsStatusResponse>(
                `/api/application-settings/status`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }
}