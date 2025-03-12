import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { ApplicationSingUp } from "../../services/general-settings.api";

@Injectable({
    providedIn: 'root'
})
export class SingUpService {
    constructor(
        private _http: HttpClient) {        
    }

    private _isSignUpAvailablePromise: Promise<boolean> | null = null;

    public async isSingUpAvailabe(): Promise<boolean> {        
        if(!this._isSignUpAvailablePromise) {
            this._isSignUpAvailablePromise = this.getIsSingUpAvailable();
        }

        return await this._isSignUpAvailablePromise;
    }

    private async getIsSingUpAvailable(): Promise<boolean> {
        const setting = await this.getApplicationSingUpSetting();

        return setting.value === 'everyone';
    }

    private async getApplicationSingUpSetting(): Promise<{value: ApplicationSingUp}> {
        const call = this
            ._http
            .get<{value: ApplicationSingUp}>(
                `/api/settings/application-sign-up`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }
}