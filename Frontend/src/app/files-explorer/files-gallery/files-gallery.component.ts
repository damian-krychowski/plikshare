import { Component, DestroyRef, ElementRef, WritableSignal, computed, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppFileItem, AppFileItems, FileOperations } from '../../shared/file-item/file-item.component';
import { AppFolderItem } from '../../shared/folder-item/folder-item.component';
import { SortDirection, SortMode } from '../../services/folders-and-files.api';
import { sortFiles, sortFolders } from '../../services/sort-items';
import { getFileDetails } from '../../services/file-type';
import { CtrlClickDirective } from '../../shared/ctrl-click.directive';
import { FileIconPipe } from '../file-icon-pipe/file-icon.pipe';
import { ConfirmOperationDirective } from '../../shared/operation-confirm/confirm-operation.directive';
import { GalleryLightboxComponent } from '../gallery-lightbox/gallery-lightbox.component';

const TILE_GAP_PX = 6;
const HEADER_HEIGHT_PX = 48;
const RENDER_BUFFER_PX = 800;
const MIN_ASPECT_RATIO = 0.35;
const MAX_ASPECT_RATIO = 3;
const LAST_ROW_MAX_STRETCH = 1.15;

export type GalleryTile = {
    file: AppFileItem;
    index: number;
    x: number;
    y: number;
    w: number;
    h: number;
};

type GalleryHeader = {
    label: string;
    y: number;
};

type GalleryLayout = {
    tiles: GalleryTile[];
    headers: GalleryHeader[];
    contentHeight: number;
};

type RenderedTile = {
    tile: GalleryTile;
    thumbUrl: string | null;
    isMedia: boolean;
    isVideo: boolean;
    isProcessing: boolean;
};

function getTileAspectRatio(file: AppFileItem): number {
    const dimensions = file.metadata()?.dimensions;

    if (!dimensions || dimensions.width <= 0 || dimensions.height <= 0)
        return 1;

    return Math.min(
        MAX_ASPECT_RATIO,
        Math.max(MIN_ASPECT_RATIO, dimensions.width / dimensions.height));
}

function getMonthLabel(date: Date | null): string {
    if (!date)
        return 'Unknown date';

    return new Intl.DateTimeFormat('en', { month: 'long', year: 'numeric' })
        .format(date);
}

function buildJustifiedLayout(args: {
    files: AppFileItem[],
    width: number,
    targetRowHeight: number,
    groupByMonth: boolean
}): GalleryLayout {
    const { files, width, targetRowHeight, groupByMonth } = args;

    if (width < 50 || files.length === 0) {
        return { tiles: [], headers: [], contentHeight: 0 };
    }

    const tiles: GalleryTile[] = [];
    const headers: GalleryHeader[] = [];

    let y = 0;
    let row: { file: AppFileItem, index: number, ratio: number }[] = [];
    let rowRatioSum = 0;

    const flushRow = (isLastRow: boolean) => {
        if (row.length === 0)
            return;

        const gapsWidth = TILE_GAP_PX * (row.length - 1);
        const justifiedHeight = (width - gapsWidth) / rowRatioSum;

        const height = isLastRow
            ? Math.min(targetRowHeight, justifiedHeight)
            : Math.min(justifiedHeight, targetRowHeight * LAST_ROW_MAX_STRETCH);

        const isRowFull = !isLastRow || justifiedHeight <= targetRowHeight;

        let x = 0;

        for (let i = 0; i < row.length; i++) {
            const entry = row[i];

            const tileWidth = isRowFull && i === row.length - 1
                ? Math.max(0, width - x)
                : entry.ratio * height;

            tiles.push({
                file: entry.file,
                index: entry.index,
                x: x,
                y: y,
                w: tileWidth,
                h: height
            });

            x += tileWidth + TILE_GAP_PX;
        }

        y += height + TILE_GAP_PX;
        row = [];
        rowRatioSum = 0;
    };

    let currentGroupLabel: string | null = null;

    for (let index = 0; index < files.length; index++) {
        const file = files[index];

        if (groupByMonth) {
            const label = getMonthLabel(file.createdAt);

            if (label !== currentGroupLabel) {
                flushRow(true);
                currentGroupLabel = label;
                headers.push({ label, y });
                y += HEADER_HEIGHT_PX;
            }
        }

        const ratio = getTileAspectRatio(file);

        row.push({ file, index, ratio });
        rowRatioSum += ratio;

        const rowWidthAtTarget = rowRatioSum * targetRowHeight + TILE_GAP_PX * (row.length - 1);

        if (rowWidthAtTarget >= width) {
            flushRow(false);
        }
    }

    flushRow(true);

    return {
        tiles,
        headers,
        contentHeight: y > 0 ? y - TILE_GAP_PX : 0
    };
}

