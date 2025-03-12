import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface TestChatGptConfigurationRequest {
    apiKey: string;
}

export interface TestChatGptConfigurationResponse {
    haiku: string;
}

@Injectable({
    providedIn: 'root'
})
export class ChatGptApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async testConfiguration(request: TestChatGptConfigurationRequest): Promise<TestChatGptConfigurationResponse> {
        const call = this
            ._http
            .post<TestChatGptConfigurationResponse>(
                `/api/integrations/openai-chatgpt/test-configuration`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                }),
            });

        return await firstValueFrom(call);
    }
}