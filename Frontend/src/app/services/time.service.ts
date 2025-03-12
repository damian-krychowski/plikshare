import { Injectable, NgZone, computed, signal } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class TimeService {
    private readonly _currentTime = signal<number>(Date.now());
    readonly currentTime = computed(() => this._currentTime());
    
    constructor(private _ngZone: NgZone) {
        // Run outside Angular zone to avoid unnecessary change detection
        this._ngZone.runOutsideAngular(() => {
            setInterval(() => {
                // Re-enter Angular zone when updating the signal
                this._ngZone.run(() => {
                    this._currentTime.set(Date.now());
                });
            }, 1000);
        });
    }
}

export function getRelativeTime(dateString: string): string {
    const date = new Date(dateString);
    const now = new Date();
    const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000);
    
    // Less than a minute
    if (diffInSeconds < 60) {
        return 'just now';
    }
    
    // Less than an hour
    const minutes = Math.floor(diffInSeconds / 60);
    if (minutes < 60) {
        return `${minutes} ${minutes === 1 ? 'minute' : 'minutes'} ago`;
    }
    
    // Less than a day
    const hours = Math.floor(minutes / 60);
    if (hours < 24) {
        return `${hours} ${hours === 1 ? 'hour' : 'hours'} ago`;
    }
    
    // Less than a week
    const days = Math.floor(hours / 24);
    if (days < 7) {
        return `${days} ${days === 1 ? 'day' : 'days'} ago`;
    }
    
    // Less than a month
    const weeks = Math.floor(days / 7);
    if (weeks < 4) {
        return `${weeks} ${weeks === 1 ? 'week' : 'weeks'} ago`;
    }
    
    // Less than a year
    const months = Math.floor(days / 30);
    if (months < 12) {
        return `${months} ${months === 1 ? 'month' : 'months'} ago`;
    }
    
    // A year or more
    const years = Math.floor(days / 365);
    return `${years} ${years === 1 ? 'year' : 'years'} ago`;
}