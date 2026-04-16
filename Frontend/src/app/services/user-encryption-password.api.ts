import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface SetupEncryptionPasswordRequest {
    encryptionPassword: string;
}

export interface SetupEncryptionPasswordResponse {
    recoveryCode: string;
}

export interface UnlockEncryptionPasswordRequest {
    encryptionPassword: string;
}

export interface ChangeEncryptionPasswordRequest {
    oldPassword: string;
    newPassword: string;
}

export interface ResetEncryptionPasswordRequest {
    recoveryCode: string;
    newPassword: string;
}

@Injectable({
    providedIn: 'root'
})
export class UserEncryptionPasswordApi {
    constructor(private _http: HttpClient) {
    }

    public async setup(request: SetupEncryptionPasswordRequest): Promise<SetupEncryptionPasswordResponse> {
        const call = this
            ._http
            .post<SetupEncryptionPasswordResponse>(
                `/api/user-encryption-password/setup`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async unlock(request: UnlockEncryptionPasswordRequest): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/user-encryption-password/unlock`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async lock(): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/user-encryption-password/lock`, {}, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async change(request: ChangeEncryptionPasswordRequest): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/user-encryption-password/change`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async reset(request: ResetEncryptionPasswordRequest): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/user-encryption-password/reset`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        await firstValueFrom(call);
    }
}
