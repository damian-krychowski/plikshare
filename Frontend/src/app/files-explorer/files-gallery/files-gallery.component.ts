import { Component, DestroyRef, ElementRef, WritableSignal, computed, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppFileItem, AppFileItems, FileOperations } from '../../shared/file-item/file-item.component';
import { SortDirection, SortMode } from '../../services/folders-and-files.api';
import { sortFiles } from '../../services/sort-items';
import { getFileDetails } from '../../services/file-type';
import { CtrlClickDirective } from '../../shared/ctrl-click.directive';
import { FileIconPipe } from '../file-icon-pipe/file-icon.pipe';
import { ConfirmOperationDirective } from '../../shared/operation-confirm/confirm-operation.directive';
import { canOpenFileInLightbox } from '../gallery-lightbox/gallery-lightbox.component';

const TILE_GAP_PX = 6;
const HEADER_HEIGHT_PX = 48;
const RENDER_BUFFER_PX = 800;
const MIN_ASPECT_RATIO = 0.35;
const MAX_ASPECT_RATIO = 3;
const LAST_ROW_MAX_STRETCH = 1.15;
const NARROW_WIDTH_PX = 640;
const NARROW_SCALE = 0.72;
const HERO_EVERY_NTH = 7;
const SHOWCASE_EVERY_NTH = 19;
const SHOWCASE_MIN_COLS = 5;
const PANORAMA_RATIO = 1.6;
const PORTRAIT_RATIO = 0.7;
const MOSAIC_LOOKBACK_ROWS = 3;
const LARGE_VARIANT_THRESHOLD_PX = 420;

export type GalleryLayoutMode = 'justified' | 'mosaic' | 'grid';
export type GalleryDensity = 'compact' | 'standard' | 'comfortable';

const JUSTIFIED_ROW_HEIGHTS: Record<GalleryDensity, number> = {
    compact: 150,
    standard: 220,
    comfortable: 300
};

const CELL_SIZES: Record<GalleryDensity, number> = {
    compact: 130,
    standard: 180,
    comfortable: 240
};

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

function buildGridLayout(args: {
    files: AppFileItem[],
    width: number,
    cellSize: number,
    groupByMonth: boolean
}): GalleryLayout {
    const { files, width, cellSize, groupByMonth } = args;

    if (width < 50 || files.length === 0) {
        return { tiles: [], headers: [], contentHeight: 0 };
    }

    const cols = Math.max(1, Math.floor((width + TILE_GAP_PX) / (cellSize + TILE_GAP_PX)));
    const cellWidth = (width - TILE_GAP_PX * (cols - 1)) / cols;
    const stride = cellWidth + TILE_GAP_PX;

    const tiles: GalleryTile[] = [];
    const headers: GalleryHeader[] = [];

    let y = 0;
    let col = 0;
    let currentGroupLabel: string | null = null;

    const closeRow = () => {
        if (col > 0) {
            y += stride;
            col = 0;
        }
    };

    for (let index = 0; index < files.length; index++) {
        const file = files[index];

        if (groupByMonth) {
            const label = getMonthLabel(file.createdAt);

            if (label !== currentGroupLabel) {
                closeRow();
                currentGroupLabel = label;
                headers.push({ label, y });
                y += HEADER_HEIGHT_PX;
            }
        }

        tiles.push({
            file,
            index,
            x: col * stride,
            y: y,
            w: cellWidth,
            h: cellWidth
        });

        col++;

        if (col === cols) {
            closeRow();
        }
    }

    closeRow();

    return {
        tiles,
        headers,
        contentHeight: y > 0 ? y - TILE_GAP_PX : 0
    };
}

function hashExternalId(externalId: string): number {
    let hash = 0;

    for (let i = 0; i < externalId.length; i++) {
        hash = (hash * 31 + externalId.charCodeAt(i)) | 0;
    }

    return Math.abs(hash);
}

function getMosaicSpan(file: AppFileItem, cols: number): { w: number, h: number } {
    const fileType = getFileDetails(file.extension).type;

    if (fileType !== 'image' && fileType !== 'video')
        return { w: 1, h: 1 };

    const hash = hashExternalId(file.externalId);
    const ratio = getTileAspectRatio(file);

    if (cols >= SHOWCASE_MIN_COLS && hash % SHOWCASE_EVERY_NTH === 0) {
        if (ratio >= PANORAMA_RATIO)
            return { w: 3, h: 2 };

        if (ratio <= PORTRAIT_RATIO)
            return { w: 2, h: 3 };

        return { w: 3, h: 3 };
    }

    if (cols >= 3 && hash % HERO_EVERY_NTH === 0)
        return { w: 2, h: 2 };

    if (cols >= 2 && ratio >= PANORAMA_RATIO)
        return { w: 2, h: 1 };

    if (ratio <= PORTRAIT_RATIO)
        return { w: 1, h: 2 };

    return { w: 1, h: 1 };
}

