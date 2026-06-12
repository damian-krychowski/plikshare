import { Component, OnChanges, OnInit, SimpleChanges, input, output } from "@angular/core";
import { MatSelectModule } from "@angular/material/select";
import { FormsModule } from "@angular/forms";
import { BoxDefaultDisplayConfiguration } from "../../../../services/boxes.api";
import { ViewMode } from "../../../../files-explorer/display-menu/display-menu.component";
import { SortDirection, SortMode } from "../../../../services/folders-and-files.api";
import { GalleryLayoutMode, GalleryTileSize } from "../../../../files-explorer/files-gallery/files-gallery.component";

type SortChoice = 'custom' | 'name-asc' | 'name-desc';

@Component({
    selector: 'app-box-default-display-config',
    standalone: true,
    imports: [
        MatSelectModule,
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
        { value: 'tree-view', label: 'Tree view' },
        { value: 'gallery-view', label: 'Gallery view' }
    ];
    selectedViewMode: ViewMode = 'list-view';

    sortOptions: { value: SortChoice, label: string }[] = [
        { value: 'custom', label: 'Custom' },
        { value: 'name-asc', label: 'Name ↑' },
        { value: 'name-desc', label: 'Name ↓' }
    ];
    selectedSort: SortChoice = 'custom';

    visibilityOptions: { value: boolean, label: string }[] = [
        { value: false, label: 'Hidden' },
        { value: true, label: 'Visible' }
    ];
    selectedThumbnails: boolean = false;
    selectedMinimap: boolean = false;

    galleryLayoutOptions: { value: GalleryLayoutMode, label: string }[] = [
        { value: 'justified', label: 'Justified' },
        { value: 'mosaic', label: 'Mosaic' },
        { value: 'grid', label: 'Grid' }
    ];
    selectedGalleryLayout: GalleryLayoutMode = 'justified';

    galleryTileSizeOptions: { value: GalleryTileSize, label: string }[] = [
        { value: 'small', label: 'Small' },
        { value: 'medium', label: 'Medium' },
        { value: 'large', label: 'Large' }
    ];
    selectedGalleryTileSize: GalleryTileSize = 'medium';

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
        this.selectedViewMode = cfg.viewMode === 'tree-view' || cfg.viewMode === 'gallery-view'
            ? cfg.viewMode
            : 'list-view';
        this.selectedSort = this.toSortChoice(cfg.sortMode, cfg.sortDirection);
        this.selectedThumbnails = cfg.thumbnailsEnabled;
        this.selectedMinimap = cfg.minimapEnabled;
        this.selectedGalleryLayout = cfg.galleryLayout === 'mosaic' || cfg.galleryLayout === 'grid'
            ? cfg.galleryLayout
            : 'justified';
        this.selectedGalleryTileSize = cfg.galleryTileSize === 'small' || cfg.galleryTileSize === 'large'
            ? cfg.galleryTileSize
            : 'medium';
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
            thumbnailsEnabled: this.selectedThumbnails,
            minimapEnabled: this.selectedMinimap,
            galleryLayout: this.selectedGalleryLayout,
            galleryTileSize: this.selectedGalleryTileSize
        });
    }
}
