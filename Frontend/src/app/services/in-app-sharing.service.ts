import { Injectable } from "@angular/core";

@Injectable({
    providedIn: 'root',
  })
export class InAppSharing {
    private _store: Map<string, any> = new Map();

    public set(value: any): string {
        const temporaryKey = crypto.randomUUID();

        this._store.set(temporaryKey, value);

        return temporaryKey;
    }

    public pop(key: string | null) {
        if(key == null) {
            return null;
        }

        const value = this._store.get(key);

        if(value != null) {
            this._store.delete(key);
        }

        return value;
    }
}