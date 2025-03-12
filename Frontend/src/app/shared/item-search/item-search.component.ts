import { Component, input, output, signal } from "@angular/core";
import { DebouncedChangeDirective } from "../debounced-change.directive";

export type ItemSearchCount = {
    allItems: number;
    matchingItems: number;
}

@Component({
    selector: 'app-item-search',
    imports: [
        DebouncedChangeDirective
    ],
    templateUrl: './item-search.component.html',
    styleUrls: ['./item-search.component.scss']
})
export class ItemSearchComponent  {
    phrase = input<string>('');
    count = input<ItemSearchCount>();
    debounce = input(50);
    minimalLength = input(1);

    searched = output<string>();

    isFocused = signal(false);

    onPerformSearch(phrase: string){
        if(phrase.length >= this.minimalLength()) {
            this.searched.emit(phrase);
        } else {
            this.searched.emit('');
        }
    }
}