import { Component, OnChanges, OnInit, SimpleChanges, input, output } from "@angular/core";
import { MatSelectModule } from "@angular/material/select";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { FormsModule } from "@angular/forms";
import { BoxDefaultDisplayConfiguration } from "../../../../services/boxes.api";
import { ViewMode } from "../../../../files-explorer/display-menu/display-menu.component";
import { SortDirection, SortMode } from "../../../../services/folders-and-files.api";

type SortChoice = 'custom' | 'name-asc' | 'name-desc';

@Component({
    selector: 'app-box-default-display-config',
    standalone: true,
    imports: [
        MatSelectModule,
        MatSlideToggleModule,
        FormsModule
    ],
    templateUrl: './box-default-display-config.component.html',
    styleUrl: './box-default-display-config.component.scss'
})
export class BoxDefaultDisplayConfigComponent implements OnInit, OnChanges {
    configuration = input.required<BoxDefaultDisplayConfiguration>();
    configChanged = output<BoxDefaultDisplayConfiguration>();

    viewModeOptions: { value: ViewMode, label: string }[] = [
        { value: 'list-view', label: 'List view' },
        { value: 'tree-view', label: 'Tree view' }
    ];
    selectedViewMode: ViewMode = 'list-view';

    sortOptions: { value: SortChoice, label: string }[] = [
        { value: 'custom', label: 'Custom order' },
        { value: 'name-asc', label: 'Name (A–Z)' },
        { value: 'name-desc', label: 'Name (Z–A)' }
    ];
    selectedSort: SortChoice = 'custom';

    thumbnailsEnabled: boolean = false;

    ngOnInit() {
        this.initialize();
    }

    ngOnChanges(changes: SimpleChanges) {
        if (changes['configuration']) {
            this.initialize();
        }
    }

    private initialize() {
        const cfg = this.configuration();
        this.selectedViewMode = cfg.viewMode === 'tree-view' ? 'tree-view' : 'list-view';
        this.selectedSort = this.toSortChoice(cfg.sortMode, cfg.sortDirection);
        this.thumbnailsEnabled = cfg.thumbnailsEnabled;
    }

    private toSortChoice(mode: SortMode, direction: SortDirection): SortChoice {
        if (mode === 'name')
            return direction === 'desc' ? 'name-desc' : 'name-asc';

        return 'custom';
    }

    private fromSortChoice(choice: SortChoice): { sortMode: SortMode, sortDirection: SortDirection } {
        if (choice === 'name-asc')
            return { sortMode: 'name', sortDirection: 'asc' };

        if (choice === 'name-desc')
            return { sortMode: 'name', sortDirection: 'desc' };

        return { sortMode: 'custom', sortDirection: 'asc' };
    }

    onChange() {
        const { sortMode, sortDirection } = this.fromSortChoice(this.selectedSort);

        this.configChanged.emit({
            viewMode: this.selectedViewMode,
            sortMode,
            sortDirection,
            thumbnailsEnabled: this.thumbnailsEnabled
        });
    }
}
