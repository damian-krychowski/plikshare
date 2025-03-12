import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface GetEmailProvidersResponse {
    items: GetEmailProvidersResponseItem[];
}

export interface GetEmailProvidersResponseItem {
    externalId: string,
    type: AppEmailProviderType,
    name: string,
    emailFrom: string,
    isConfirmed: boolean,
    isActive: boolean
}

export interface CreateSmtpEmailProviderRequest {
    name: string;
    emailFrom: string;
    hostname: string;
    port: number;
    sslMode: SmtpSslMode;
    username: string;
    password: string;
}

export interface CreateSmtpEmailProviderResponse {
    externalId: string;
}

export interface CreateAwsSesEmailProviderRequest {
    name: string;
    emailFrom: string;
    accessKey: string;
    secretAccessKey:string;
    region: string;
}

export interface CreateAwsSesEmailProviderResponse {
    externalId: string;
}

export interface CreateResendEmailProviderRequest {
    name: string;
    emailFrom: string;
    apiKey: string;
}

export interface CreateResendEmailProviderResponse {
    externalId: string;
}

export interface ResendConfirmationEmailRequest {
    emailTo: string;
}

export interface ConfirmEmailProviderRequest {
    confirmationCode: string;
}

export type AppEmailProviderType = 'aws-ses' | 'resend' | 'smtp';

export interface UpdateEmailProviderNameRequest {
    name: string;
}

export type SmtpSslMode = 'none' | 'auto' | 'sslOnConnect' | 'startTls' | 'startTlsWhenAvailable'

@Injectable({
    providedIn: 'root'
})
export class EmailProvidersApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async getEmailProviders(): Promise<GetEmailProvidersResponse> {
        const call = this
            ._http
            .get<GetEmailProvidersResponse>(
                `/api/email-providers`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async createAwsSesEmailProvider(request: CreateAwsSesEmailProviderRequest): Promise<CreateAwsSesEmailProviderResponse> {
        const call = this
            ._http
            .post<CreateAwsSesEmailProviderResponse>(
                `/api/email-providers/aws-ses`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async createResendEmailProvider(request: CreateResendEmailProviderRequest): Promise<CreateResendEmailProviderResponse> {
        const call = this
            ._http
            .post<CreateResendEmailProviderResponse>(
                `/api/email-providers/resend`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async createSmtpEmailProvider(request: CreateSmtpEmailProviderRequest): Promise<CreateSmtpEmailProviderResponse> {
        const call = this
            ._http
            .post<CreateSmtpEmailProviderResponse>(
                `/api/email-providers/smtp`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async deleteEmailProvider(externalId: string) {
        const call = this
            ._http
            .delete(
                `/api/email-providers/${externalId}`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

         await firstValueFrom(call);
    }

    public async resendConfirmationEmail(externalId: string, request: ResendConfirmationEmailRequest): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/email-providers/${externalId}/resend-confirmation-email`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async confirm(externalId: string, request: ConfirmEmailProviderRequest): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/email-providers/${externalId}/confirm`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async activate(externalId: string): Promise<void> {
        const call = this
        ._http
        .post(
            `/api/email-providers/${externalId}/activate`, {}, {
            headers: new HttpHeaders({
                'Content-Type':  'application/json'
            })
        });

        await firstValueFrom(call);
    }

    public async deactivate(externalId: string): Promise<void> {
        const call = this
        ._http
        .post(
            `/api/email-providers/${externalId}/deactivate`, {}, {
            headers: new HttpHeaders({
                'Content-Type':  'application/json'
            })
        });

        await firstValueFrom(call);
    }

    public async updateName(externalId: string, request: UpdateEmailProviderNameRequest): Promise<void> {
        const call = this
        ._http
        .patch(
            `/api/email-providers/${externalId}/name`, request, {
            headers: new HttpHeaders({
                'Content-Type':  'application/json'
            })
        });

        await firstValueFrom(call);
    }
}