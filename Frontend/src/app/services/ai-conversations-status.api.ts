import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface CheckAiConversationStatusResponse {
    conversationsWithNewMessages: string[];
}

export interface CheckAiConversationStatusRequest {
    conversations: AiConversationStateDto[];
}

export interface AiConversationStateDto {
    externalId: string;
    conversationCounter: number;
}

@Injectable({
    providedIn: 'root'
})
export class AiConversationsStatusApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async checkConversationsStatus(request: CheckAiConversationStatusRequest): Promise<CheckAiConversationStatusResponse> {
        const call = this
            ._http
            .post<CheckAiConversationStatusResponse>(
                `/api/ai/conversations/check-status`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }
}