@Component({
    selector: 'app-files-gallery',
    imports: [
        FormsModule,
        MatCheckboxModule,
        MatTooltipModule,
        CtrlClickDirective,
        FileIconPipe,
        ConfirmOperationDirective,
        GalleryLightboxComponent
    ],
    templateUrl: './files-gallery.component.html',
    styleUrl: './files-gallery.component.scss'
})
export class FilesGalleryComponent {
    files = input.required<AppFileItem[]>();
    folders = input.required<AppFolderItem[]>();
    sortMode = input.required<SortMode>();
    sortDirection = input.required<SortDirection>();
    searchPhrase = input.required<string>();
    operations = input.required<FileOperations>();

    canSelect = input(false);
    allowDownload = input(false);
    canGenerateThumbnails = input(false);
    processingFileIds = input<ReadonlySet<string>>(new Set());
    expectedTotalCount = input<number | null>(null);
    isActive = input(true);

    folderOpened = output<AppFolderItem>();
    folderPrefetched = output<AppFolderItem>();
    fileDetailsRequested = output<AppFileItem>();
    visibleRangeEndChanged = output<number>();
    thumbnailsGenerationRequested = output<string[]>();

    private _hostRef = viewChild<ElementRef<HTMLElement>>('galleryHost');

    containerWidth = signal(0);

    isSearchActive = computed(() => this.searchPhrase().length > 0);

    sortedFolders = computed(() => sortFolders(
        this.folders(),
        this.sortMode(),
        this.sortDirection()));

    visibleFolders = computed(() => {
        const phrase = this.searchPhrase().toLowerCase();
        const folders = this.sortedFolders();

        if (!phrase)
            return folders;

        return folders.filter(f => f.name().toLowerCase().includes(phrase));
    });

    sortedFiles = computed(() => sortFiles(
        this.files(),
        this.sortMode(),
        this.sortDirection()));

    visibleFiles = computed(() => {
        const phrase = this.searchPhrase().toLowerCase();
        const files = this.sortedFiles();

        if (!phrase)
            return files;

        return files.filter(f => (f.name() + f.extension).toLowerCase().includes(phrase));
    });

    hasNoSearchMatches = computed(() =>
        this.isSearchActive()
        && this.visibleFiles().length === 0
        && this.visibleFolders().length === 0
        && (this.files().length > 0 || this.folders().length > 0));

    isEmpty = computed(() =>
        this.files().length === 0
        && this.folders().length === 0
        && this.expectedTotalCount() == null);

    targetRowHeight = computed(() => this.containerWidth() < 640 ? 168 : 224);

    layout = computed<GalleryLayout>(() => buildJustifiedLayout({
        files: this.visibleFiles(),
        width: this.containerWidth(),
        targetRowHeight: this.targetRowHeight(),
        groupByMonth: this.sortMode() === 'date'
    }));

    totalHeightPx = computed(() => {
        const layout = this.layout();
        const expected = this.expectedTotalCount();

        if (expected == null || this.isSearchActive())
            return layout.contentHeight;

        const missingCount = Math.max(0, expected - this.visibleFiles().length);

        if (missingCount === 0)
            return layout.contentHeight;

        const target = this.targetRowHeight();
        const tilesPerRow = Math.max(1, Math.floor(this.containerWidth() / (target + TILE_GAP_PX)));
        const fillerRows = Math.ceil(missingCount / tilesPerRow);

        return layout.contentHeight + fillerRows * (target + TILE_GAP_PX);
    });

    private _viewport = signal<{ top: number, bottom: number }>({ top: 0, bottom: 0 });

    renderedTiles = computed<RenderedTile[]>(() => {
        const { tiles } = this.layout();
        const viewport = this._viewport();
        const top = viewport.top - RENDER_BUFFER_PX;
        const bottom = viewport.bottom + RENDER_BUFFER_PX;
        const failedUrls = this._failedThumbnailUrls();
        const processingIds = this.processingFileIds();

        const out: RenderedTile[] = [];

        for (const tile of tiles) {
            if (tile.y + tile.h < top || tile.y > bottom)
                continue;

            const fileType = getFileDetails(tile.file.extension).type;
            const isMedia = fileType === 'image' || fileType === 'video';

            const thumbUrl = isMedia
                ? this.buildTileThumbUrl(tile.file, failedUrls)
                : null;

            out.push({
                tile,
                thumbUrl,
                isMedia,
                isVideo: fileType === 'video',
                isProcessing: processingIds.has(tile.file.externalId)
            });
        }

        return out;
    });

