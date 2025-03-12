import { computed, Signal, WritableSignal } from "@angular/core";
import { TextractFeature, TextractJobStatus } from "../services/folders-and-files.api";

export type AppTextractJobItem = {
    type: 'textract-job';
    externalId: string;
    status: WritableSignal<TextractJobStatus | null>;
    features: TextractFeature[];
    progressPercentage: Signal<number>;

    onCompletedHandler: () => void;
}

export class AppTextractJobItemExtensions {
    public static getProgressPercentageSignal(status: WritableSignal<TextractJobStatus | null>): Signal<number>{
        return computed(() => {
            const statusVal = status();
    
            if(!statusVal)
                return 0;
    
            switch(statusVal) {
                case 'waits-for-file': return 20;
                case 'pending': return 40;
                case 'processing': return 60;
                case 'downloading-results': return 80;
                case 'completed': return 100;
                case 'partially-completed': return 100;
                case 'failed': return 100;
            }
        });
    }
}