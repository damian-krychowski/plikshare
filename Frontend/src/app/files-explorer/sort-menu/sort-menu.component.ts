import { Component, computed, ElementRef, HostListener, input, output, signal } from '@angular/core';
import { SortDirection, SortMode } from '../../services/folders-and-files.api';

export type SortChange = {
    mode: SortMode;
    direction: SortDirection;
};

@Component({
    selector: 'app-sort-menu',
    standalone: true,
    templateUrl: './sort-menu.component.html',
    styleUrl: './sort-menu.component.scss'
})
export class SortMenuComponent {
    mode = input.required<SortMode>();
    direction = input.required<SortDirection>();
    allowDateSort = input(false);

    sortChanged = output<SortChange>();

    isOpen = signal(false);

    label = computed(() => {
        const m = this.mode();
        const d = this.direction();
        if (m === 'custom') return 'Custom';
        const arrow = d === 'asc' ? '↑' : '↓';
        return m === 'name' ? `Name ${arrow}` : `Date ${arrow}`;
    });

    constructor(private _el: ElementRef<HTMLElement>) {}

    toggle() {
        this.isOpen.update(v => !v);
    }

    select(mode: SortMode, direction: SortDirection) {
        if (mode === 'date' && !this.allowDateSort()) return;
        this.sortChanged.emit({ mode, direction });
        this.isOpen.set(false);
    }

    isActive(mode: SortMode, direction: SortDirection): boolean {
        if (this.mode() !== mode) return false;
        if (mode === 'custom') return true;
        return this.direction() === direction;
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
