import { Component, computed, ElementRef, HostListener, input, output, signal } from '@angular/core';
import { SortDirection, SortMode } from '../../services/folders-and-files.api';
import { SortChange } from '../sort-menu/sort-menu.component';

// Mobile-only counterpart of the inline View toggle + sort-menu pair from the
// context bar. Combines both behind a single trigger so the mobile toolbar
// stays compact. Desktop keeps using the inline controls — this menu is
// hidden via d-none-on-desktop.
export type ViewMode = 'list-view' | 'tree-view';

@Component({
    selector: 'app-display-menu',
    standalone: true,
    templateUrl: './display-menu.component.html',
    styleUrl: './display-menu.component.scss'
})
export class DisplayMenuComponent {d
    viewMode = input.required<ViewMode>();
    sortMode = input.required<SortMode>();
    sortDirection = input.required<SortDirection>();
    allowDateSort = input(false);
    allowList = input(true);
    showThumbnails = input(false);
    allowThumbnails = input(false);

    viewModeChanged = output<ViewMode>();
    sortChanged = output<SortChange>();
    showThumbnailsChanged = output<boolean>();

    isOpen = signal(false);

    sortLabel = computed(() => {
        const m = this.sortMode();
        const d = this.sortDirection();
        if (m === 'custom') return 'Custom';
        const arrow = d === 'asc' ? '↑' : '↓';
        return m === 'name' ? `Name ${arrow}` : `Date ${arrow}`;
    });

    viewLabel = computed(() => this.viewMode() === 'list-view' ? 'List' : 'Tree');

    triggerLabel = computed(() => this.allowList()
        ? `${this.viewLabel()} · ${this.sortLabel()}`
        : this.sortLabel());

    constructor(private _el: ElementRef<HTMLElement>) {}

    toggle() {
        this.isOpen.update(v => !v);
    }

    selectView(mode: ViewMode) {
        this.viewModeChanged.emit(mode);
        this.isOpen.set(false);
    }

    selectSort(mode: SortMode, direction: SortDirection) {
        if (mode === 'date' && !this.allowDateSort()) return;
        this.sortChanged.emit({ mode, direction });
        this.isOpen.set(false);
    }

    toggleThumbnails() {
        this.showThumbnailsChanged.emit(!this.showThumbnails());
        // Keep the menu open — toggling is a setting the user may flip back and forth.
    }

    isViewActive(mode: ViewMode): boolean {
        return this.viewMode() === mode;
    }

    isSortActive(mode: SortMode, direction: SortDirection): boolean {
        if (this.sortMode() !== mode) return false;
        if (mode === 'custom') return true;
        return this.sortDirection() === direction;
    }

    @HostListener('document:click', ['$event'])
    onDocumentClick(event: MouseEvent) {
        if (!this.isOpen()) return;
        const target = event.target as Node;
        if (!this._el.nativeElement.contains(target)) {
            this.isOpen.set(false);
        }
    }
}