    renderedHeaders = computed<GalleryHeader[]>(() => {
        const { headers } = this.layout();
        const viewport = this._viewport();
        const top = viewport.top - RENDER_BUFFER_PX;
        const bottom = viewport.bottom + RENDER_BUFFER_PX;

        return headers.filter(h => h.y + HEADER_HEIGHT_PX >= top && h.y <= bottom);
    });

    filesMissingThumbnails = computed(() => this.visibleFiles().filter(file => {
        if (file.isLocked())
            return false;

        if (this.processingFileIds().has(file.externalId))
            return false;

        const fileType = getFileDetails(file.extension).type;

        if (fileType !== 'image' && fileType !== 'video')
            return false;

        return !file.metadata()?.thumbnail?.smallEtag;
    }));

    showGenerateBanner = computed(() =>
        this.canGenerateThumbnails()
        && this.filesMissingThumbnails().length > 0);

    generateBannerSubtitle = computed(() => {
        const count = this.filesMissingThumbnails().length;
        const fileLabel = count === 1 ? 'file' : 'files';
        return `Thumbnails for ${count} ${fileLabel} will be generated in the background.`;
    });

    lightboxFiles = computed(() => this.visibleFiles().filter(
        file => this.canOpenInLightbox(file)));

    lightboxIndex = signal<number | null>(null);

    private _failedThumbnailUrls = signal<ReadonlySet<string>>(new Set<string>());
    private _fileSelectionAnchorId: string | null = null;
    private _folderSelectionAnchorId: string | null = null;
    private _lastEmittedRangeEnd = -1;

    constructor() {
        const destroyRef = inject(DestroyRef);

        effect((onCleanup) => {
            const host = this._hostRef()?.nativeElement;

            if (!host)
                return;

            const resizeObserver = new ResizeObserver(() => {
                this.containerWidth.set(host.clientWidth);
                requestAnimationFrame(() => this.recomputeViewport());
            });

            resizeObserver.observe(host);
            this.containerWidth.set(host.clientWidth);

            onCleanup(() => resizeObserver.disconnect());
        });

        const onScroll = () => requestAnimationFrame(() => this.recomputeViewport());

        window.addEventListener('scroll', onScroll, { capture: true, passive: true });
        window.addEventListener('resize', onScroll, { passive: true });

        destroyRef.onDestroy(() => {
            window.removeEventListener('scroll', onScroll, { capture: true });
            window.removeEventListener('resize', onScroll);
        });

        effect(() => {
            this.layout();
            this.totalHeightPx();
            requestAnimationFrame(() => this.recomputeViewport());
        });

        effect(() => {
            if (!this.isActive())
                return;

            const layout = this.layout();
            const viewport = this._viewport();
            const bottom = viewport.bottom + RENDER_BUFFER_PX;

            let needed: number;

            if (bottom >= layout.contentHeight) {
                needed = untracked(() => this.visibleFiles()).length + 300;
            } else {
                needed = 0;

                for (const tile of layout.tiles) {
                    if (tile.y < bottom && tile.index + 1 > needed) {
                        needed = tile.index + 1;
                    }
                }
            }

            if (needed !== this._lastEmittedRangeEnd) {
                this._lastEmittedRangeEnd = needed;
                this.visibleRangeEndChanged.emit(needed);
            }
        });
    }

    private recomputeViewport(): void {
        const host = this._hostRef()?.nativeElement;

        if (!host) {
            this._viewport.set({ top: 0, bottom: 0 });
            return;
        }

        const rect = host.getBoundingClientRect();
        const windowHeight = typeof window !== 'undefined' ? window.innerHeight : 800;

        const top = Math.max(0, -rect.top);
        const visiblePx = Math.max(0, Math.min(rect.height, windowHeight - Math.max(0, rect.top)));

        this._viewport.set({
            top: top,
            bottom: top + visiblePx
        });
    }

    private buildTileThumbUrl(file: AppFileItem, failedUrls: ReadonlySet<string>): string | null {
        const base = this.operations().getThumbnailUrl?.(file.externalId);

        if (!base)
            return null;

        const separator = base.includes('?') ? '&' : '?';
        const thumbnail = file.metadata()?.thumbnail;

        const candidates: string[] = [];

        if (thumbnail?.smallEtag)
            candidates.push(`${base}${separator}variant=small&v=${thumbnail.smallEtag}`);

        if (thumbnail?.miniEtag)
            candidates.push(`${base}${separator}v=${thumbnail.miniEtag}`);

        return candidates.find(url => !failedUrls.has(url)) ?? null;
    }

