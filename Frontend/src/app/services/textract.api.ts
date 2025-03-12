import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface TestTextractConfigurationRequest {
    accessKey: string;
    secretAccessKey: string;
    region: string;
    storageExternalId: string;
}

export interface TestTextractConfigurationResponse {
    code: TestTextractConfigurationResultCode;
    detectedLines: string[];
}

export type TestTextractConfigurationResultCode = 'ok';

@Injectable({
    providedIn: 'root'
})
export class TextractApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async getTestImage(): Promise<Blob> {
        const call = this
            ._http
            .get(
                `/api/integrations/aws-textract/test-image`, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                }),
                responseType: 'blob'
            });

        return await firstValueFrom(call);
    }
    
    public async testConfiguration(request: TestTextractConfigurationRequest): Promise<TestTextractConfigurationResponse> {
        const call = this
            ._http
            .post<TestTextractConfigurationResponse>(
                `/api/integrations/aws-textract/test-configuration`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                }),
            });

        return await firstValueFrom(call);
    }
}