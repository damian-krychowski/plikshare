import { interval, Subscription } from 'rxjs';
import { AppFileItem } from '../shared/file-item/file-item.component';

export class BoxLinkFileLockService {
    private subscriptions = new Map<string, AppFileItem>();
    private pollingSubscription: Subscription | null = null;

    constructor(
        private _checkFileLocks: (externalIds: string[]) => Promise<string[]>
    ) {
    }

    subscribeToLockStatus(file: AppFileItem) {
        if (!file.isLocked())
            return;

        const fileId = file.externalId;

        if (!this.subscriptions.has(fileId)) {
            this.subscriptions.set(fileId, file);
        }
    }

    unsubscribe(fileId: string) {
        this.subscriptions.delete(fileId);
    }

    startPolling(intervalMs: number = 1000) {
        this.pollingSubscription = interval(intervalMs)
            .subscribe(() => this.checkLockStatus());
    }

    stopPolling() {
        this.pollingSubscription?.unsubscribe();
    }

    private async checkLockStatus() {
        if (this.subscriptions.size === 0)
            return;

        try {
            const fileIds = Array.from(this.subscriptions.keys());
            const lockedExternalIds = await this._checkFileLocks(fileIds);
            const lockedSet = new Set(lockedExternalIds);

            this.subscriptions.forEach((file, id) => {
                const isLocked = lockedSet.has(id);
                file.isLocked.set(isLocked);

                if (!isLocked) {
                    this.unsubscribe(id);
                }
            });
        } catch (error) {
            console.error('Failed to check lock status:', error);
        }
    }
}
