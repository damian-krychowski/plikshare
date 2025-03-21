import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

@Injectable({
    providedIn: 'root'
})
export class AntiforgeryApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async fetchForAnonymousOrInternal(): Promise<void> {
        const call = this
            ._http
            .get(
                `/api/antiforgery/token`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async fetchForBoxLink(): Promise<void> {
        const call = this
            ._http
            .get(
                `/api/antiforgery/box-link-token`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }
}