function buildMosaicLayout(args: {
    files: AppFileItem[],
    width: number,
    cellSize: number,
    groupByMonth: boolean
}): GalleryLayout {
    const { files, width, cellSize, groupByMonth } = args;

    if (width < 50 || files.length === 0) {
        return { tiles: [], headers: [], contentHeight: 0 };
    }

    const cols = Math.max(1, Math.floor((width + TILE_GAP_PX) / (cellSize + TILE_GAP_PX)));
    const cellWidth = (width - TILE_GAP_PX * (cols - 1)) / cols;
    const stride = cellWidth + TILE_GAP_PX;

    const tiles: GalleryTile[] = [];
    const headers: GalleryHeader[] = [];

    let y = 0;
    let currentGroupLabel: string | null = null;

    let occupied: boolean[][] = [];
    let searchStartRow = 0;
    let usedRows = 0;

    const ensureRow = (row: number) => {
        while (occupied.length <= row) {
            occupied.push(new Array(cols).fill(false));
        }
    };

    const fits = (row: number, colIdx: number, span: { w: number, h: number }) => {
        for (let r = row; r < row + span.h; r++) {
            ensureRow(r);

            for (let c = colIdx; c < colIdx + span.w; c++) {
                if (occupied[r][c])
                    return false;
            }
        }

        return true;
    };

    const closeGroup = () => {
        if (usedRows > 0) {
            y += usedRows * stride;
        }

        occupied = [];
        searchStartRow = 0;
        usedRows = 0;
    };

    for (let index = 0; index < files.length; index++) {
        const file = files[index];

        if (groupByMonth) {
            const label = getMonthLabel(file.createdAt);

            if (label !== currentGroupLabel) {
                closeGroup();
                currentGroupLabel = label;
                headers.push({ label, y });
                y += HEADER_HEIGHT_PX;
            }
        }

        const span = getMosaicSpan(file, cols);
        span.w = Math.min(span.w, cols);

        let placed = false;

        for (let row = searchStartRow; !placed; row++) {
            for (let colIdx = 0; colIdx <= cols - span.w; colIdx++) {
                if (!fits(row, colIdx, span))
                    continue;

                for (let r = row; r < row + span.h; r++) {
                    for (let c = colIdx; c < colIdx + span.w; c++) {
                        occupied[r][c] = true;
                    }
                }

                tiles.push({
                    file,
                    index,
                    x: colIdx * stride,
                    y: y + row * stride,
                    w: span.w * cellWidth + (span.w - 1) * TILE_GAP_PX,
                    h: span.h * cellWidth + (span.h - 1) * TILE_GAP_PX
                });

                usedRows = Math.max(usedRows, row + span.h);
                searchStartRow = Math.max(searchStartRow, row - MOSAIC_LOOKBACK_ROWS);
                placed = true;
                break;
            }
        }
    }

    closeGroup();

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
        ConfirmOperationDirective
    ],
    templateUrl: './files-gallery.component.html',
    styleUrl: './files-gallery.component.scss'
})
export class FilesGalleryComponent {
    files = input.required<AppFileItem[]>();
    sortMode = input.required<SortMode>();
    sortDirection = input.required<SortDirection>();
    searchPhrase = input.required<string>();
    operations = input.required<FileOperations>();

    layoutMode = input<GalleryLayoutMode>('justified');
    density = input<GalleryDensity>('standard');

    canSelect = input(false);
    allowDownload = input(false);
    canGenerateThumbnails = input(false);
    processingFileIds = input<ReadonlySet<string>>(new Set());
    expectedTotalCount = input<number | null>(null);
    isActive = input(true);

    fileDetailsRequested = output<AppFileItem>();
    lightboxRequested = output<AppFileItem>();
    visibleRangeEndChanged = output<number>();
    thumbnailsGenerationRequested = output<string[]>();

    private _hostRef = viewChild<ElementRef<HTMLElement>>('galleryHost');

    containerWidth = signal(0);

