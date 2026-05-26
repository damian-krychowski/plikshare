import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type AppCapabilities = {
    isFfmpegAvailable: boolean;
};

/**
 * Single-fetch, app-wide capabilities surfaced from the backend (ffmpeg presence, …).
 * Components inject the service and read the signal — initial value is conservative
 * (`isFfmpegAvailable: false`) so UI hides gated features until the load resolves.
 * The first <see cref="ensureLoaded"/> call fires the HTTP request; subsequent calls
 * reuse the cached promise — safe to invoke from many component init paths.
 */
@Injectable({
    providedIn: 'root'
})
export class AppCapabilitiesService {
    private readonly _capabilities = signal<AppCapabilities>({
        isFfmpegAvailable: false,
    });
    
    private _loadPromise: Promise<void> | null = null;

    public readonly capabilities = this._capabilities.asReadonly();

    constructor(private _http: HttpClient) {}

    public ensureLoaded(): Promise<void> {
        if (this._loadPromise) return this._loadPromise;

        this._loadPromise = (async () => {
            try {
                const result = await firstValueFrom(
                    this._http.get<AppCapabilities>('/api/app-capabilities')
                );
                this._capabilities.set(result);
            } catch (err) {
                console.error('Failed to load app capabilities:', err);
                // Conservative defaults stand — next ensureLoaded retry will only run
                // if a future caller clears _loadPromise; not needed for current flow.
            }
        })();

        return this._loadPromise;
    }
}
