import { AfterViewInit, Component, ElementRef, input, output, signal, ViewChild } from "@angular/core";
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
    styleUrls: ['./item-search.component.scss'],
    host: {
        '[class.is-mobile-collapsible]': 'collapsibleOnMobile()',
        '[class.is-mobile-expanded]': 'isExpanded()',
        '[class.is-full-width-on-mobile]': 'fullWidthOnMobile()'
    }
})
export class ItemSearchComponent implements AfterViewInit {
    phrase = input<string>('');
    count = input<ItemSearchCount>();
    debounce = input(50);
    minimalLength = input(1);

    // Opt-in: when true, the component is hidden on mobile until `isExpanded`
    // is set by the parent (typically by a magnifier action button next to
    // the other toolbar actions). Desktop ignores both flags.
    collapsibleOnMobile = input(false);
    isExpanded = input(false);

    // Opt-in: on mobile the input stretches to fill its host and may shrink
    // below its natural width, so it never overflows a tight toolbar row.
    fullWidthOnMobile = input(false);

    searched = output<string>();
    closed = output<void>();

    isFocused = signal(false);

    @ViewChild('searchInput') searchInput?: ElementRef<HTMLInputElement>;

    ngAfterViewInit() {
        // No-op; ViewChild used externally via focus() through parent's effect.
    }

    onPerformSearch(phrase: string){
        if(phrase.length >= this.minimalLength()) {
            this.searched.emit(phrase);
        } else {
            this.searched.emit('');
        }
    }

    onCloseClicked() {
        this.searched.emit('');
        this.closed.emit();
    }

    focusInput() {
        setTimeout(() => this.searchInput?.nativeElement.focus(), 0);
    }
}