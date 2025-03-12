import { signal, WritableSignal } from "@angular/core";
import { Subscription, takeWhile, timer } from "rxjs";

export class Countdown {
    secondsLeft: WritableSignal<number>;

    private _subscription: Subscription | null = null;
    
    constructor(
        private _seconds: number) {
        this.secondsLeft = signal(_seconds);
    }
    
    start() {
        if(this._subscription){
            this._subscription.unsubscribe();
            this._subscription = null;
        }

        this.secondsLeft.set(this._seconds);

        // timer() will emit an incrementing number each second
        this._subscription = timer(0, 1000).pipe(
            // i is emitted by timer(), basically the number of elapsed seconds.
            takeWhile((i) => i <= this._seconds),
        ).subscribe((i) => {
            this.secondsLeft.set(this._seconds - i);
        });
    }

    clear() {
        if(this._subscription){
            this._subscription.unsubscribe();
            this._subscription = null;
        }

        this.secondsLeft.set(0);
    }
}   