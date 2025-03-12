import { Signal, WritableSignal } from "@angular/core";

export function toggle(signal: WritableSignal<boolean>): boolean {
    signal.update(value => !value);
    return signal();
}

export function indexOf<TItem>(signal: Signal<TItem[]>, item: TItem): number {
    return signal().indexOf(item);
}

export function removeItem<TItem>(signal: WritableSignal<TItem[]>, item: TItem): { index: number} {
    const items = signal();
    const index = items.indexOf(item);

    signal.update(values => {
        values.splice(index, 1);
        return [...values];
    });

    return {
        index: index
    };
}

export function removeItems<TItem>(signal: WritableSignal<TItem[]>, ...items: TItem[]) {
    signal.update(values => values.filter(v => !items.some(i => v === i)))
}

export function insertItem<TItem>(signal: WritableSignal<TItem[]>, item: TItem, index: number) {
    signal.update(values => {
        values.splice(index, 0, item);
        return [...values];
    });
}

export function pushItems<TItem>(signal: WritableSignal<TItem[]>, ...items: TItem[]) {
    signal.update(values => [...values, ...items]);
}

export function unshiftItems<TItem>(signal: WritableSignal<TItem[]>, ...items: TItem[]) {
    signal.update(values => [...items, ...values]);
}

export function containsItem<TItem>(signal: Signal<TItem[]>, item: TItem): boolean {
    return signal().indexOf(item) !== -1;
}