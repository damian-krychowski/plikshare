import { Component, computed, ElementRef, HostListener, input, output, signal } from '@angular/core';
import { GalleryDensity, GalleryLayoutMode } from '../files-gallery/files-gallery.component';

@Component({
    selector: 'app-gallery-menu',
    standalone: true,
    templateUrl: './gallery-menu.component.html',
    styleUrl: './gallery-menu.component.scss'
})
export class GalleryMenuComponent {
    layout = input.required<GalleryLayoutMode>();
    density = input.required<GalleryDensity>();

    layoutChanged = output<GalleryLayoutMode>();
    densityChanged = output<GalleryDensity>();

    isOpen = signal(false);

    layoutLabel = computed(() => {
        const mode = this.layout();
        if (mode === 'mosaic') return 'Mosaic';
        if (mode === 'grid') return 'Grid';
        return 'Justified';
    });

    densityLabel = computed(() => {
        const density = this.density();
        if (density === 'compact') return 'S';
        if (density === 'comfortable') return 'L';
        return 'M';
    });

    triggerLabel = computed(() => `${this.layoutLabel()} · ${this.densityLabel()}`);

    constructor(private _el: ElementRef<HTMLElement>) {}

    toggle() {
        this.isOpen.update(v => !v);
    }

    selectLayout(mode: GalleryLayoutMode) {
        this.layoutChanged.emit(mode);
    }

    selectDensity(density: GalleryDensity) {
        this.densityChanged.emit(density);
    }

    isLayoutActive(mode: GalleryLayoutMode): boolean {
        return this.layout() === mode;
    }

    isDensityActive(density: GalleryDensity): boolean {
        return this.density() === density;
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
