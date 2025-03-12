import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export type AppIntegrationType = 'awsTextract';

export interface GetIntegrationsResponse {
    items: IntegrationDto[];
}

export interface IntegrationDto {
    name: string;
    externalId: string;
    type: AppIntegrationType;
    isActive: boolean;
    workspace: {
        externalId: string;
        name: string
    }
}

export type CreateIntegrationRequest = CreateAwsTextractIntegrationRequest | CreateChatGptIntegrationRequest;

export type CreateAwsTextractIntegrationRequest = {
    $type: 'aws-textract';
    name: string;

    accessKey: string;
    secretAccessKey: string;
    region: string;
    storageExternalId: string;
}

export type CreateChatGptIntegrationRequest = {
    $type: 'openai-chatgpt';
    name: string;
    apiKey: string;
    storageExternalId: string;
}

export interface CreateIntegrationResponse {
    externalId: string;
}

export interface UpdateIntegrationNameRequest {
    name: string;
}

@Injectable({
    providedIn: 'root'
})
export class IntegrationsApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async getIntegrations(): Promise<GetIntegrationsResponse> {
        const call = this
            ._http
            .get<GetIntegrationsResponse>(
                `/api/integrations`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async createIntegration(request: CreateIntegrationRequest): Promise<CreateIntegrationResponse> {
        const call = this
            ._http
            .post<CreateIntegrationResponse>(
                `/api/integrations`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async deleteIntegration(externalId: string) {
        const call = this
            ._http
            .delete(
                `/api/integrations/${externalId}`, {
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
                `/api/integrations/${externalId}/activate`, {}, {
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
                `/api/integrations/${externalId}/deactivate`, {}, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }

    public async updateName(externalId: string, request: UpdateIntegrationNameRequest): Promise<void> {
        const call = this
            ._http
            .patch(
                `/api/integrations/${externalId}/name`, request, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }
}