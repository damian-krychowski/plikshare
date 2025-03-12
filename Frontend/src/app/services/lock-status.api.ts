import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";


export interface CheckFileLocksRequest {
    externalIds: string[];
}

export interface CheckFileLocksResponse {
    lockedExternalIds: string[];
}

@Injectable({
    providedIn: 'root'
})
export class LockStatusApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async checkFileLocks(request: CheckFileLocksRequest): Promise<CheckFileLocksResponse> {
        const call = this
            ._http
            .post<CheckFileLocksResponse>(`/api/lock-status/files`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }
}