    isSearchActive = computed(() => this.searchPhrase().length > 0);

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
        && this.files().length > 0);

    private densityScale = computed(() =>
        this.containerWidth() < NARROW_WIDTH_PX ? NARROW_SCALE : 1);

    targetRowHeight = computed(() =>
        Math.round(JUSTIFIED_ROW_HEIGHTS[this.density()] * this.densityScale()));

    cellSize = computed(() =>
        Math.round(CELL_SIZES[this.density()] * this.densityScale()));

    layout = computed<GalleryLayout>(() => {
        const mode = this.layoutMode();
        const files = this.visibleFiles();
        const width = this.containerWidth();
        const groupByMonth = this.sortMode() === 'date';

        if (mode === 'grid') {
            return buildGridLayout({
                files,
                width,
                cellSize: this.cellSize(),
                groupByMonth
            });
        }

        if (mode === 'mosaic') {
            return buildMosaicLayout({
                files,
                width,
                cellSize: this.cellSize(),
                groupByMonth
            });
        }

        return buildJustifiedLayout({
            files,
            width,
            targetRowHeight: this.targetRowHeight(),
            groupByMonth
        });
    });

    totalHeightPx = computed(() => {
        const layout = this.layout();
        const expected = this.expectedTotalCount();

        if (expected == null || this.isSearchActive())
            return layout.contentHeight;

        const missingCount = Math.max(0, expected - this.visibleFiles().length);

        if (missingCount === 0)
            return layout.contentHeight;

        const unit = this.layoutMode() === 'justified'
            ? this.targetRowHeight()
            : this.cellSize();

        const tilesPerRow = Math.max(1, Math.floor(this.containerWidth() / (unit + TILE_GAP_PX)));
        const fillerRows = Math.ceil(missingCount / tilesPerRow);

        return layout.contentHeight + fillerRows * (unit + TILE_GAP_PX);
    });

    private _viewport = signal<{ top: number, bottom: number }>({ top: 0, bottom: 0 });

    renderedTiles = computed<RenderedTile[]>(() => {
        const { tiles } = this.layout();
        const viewport = this._viewport();
        const top = viewport.top - RENDER_BUFFER_PX;
        const bottom = viewport.bottom + RENDER_BUFFER_PX;
        const failedUrls = this._failedThumbnailUrls();
        const processingIds = this.processingFileIds();
        const devicePixelRatio = typeof window !== 'undefined' ? (window.devicePixelRatio || 1) : 1;

        const out: RenderedTile[] = [];

        for (const tile of tiles) {
            if (tile.y + tile.h < top || tile.y > bottom)
                continue;

            const fileType = getFileDetails(tile.file.extension).type;
            const isMedia = fileType === 'image' || fileType === 'video';

            const thumbUrl = isMedia
                ? this.buildTileThumbUrl(
                    tile.file,
                    failedUrls,
                    Math.max(tile.w, tile.h) * devicePixelRatio)
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

    private _failedThumbnailUrls = signal<ReadonlySet<string>>(new Set<string>());
    private _fileSelectionAnchorId: string | null = null;
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

    private buildTileThumbUrl(
        file: AppFileItem,
        failedUrls: ReadonlySet<string>,
        renderedPx: number
    ): string | null {
        const base = this.operations().getThumbnailUrl?.(file.externalId);

        if (!base)
            return null;

        const separator = base.includes('?') ? '&' : '?';
        const thumbnail = file.metadata()?.thumbnail;

        const smallUrl = thumbnail?.smallEtag
            ? `${base}${separator}variant=small&v=${thumbnail.smallEtag}`
            : null;

        const largeUrl = thumbnail?.largeEtag
            ? `${base}${separator}variant=large&v=${thumbnail.largeEtag}`
            : null;

        const miniUrl = thumbnail?.miniEtag
            ? `${base}${separator}v=${thumbnail.miniEtag}`
            : null;

        const candidates = renderedPx > LARGE_VARIANT_THRESHOLD_PX
            ? [largeUrl, smallUrl, miniUrl]
            : [smallUrl, miniUrl, largeUrl];

        return candidates.find(url => url != null && !failedUrls.has(url)) ?? null;
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

    onTileClicked(file: AppFileItem) {
        if (file.isLocked())
            return;

        if (canOpenFileInLightbox(file, this.allowDownload())) {
            this.lightboxRequested.emit(file);
            return;
        }

        if (AppFileItems.canPreview(file, this.allowDownload())) {
            this.fileDetailsRequested.emit(file);
        }
    }

    isTileClickable(file: AppFileItem): boolean {
        return canOpenFileInLightbox(file, this.allowDownload())
            || AppFileItems.canPreview(file, this.allowDownload());
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
}
