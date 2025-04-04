import { HttpClient, HttpEvent, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom, Observable } from "rxjs";
import { UserPermissionsAndRolesDto } from "./users.api";

export type ApplicationSingUp = 'everyone' | 'only-invited-users';

export interface GetApplicationSettingsResponse {
    applicationSignUp: ApplicationSingUp;
    termsOfService: string | null;
    privacyPolicy: string | null;
    applicationName: string | null;
    signUpCheckboxes: SignUpCheckboxDto[];
    newUserDefaultMaxWorkspaceNumber: number | null;
    newUserDefaultMaxWorkspaceSizeInBytes: number | null;
    newUserDefaultPermissionsAndRoles: {
        isAdmin: boolean;
        canAddWorkspace: boolean;
        canManageGeneralSettings: boolean;
        canManageUsers: boolean;
        canManageStorages: boolean;
        canManageEmailProviders: boolean;
    },
    alertOnNewUserRegistered: boolean;
}

export interface SignUpCheckboxDto {
    id: number;
    text: string;
    isRequired: boolean;
}

export interface SetApplicationSignUpRequest {
    value: ApplicationSingUp;
}

export interface SetApplicationNameRequest {
    value: string | null;
}

export interface CreateOrUpdateSignUpCheckboxRequest {
    id: number | null;
    text: string;
    isRequired: boolean;
}

export interface CreateOrUpdateSignUpCheckboxResponse {
    newId: number;
}

export interface SetNewUserDefaultMaxWorkspaceNumberRequestDto {
    value: number | null;
}

export interface SetNewUserDefaultMaxWorkspaceSizeInBytesRequestDto {
    value: number | null;
}

export interface SetAlertSettingRequest {
    isTurnedOn: boolean;
}

@Injectable({
    providedIn: 'root'
})
export class GeneralSettingsApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async deleteSignUpCheckobx(id: number) {
        const response = this
            ._http
            .delete(`/api/general-settings/sign-up-checkboxes/${id}`);

        await firstValueFrom(response);
    }

    public async createOrUpdateSignUpCheckbox(request: CreateOrUpdateSignUpCheckboxRequest): Promise<CreateOrUpdateSignUpCheckboxResponse> {
        const response = this
            ._http
            .post<CreateOrUpdateSignUpCheckboxResponse>(
                `/api/general-settings/sign-up-checkboxes`, request);

        return await firstValueFrom(response);
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

    public async setNewUserDefaultMaxWorkspaceNumber(request: SetNewUserDefaultMaxWorkspaceNumberRequestDto) {
        const call = this
            ._http
            .patch(
                `/api/general-settings/new-user-default-max-workspace-number`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });
    
        await firstValueFrom(call);
    }
    
    public async setNewUserDefaultMaxWorkspaceSizeInBytes(request: SetNewUserDefaultMaxWorkspaceSizeInBytesRequestDto) {
        const call = this
            ._http
            .patch(
                `/api/general-settings/new-user-default-max-workspace-size-in-bytes`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });
    
        await firstValueFrom(call);
    }
    
    public async setNewUserDefaultPermissionsAndRoles(request: UserPermissionsAndRolesDto) {
        const call = this
            ._http
            .patch(
                `/api/general-settings/new-user-default-permissions-and-roles`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });
    
        await firstValueFrom(call);
    }

    public async setAlertOnNewUserRegistered(request: SetAlertSettingRequest) {
        const call = this
            ._http
            .patch(
                `/api/general-settings/alert-on-new-user-registered`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });
    
        await firstValueFrom(call);
    }
}