    onThumbnailLoaded(event: Event) {
        (event.target as HTMLElement).classList.add('gallery-tile__img--loaded');
    }

    onThumbnailError(url: string | null) {
        if (!url)
            return;

        this._failedThumbnailUrls.update(failed => {
            const next = new Set(failed);
            next.add(url);
            return next;
        });
    }

    private canOpenInLightbox(file: AppFileItem): boolean {
        if (file.isLocked())
            return false;

        const fileType = getFileDetails(file.extension).type;

        if (fileType !== 'image' && fileType !== 'video')
            return false;

        if (this.allowDownload())
            return true;

        const thumbnail = file.metadata()?.thumbnail;

        return !!(thumbnail?.largeEtag || thumbnail?.smallEtag || thumbnail?.miniEtag);
    }

    onTileClicked(file: AppFileItem) {
        if (file.isLocked())
            return;

        const lightboxIdx = this.lightboxFiles().indexOf(file);

        if (lightboxIdx >= 0) {
            this.lightboxIndex.set(lightboxIdx);
            return;
        }

        if (AppFileItems.canPreview(file, this.allowDownload())) {
            this.fileDetailsRequested.emit(file);
        }
    }

    isTileClickable(file: AppFileItem): boolean {
        return this.canOpenInLightbox(file)
            || AppFileItems.canPreview(file, this.allowDownload());
    }

    closeLightbox() {
        this.lightboxIndex.set(null);
    }

    onLightboxDetails(file: AppFileItem) {
        this.lightboxIndex.set(null);
        this.fileDetailsRequested.emit(file);
    }

    requestThumbnailsGeneration() {
        const fileExternalIds = this
            .filesMissingThumbnails()
            .map(f => f.externalId);

        if (fileExternalIds.length > 0) {
            this.thumbnailsGenerationRequested.emit(fileExternalIds);
        }
    }

    toggleFileSelection(file: AppFileItem) {
        file.isSelected.update(value => !value);

        if (file.isSelected()) {
            this._fileSelectionAnchorId = file.externalId;
        } else {
            const firstSelected = this.visibleFiles().find(f => f.isSelected());
            this._fileSelectionAnchorId = firstSelected?.externalId ?? null;
        }
    }

    onFileShiftClicked(file: AppFileItem) {
        this.applyRangeSelection(
            this.visibleFiles(),
            this._fileSelectionAnchorId,
            file,
            anchorId => this._fileSelectionAnchorId = anchorId,
            () => this.toggleFileSelection(file));
    }

    toggleFolderSelection(folder: AppFolderItem) {
        folder.isSelected.update(value => !value);

        if (folder.isSelected()) {
            this._folderSelectionAnchorId = folder.externalId;
        } else {
            const firstSelected = this.visibleFolders().find(f => f.isSelected());
            this._folderSelectionAnchorId = firstSelected?.externalId ?? null;
        }
    }

    onFolderShiftClicked(folder: AppFolderItem) {
        this.applyRangeSelection(
            this.visibleFolders(),
            this._folderSelectionAnchorId,
            folder,
            anchorId => this._folderSelectionAnchorId = anchorId,
            () => this.toggleFolderSelection(folder));
    }

    private applyRangeSelection<T extends { externalId: string, isSelected: WritableSignal<boolean> }>(
        list: T[],
        anchorId: string | null,
        target: T,
        setAnchor: (anchorId: string | null) => void,
        fallbackToggle: () => void
    ) {
        if (!anchorId) {
            fallbackToggle();
            return;
        }

        const anchorIdx = list.findIndex(i => i.externalId === anchorId);
        const targetIdx = list.findIndex(i => i.externalId === target.externalId);

        if (anchorIdx === -1 || targetIdx === -1) {
            fallbackToggle();
            return;
        }

        const from = Math.min(anchorIdx, targetIdx);
        const to = Math.max(anchorIdx, targetIdx);

        list.forEach((item, idx) => {
            const inRange = idx >= from && idx <= to;

            if (item.isSelected() !== inRange)
                item.isSelected.set(inRange);
        });

        setAnchor(anchorId);
    }

    openFolder(folder: AppFolderItem) {
        this.folderOpened.emit(folder);
    }

    prefetchFolder(folder: AppFolderItem) {
        this.folderPrefetched.emit(folder);
    }
}
