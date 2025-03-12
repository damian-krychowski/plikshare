import { signal } from "@angular/core";

export class Debouncer {
    constructor(
        private _milliseconds: number){
    }

    private debounceTimer: any | null;
    
    isOn = signal(false);

    public debounce(action: () => any) {
        if(this.debounceTimer)
            clearTimeout(this.debounceTimer);

        this.debounceTimer = setTimeout(
            () => {
                action();
                this.isOn.set(false);
            }, 
            this._milliseconds);

        this.isOn.set(true);
    }

    public debounceAsync(action: () => Promise<any>) {
        if(this.debounceTimer)
            clearTimeout(this.debounceTimer);

        this.debounceTimer = setTimeout(
            async () => {
                try {
                    await action();
                } finally {
                    this.isOn.set(false);
                }
            }, 
            this._milliseconds);

        this.isOn.set(true);
    }

    public cancel() {
        if(this.debounceTimer)
            clearTimeout(this.debounceTimer)


        this.isOn.set(false);
    }
}