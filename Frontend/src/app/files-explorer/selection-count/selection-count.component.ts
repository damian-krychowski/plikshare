import { Component, input, output } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';

// Presentational counter for a list section header: shows "visible/all" (or just "all" when not
// filtered) plus a selected badge, each segment separately clickable. Holds no selection logic —
// emits intents; the parent owns the actual select/clear.
@Component({
    selector: 'app-selection-count',
    standalone: true,
    imports: [MatTooltipModule],
    templateUrl: './selection-count.component.html',
    styleUrl: './selection-count.component.scss'
})
export class SelectionCountComponent {
    visibleCount = input.required<number>();
    totalCount = input.required<number>();
    selectedCount = input.required<number>();
    isFiltered = input(false);
    noun = input.required<string>();

    selectVisible = output<void>();
    selectAll = output<void>();
    clearSelection = output<void>();
}
