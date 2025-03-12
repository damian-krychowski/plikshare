import { computed, Signal, WritableSignal } from "@angular/core";

export function observeIsHighlighted(itemSignal: Signal<{isHighlighted: WritableSignal<boolean>}>): Signal<boolean> {
    return computed(() => {
        const item = itemSignal();

        if(!item) return false;

        const isHighlighted = item.isHighlighted();

        if(isHighlighted) {
            setTimeout(() => item.isHighlighted.set(false), 5000);
        }

        return isHighlighted;
    });
}