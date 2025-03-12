import { Injectable, OnDestroy } from '@angular/core';
import { interval, Subject, Subscription, takeUntil } from 'rxjs';
import { AiConversationsStatusApi } from './ai-conversations-status.api';
import { AppAiConversation } from '../shared/ai-conversation-item/ai-conversation-item.component';

// Simple interface for tracking subscriptions
interface ConversationSubscription {
    externalId: string;
    conversationCounter: number;
    callback: (externalId: string) => void;
}

@Injectable({
    providedIn: 'root'
})
export class AiConversationsStatusService implements OnDestroy {
    private subscriptions = new Map<string, ConversationSubscription>();
    private destroy$ = new Subject<void>();
    private pollingSubscription: Subscription;

    constructor(private _api: AiConversationsStatusApi) {
        // Set up the polling interval
        this.pollingSubscription = interval(1000)
            .pipe(takeUntil(this.destroy$))
            .subscribe(() => this.checkConversationsStatus());
    }

    /**
     * Subscribe to status updates for a conversation
     * @param externalId The conversation external ID
     * @param conversationCounter The conversation counter
     * @param callback Function to be called when status changes
     * @returns A Subscription object that can be used to unsubscribe
     */
    subscribe(
        conversation: AppAiConversation,
        callback: (externalId: string) => void
    ): Subscription | null {
        if(conversation.status() != 'waits-for-ai-response')
            return null;

        const externalId = conversation.aiConversationExternalId;

        this.subscriptions.set(externalId, {
            externalId: externalId,
            conversationCounter: conversation.conversationCounter(),
            callback
        });
        
        return new Subscription(() => {
            this.subscriptions.delete(externalId);
        });
    }

    /**
     * Check conversation status and notify callbacks
     */
    private async checkConversationsStatus(): Promise<void> {
        if (this.subscriptions.size === 0) return;

        try {
            const subscriptionEntries = Array.from(this.subscriptions.entries());

            // Build the request payload with just the necessary information
            const requestPayload = {
                conversations: subscriptionEntries.map(([_, sub]) => ({
                    conversationCounter: sub.conversationCounter,
                    externalId: sub.externalId
                }))
            };

            // Make the API call
            const response = await this._api.checkConversationsStatus(requestPayload);

            // Process the response and notify callbacks only when there are new messages
            for (const [externalId, subscription] of subscriptionEntries) {
                const hasNewMessages = response.conversationsWithNewMessages.includes(externalId);

                // Only call the callback if there are new messages
                if (hasNewMessages) {
                    // Call the callback with the externalId
                    subscription.callback(externalId);

                    // Automatically unsubscribe
                    this.subscriptions.delete(externalId);
                }
            }
        } catch (error) {
            console.error('Failed to check AI conversations status:', error);
        }
    }

    /**
     * Clean up resources when the service is destroyed
     */
    ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();

        if (this.pollingSubscription) {
            this.pollingSubscription.unsubscribe();
        }
    }
}