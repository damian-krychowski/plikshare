import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";


@Injectable({
    providedIn: 'root'
})
export class WidgetsApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async getWidgetScripts(): Promise<string> {
        const call = this
            ._http
            .get(`/api/widgets/scripts`, { 
                responseType: 'text'
            });

        return await firstValueFrom(call);
    }
}