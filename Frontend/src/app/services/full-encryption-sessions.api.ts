import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface FullEncryptionSessionItem {
    storageExternalId: string;
    storageName: string;
}

export interface GetFullEncryptionSessionsResponse {
    items: FullEncryptionSessionItem[];
}

@Injectable({
    providedIn: 'root'
})
export class FullEncryptionSessionsApi {
    constructor(private _http: HttpClient) {
    }

    public async getAll(): Promise<GetFullEncryptionSessionsResponse> {
        const call = this
            ._http
            .get<GetFullEncryptionSessionsResponse>(`/api/full-encryption-sessions/`);

        return await firstValueFrom(call);
    }

    public async lockAll(): Promise<void> {
        const call = this
            ._http
            .delete(`/api/full-encryption-sessions/`);

        await firstValueFrom(call);
    }

    public async lock(storageExternalId: string): Promise<void> {
        const call = this
            ._http
            .delete(`/api/full-encryption-sessions/${storageExternalId}`);

        await firstValueFrom(call);
    }
}
