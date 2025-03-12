import { Component, EffectRef, ElementRef, OnDestroy, Signal, ViewChild, computed, effect, signal } from "@angular/core";
import { SearchService } from "../../services/search.service";
import { DebouncedChangeDirective } from "../debounced-change.directive";
import { ActivatedRoute, Router } from "@angular/router";
import { MatButtonModule } from "@angular/material/button";
import { Subscription } from "rxjs";

@Component({
    selector: 'app-search-input',
    imports: [
        DebouncedChangeDirective,
        MatButtonModule
    ],
    host: {
        '[style.flex-grow]': 'isSearchActive() ? 1 : 0',
    },
    templateUrl: './search-input.component.html',
    styleUrl: './search-input.component.scss'
})
export class SearchInputComponent implements OnDestroy {
    isSearchActive = signal(false);
    displayedSearchPhrase = computed(() => {
        if(this.isSearchActive()) {
            return this.searchService.searchPhrase();
        } else {
            return '';
        }
    });

    private _performSearchEffect: EffectRef | null = null;
    private _subscription: Subscription;

    @ViewChild('searchInput') searchInput: ElementRef | null = null;

    constructor(
        public searchService: SearchService,
        private _activatedRoute: ActivatedRoute,
        private _router: Router
    ) {
        this._performSearchEffect = effect(() => {
            const phrase = this.searchService.searchPhrase();
            const isSearchActive = this.isSearchActive();
            
            this.updateQueryParams(isSearchActive, phrase);
        });

        this._subscription = this._activatedRoute.queryParams.subscribe(params => {
            const searchParam = params['search'];

            if(searchParam != null) {
                this.isSearchActive.set(true);
                this.searchService.searchPhrase.set(searchParam);
                this.performSearch(searchParam);
            } else {
                this.isSearchActive.set(false);
            }
        });
    }

    private updateQueryParams(isSearchActive: boolean, phrase: string): void {
        this._router.navigate([], {
          relativeTo: this._activatedRoute,
          queryParams: { search: isSearchActive ? phrase : null},
          queryParamsHandling: 'merge',
          replaceUrl: true // this prevents navigation history from filling up with query param changes
        });
    }

    ngOnDestroy(): void {
        this._performSearchEffect?.destroy();
        this._subscription?.unsubscribe();
    }

    performSearch(query: string) {                
        this.searchService.performSearch(query);
    }

    activateSearch() {
        this.isSearchActive.set(true);
    }

    deactivateSearch() {
        this.isSearchActive.set(false);
        this.searchService.clearSearchResults();
        setTimeout(() => this.searchInput?.nativeElement.blur());
    }
}