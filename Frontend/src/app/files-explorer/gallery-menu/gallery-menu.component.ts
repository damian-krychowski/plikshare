import { Component, computed, ElementRef, HostListener, input, output, signal } from '@angular/core';
import { GalleryLayoutMode, GalleryTileSize } from '../files-gallery/files-gallery.component';

@Component({
    selector: 'app-gallery-menu',
    standalone: true,
    templateUrl: './gallery-menu.component.html',
    styleUrl: './gallery-menu.component.scss'
})
export class GalleryMenuComponent {
    layout = input.required<GalleryLayoutMode>();
    tileSize = input.required<GalleryTileSize>();

    layoutChanged = output<GalleryLayoutMode>();
    tileSizeChanged = output<GalleryTileSize>();

    isOpen = signal(false);

    layoutLabel = computed(() => {
        const mode = this.layout();
        if (mode === 'mosaic') return 'Mosaic';
        if (mode === 'grid') return 'Grid';
        return 'Justified';
    });

    tileSizeLabel = computed(() => {
        const tileSize = this.tileSize();
        if (tileSize === 'small') return 'S';
        if (tileSize === 'large') return 'L';
        return 'M';
    });

    triggerLabel = computed(() => `${this.layoutLabel()} · ${this.tileSizeLabel()}`);

    constructor(private _el: ElementRef<HTMLElement>) {}

    toggle() {
        this.isOpen.update(v => !v);
    }

    selectLayout(mode: GalleryLayoutMode) {
        this.layoutChanged.emit(mode);
    }

    selectTileSize(tileSize: GalleryTileSize) {
        this.tileSizeChanged.emit(tileSize);
    }

    isLayoutActive(mode: GalleryLayoutMode): boolean {
        return this.layout() === mode;
    }

    isTileSizeActive(tileSize: GalleryTileSize): boolean {
        return this.tileSize() === tileSize;
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
