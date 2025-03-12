import { interval, Subscription } from 'rxjs';
import { AppTextractJobItem } from '../shared/app-textract-job-item';
import { CheckTextractJobsStatusRequest, CheckTextractJobsStatusResponse } from './folders-and-files.api';
import { Signal } from '@angular/core';

export interface TextractJobStatusServiceApi {
    checkTextractJobsStatus(request: CheckTextractJobsStatusRequest): Promise<CheckTextractJobsStatusResponse>;
}

export class TextractJobStatusService {
    private subscriptions = new Map<string, AppTextractJobItem>();
    private pollingSubscription: Subscription | null = null;

    constructor(
        public api: Signal<TextractJobStatusServiceApi>,
    ) {
        this.startPolling();
    }

    subscribeToStatusCheck(textractJob: AppTextractJobItem) {
        if(textractJob.status() == 'completed' || textractJob.status() == 'partially-completed')
            return;

        const textractJobId = textractJob.externalId;

        if (!this.subscriptions.has(textractJobId)) {
            this.subscriptions.set(textractJobId, textractJob);
        }
    }

    unsubscribe(textractJobExternalId: string) {
        this.subscriptions.delete(textractJobExternalId);
    }

    private async checkStatus() {
        if (this.subscriptions.size === 0) return;

        try {
            const textractJobExternalIds = Array.from(this.subscriptions.keys());

            const response = await this
                .api()
                .checkTextractJobsStatus({
                    externalIds: textractJobExternalIds
                });

            for (const responseItem of response.items) {
                const job = this.subscriptions.get(responseItem.externalId);

                if(job) {
                    job.status.set(responseItem.status);

                    if(responseItem.status == 'completed' || responseItem.status == 'partially-completed') {
                        this.unsubscribe(responseItem.externalId);
                        job.onCompletedHandler();
                    }
                }
            }
        } catch (error) {
            console.error('Failed to check lock status:', error);
        }
    }

    private startPolling(intervalMs: number = 1000) {
        this.pollingSubscription = interval(intervalMs)
            .subscribe(() => this.checkStatus());
    }

    dispose() {
        this.pollingSubscription?.unsubscribe();
    }
}