import { Injectable, computed, signal } from '@angular/core';
import { FullEncryptionSessionItem, FullEncryptionSessionsApi } from './full-encryption-sessions.api';

@Injectable({
    providedIn: 'root'
})
export class FullEncryptionSessionsStore {
    private _items = signal<FullEncryptionSessionItem[]>([]);
    private _isLoaded = signal(false);

    readonly items = this._items.asReadonly();
    readonly count = computed(() => this._items().length);
    readonly isLoaded = this._isLoaded.asReadonly();

    constructor(private _api: FullEncryptionSessionsApi) {
    }

    async load(): Promise<void> {
        const response = await this._api.getAll();
        this._items.set(response.items);
        this._isLoaded.set(true);
    }

    async ensureLoaded(): Promise<void> {
        if (this._isLoaded())
            return;

        await this.load();
    }

    async notifyUnlocked(): Promise<void> {
        await this.load();
    }

    async lock(storageExternalId: string): Promise<void> {
        await this._api.lock(storageExternalId);
        this._items.update(items => items.filter(i => i.storageExternalId !== storageExternalId));
    }

    async lockAll(): Promise<void> {
        await this._api.lockAll();
        this._items.set([]);
    }
}
