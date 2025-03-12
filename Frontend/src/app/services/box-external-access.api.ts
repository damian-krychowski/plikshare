import { Injectable } from "@angular/core";
import { BoxPermissions } from "./boxes.api";
import { HttpClient } from "@angular/common/http";
import { firstValueFrom } from "rxjs";
import { DataStore } from "./data-store.service";

@Injectable({
    providedIn: 'root'
})
export class BoxExternalAccessApi {
    constructor(
        private _http: HttpClient,
        private _dataStore: DataStore
    ) {        
    }

    public async leaveBoxMembership(boxExternalId: string) {
        const call = this
            ._http
            .delete<void>(
                `/api/boxes/${boxExternalId}`);

        await firstValueFrom(call);

        this._dataStore.invalidateEntries(
            key => key.startsWith(this._dataStore.externalBoxDetailsAndContentKey(boxExternalId, null))
        );
    }

    public async rejectBoxInvitation(externalId: string): Promise<void> {
        const call = this
            ._http
            .post<void>(
                `/api/boxes/${externalId}/reject-invitation`, {});

        return await firstValueFrom(call);
    }

    public async acceptBoxInvitation(invitationExternalId: string): Promise<void> {
        const call = this
            ._http
            .post<void>(
                `/api/boxes/${invitationExternalId}/accept-invitation`, {});

        await firstValueFrom(call);
    }
}