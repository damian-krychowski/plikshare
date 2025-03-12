import { Injectable, signal } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { AppFileItem } from '../shared/file-item/file-item.component';
import { LockStatusApi } from './lock-status.api';

@Injectable({
    providedIn: 'root'
})
export class FileLockService {
    private lockedFiles = signal<Set<string>>(new Set());
    private subscriptions = new Map<string, AppFileItem>();
    private pollingSubscription: Subscription | null = null;

    constructor(
        private _api: LockStatusApi
    ) {
        this.startPolling();
    }

    subscribeToLockStatus(file: AppFileItem) {
        if(!file.isLocked())
            return;

        const fileId = file.externalId;

        if (!this.subscriptions.has(fileId)) {
            this.subscriptions.set(fileId, file);

            this.lockedFiles.update(files => {
                files.add(fileId);
                return files;
            });
        }
    }

    unsubscribe(fileId: string) {
        this.subscriptions.delete(fileId);
        this.lockedFiles.update(files => {
            files.delete(fileId);
            return files;
        });
    }

    private async checkLockStatus() {
        if (this.subscriptions.size === 0) return;

        try {
            const fileIds = Array.from(this.subscriptions.keys());

            const response = await this
                ._api
                .checkFileLocks({
                    externalIds: fileIds
                });

            this.lockedFiles.set(new Set(response.lockedExternalIds));

            // Update individual file items and unsubscribe unlocked files
            this.subscriptions.forEach((file, id) => {
                const isLocked = response.lockedExternalIds.includes(id);
                file.isLocked.set(isLocked);
                
                // If file is no longer locked, remove it from subscriptions
                if (!isLocked) {
                    this.unsubscribe(id);
                }
            });
        } catch (error) {
            console.error('Failed to check lock status:', error);
        }
    }

    private startPolling(intervalMs: number = 1000) {
        this.pollingSubscription = interval(intervalMs)
            .subscribe(() => this.checkLockStatus());
    }

    ngOnDestroy() {
        this.pollingSubscription?.unsubscribe();
    }
}