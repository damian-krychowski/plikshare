import { AfterViewInit, Component, DestroyRef, ElementRef, computed, effect, inject, input, linkedSignal, output, signal, untracked, viewChild } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppFileItem, AppFileItems, FileOperations } from '../../shared/file-item/file-item.component';
import { FileIconPipe } from '../file-icon-pipe/file-icon.pipe';
import { StorageSizePipe } from '../../shared/storage-size.pipe';
import { getFileDetails } from '../../services/file-type';

const SLIDESHOW_INTERVAL_MS = 4000;
const STRIP_WINDOW_RADIUS = 30;
const PRELOAD_AHEAD = 4;
const PRELOAD_BEHIND = 2;
const MIN_ZOOM = 1;
const MAX_ZOOM = 6;
const SWIPE_THRESHOLD_PX = 60;

export function canOpenFileInLightbox(file: AppFileItem, allowDownload: boolean): boolean {
    if (file.isLocked())
        return false;

    const fileType = getFileDetails(file.extension).type;

    if (fileType !== 'image' && fileType !== 'video')
        return false;

    if (allowDownload)
        return true;

    const thumbnail = file.metadata()?.thumbnail;

    return !!(thumbnail?.largeEtag || thumbnail?.smallEtag || thumbnail?.miniEtag);
}

async function preloadImage(url: string): Promise<void> {
    const img = new Image();
    img.src = url;
    await img.decode();
}

@Component({
    selector: 'app-gallery-lightbox',
    imports: [
        MatTooltipModule,
        FileIconPipe,
        StorageSizePipe
    ],
    templateUrl: './gallery-lightbox.component.html',
    styleUrl: './gallery-lightbox.component.scss'
})
export class GalleryLightboxComponent implements AfterViewInit {
    files = input.required<AppFileItem[]>();
    startIndex = input.required<number>();
    operations = input.required<FileOperations>();
    allowDownload = input(false);
    canSelect = input(false);

    closed = output<void>();
    detailsRequested = output<AppFileItem>();
    indexChanged = output<number>();
    selectionToggled = output<AppFileItem>();

    index = linkedSignal(() => this.startIndex());

    count = computed(() => this.files().length);

    clampedIndex = computed(() => {
        const count = this.count();

        if (count === 0)
            return 0;

        return Math.min(Math.max(this.index(), 0), count - 1);
    });

    current = computed<AppFileItem | null>(() => {
        const files = this.files();

        if (files.length === 0)
            return null;

        return files[this.clampedIndex()];
    });

    currentIsVideo = computed(() => {
        const file = this.current();
        return file != null && getFileDetails(file.extension).type === 'video';
    });

    previewThumbSrc = computed(() => {
        const file = this.current();

        if (!file)
            return null;

        const large = this.largeThumbUrl(file);

        if (large && this._readyLargeUrls().has(large))
            return large;

        return this.quickThumbUrl(file) ?? large;
    });

    fullSrc = signal<string | null>(null);
    videoSrc = signal<string | null>(null);
    isLoadingFull = signal(false);
    isImageLoaded = signal(false);

    skeletonRect = computed<{ w: number, h: number } | null>(() => {
        const file = this.current();
        const dimensions = file?.metadata()?.dimensions;
        const stage = this._stageSize();

        if (!file || !dimensions || dimensions.width <= 0 || dimensions.height <= 0)
            return null;

        if (stage.w <= 0 || stage.h <= 0)
            return null;

        const scale = Math.min(
            stage.w / dimensions.width,
            stage.h / dimensions.height,
            1);

        return {
            w: dimensions.width * scale,
            h: dimensions.height * scale
        };
    });

    showSkeleton = computed(() =>
        !this.isImageLoaded()
        && !this.currentIsVideo()
        && this.skeletonRect() != null);

    displayedImageSrc = computed(() => this.fullSrc() ?? this.previewThumbSrc());

    imageMaxWidth = computed(() => {
        const dimensions = this.current()?.metadata()?.dimensions;
        return dimensions ? `min(100%, ${dimensions.width}px)` : null;
    });

    imageMaxHeight = computed(() => {
        const dimensions = this.current()?.metadata()?.dimensions;
        return dimensions ? `min(100%, ${dimensions.height}px)` : null;
    });

    canDownload = computed(() => {
        const file = this.current();
        return this.allowDownload() && file != null && !file.isLocked();
    });

    canShowDetails = computed(() => {
        const file = this.current();
        return file != null && AppFileItems.canPreview(file, this.allowDownload());
    });

    canSelectCurrent = computed(() => {
        const file = this.current();
        return this.canSelect() && file != null && !file.isLocked();
    });

    isCurrentSelected = computed(() => this.current()?.isSelected() ?? false);

    selectedCount = computed(() => this.files().reduce(
        (count, file) => count + (file.isSelected() ? 1 : 0),
        0));

    isSlideshowPlaying = signal(false);

    zoomScale = signal(1);
    panX = signal(0);
    panY = signal(0);

    isZoomed = computed(() => this.zoomScale() > 1);

    imageTransform = computed(() =>
        `translate(${this.panX()}px, ${this.panY()}px) scale(${this.zoomScale()})`);

    stripWindow = computed<{ file: AppFileItem, index: number }[]>(() => {
        const files = this.files();
        const center = this.clampedIndex();
        const from = Math.max(0, center - STRIP_WINDOW_RADIUS);
        const to = Math.min(files.length, center + STRIP_WINDOW_RADIUS + 1);

        const out: { file: AppFileItem, index: number }[] = [];

        for (let i = from; i < to; i++) {
            out.push({ file: files[i], index: i });
        }

        return out;
    });

    readonly slideshowIntervalMs = SLIDESHOW_INTERVAL_MS;

    showFullQualityPill = computed(() => {
        const file = this.current();

        if (!file || this.currentIsVideo())
            return false;

        if (!this.allowDownload() || file.isLocked())
            return false;

        if (this.fullSrc())
            return false;

        return this.previewThumbSrc() != null;
    });

    isQualityRevealing = signal(false);

    private _stageRef = viewChild<ElementRef<HTMLElement>>('stage');
    private _stageSize = signal<{ w: number, h: number }>({ w: 0, h: 0 });

    private _fullSrcCache = new Map<string, string>();
    private _readyLargeUrls = signal<ReadonlySet<string>>(new Set<string>());
    private _preloadingUrls = new Set<string>();
    private _failedThumbUrls = signal<ReadonlySet<string>>(new Set<string>());
    private _loadGeneration = 0;
    private _slideshowTimer: ReturnType<typeof setTimeout> | null = null;
    private _qualityRevealTimer: ReturnType<typeof setTimeout> | null = null;
    private _pointer: { id: number, startX: number, startY: number, startPanX: number, startPanY: number, moved: boolean } | null = null;

    ngAfterViewInit(): void {
        document.body.appendChild(this._elementRef.nativeElement);
    }

    constructor(private _elementRef: ElementRef<HTMLElement>) {
        const destroyRef = inject(DestroyRef);

        destroyRef.onDestroy(() => this._elementRef.nativeElement.remove());

        const onKeyDown = (event: KeyboardEvent) => {
            if (event.key === 'ArrowRight') {
                event.preventDefault();
                event.stopPropagation();
                this.next();
            } else if (event.key === 'ArrowLeft') {
                event.preventDefault();
                event.stopPropagation();
                this.previous();
            } else if (event.key === 'Escape') {
                event.preventDefault();
                event.stopPropagation();
                this.close();
            } else if (event.key === ' ' && !this.currentIsVideo()) {
                event.preventDefault();
                event.stopPropagation();
                this.toggleCurrentSelection();
            } else if (event.key === 's' || event.key === 'S') {
                event.preventDefault();
                event.stopPropagation();
                this.toggleCurrentSelection();
            } else if (event.key === 'p' || event.key === 'P') {
                event.preventDefault();
                event.stopPropagation();
                this.toggleSlideshow();
            }
        };

        window.addEventListener('keydown', onKeyDown, { capture: true });

        const previousBodyOverflow = document.body.style.overflow;
        document.body.style.overflow = 'hidden';

        destroyRef.onDestroy(() => {
            window.removeEventListener('keydown', onKeyDown, { capture: true });
            document.body.style.overflow = previousBodyOverflow;
            this.stopSlideshow();

            if (this._qualityRevealTimer != null) {
                clearTimeout(this._qualityRevealTimer);
            }
        });

        effect(() => {
            const file = this.current();
            untracked(() => this.onCurrentFileChanged(file));
        });

        effect((onCleanup) => {
            const stage = this._stageRef()?.nativeElement;

            if (!stage)
                return;

            const resizeObserver = new ResizeObserver(() =>
                this._stageSize.set({ w: stage.clientWidth, h: stage.clientHeight }));

            resizeObserver.observe(stage);
            this._stageSize.set({ w: stage.clientWidth, h: stage.clientHeight });

            onCleanup(() => resizeObserver.disconnect());
        });

        effect(() => {
            const files = this.files();
            const index = this.clampedIndex();

            untracked(() => {
                const count = files.length;

                if (count === 0)
                    return;

                this.preloadLargeFor(files[index]);

                for (let i = 1; i <= PRELOAD_AHEAD; i++) {
                    this.preloadLargeFor(files[(index + i) % count]);
                }

                for (let i = 1; i <= PRELOAD_BEHIND; i++) {
                    this.preloadLargeFor(files[(index - i + count) % count]);
                }
            });
        });

        effect(() => {
            this.clampedIndex();

            requestAnimationFrame(() => {
                this._elementRef.nativeElement
                    .querySelector('.lightbox__strip-item--active')
                    ?.scrollIntoView({ inline: 'center', block: 'nearest', behavior: 'smooth' });
            });
        });
    }

    private onCurrentFileChanged(file: AppFileItem | null) {
        this._loadGeneration++;

        this.fullSrc.set(null);
        this.videoSrc.set(null);
        this.isLoadingFull.set(false);
        this.isImageLoaded.set(false);
        this.resetZoom();

        if (!file || file.isLocked() || !this.allowDownload())
            return;

        const kind = getFileDetails(file.extension).type;

        if (kind === 'video') {
            this.loadVideo(file);
            return;
        }

        if (kind !== 'image')
            return;

        const cached = this._fullSrcCache.get(file.externalId);

        if (cached) {
            this.fullSrc.set(cached);
            return;
        }

        if (!this.previewThumbSrc()) {
            this.loadFullImage(file);
        }
    }

    private async loadVideo(file: AppFileItem) {
        const generation = this._loadGeneration;

        try {
            this.isLoadingFull.set(true);

            const response = await this.operations().getDownloadLink(
                file.externalId,
                'inline');

            if (generation !== this._loadGeneration)
                return;

            this.videoSrc.set(response.downloadPreSignedUrl);
        } catch (error) {
            console.error(error);
        } finally {
            if (generation === this._loadGeneration) {
                this.isLoadingFull.set(false);
            }
        }
    }

    private async loadFullImage(file: AppFileItem, revealOnSwap = false) {
        const generation = this._loadGeneration;

        try {
            this.isLoadingFull.set(true);

            const response = await this.operations().getDownloadLink(
                file.externalId,
                'inline');

            await preloadImage(response.downloadPreSignedUrl);

            this._fullSrcCache.set(file.externalId, response.downloadPreSignedUrl);

            if (generation !== this._loadGeneration)
                return;

            this.fullSrc.set(response.downloadPreSignedUrl);

            if (revealOnSwap) {
                this.triggerQualityReveal();
            }
        } catch (error) {
            console.error(error);
        } finally {
            if (generation === this._loadGeneration) {
                this.isLoadingFull.set(false);
            }
        }
    }

    private triggerQualityReveal() {
        if (this._qualityRevealTimer != null) {
            clearTimeout(this._qualityRevealTimer);
        }

        this.isQualityRevealing.set(true);

        this._qualityRevealTimer = setTimeout(() => {
            this.isQualityRevealing.set(false);
            this._qualityRevealTimer = null;
        }, 500);
    }

    requestFullQuality() {
        const file = this.current();

        if (!file || this.isLoadingFull() || this.fullSrc())
            return;

        if (file.isLocked() || !this.allowDownload())
            return;

        if (getFileDetails(file.extension).type !== 'image')
            return;

        this.loadFullImage(file, true);
    }

    private variantUrl(file: AppFileItem, variant: 'large' | 'small' | 'mini'): string | null {
        const base = this.operations().getThumbnailUrl?.(file.externalId);

        if (!base)
            return null;

        const separator = base.includes('?') ? '&' : '?';
        const thumbnail = file.metadata()?.thumbnail;

        if (variant === 'large' && thumbnail?.largeEtag)
            return `${base}${separator}variant=large&v=${thumbnail.largeEtag}`;

        if (variant === 'small' && thumbnail?.smallEtag)
            return `${base}${separator}variant=small&v=${thumbnail.smallEtag}`;

        if (variant === 'mini' && thumbnail?.miniEtag)
            return `${base}${separator}v=${thumbnail.miniEtag}`;

        return null;
    }

    private largeThumbUrl(file: AppFileItem): string | null {
        const url = this.variantUrl(file, 'large');

        return url && !this._failedThumbUrls().has(url)
            ? url
            : null;
    }

    private quickThumbUrl(file: AppFileItem): string | null {
        const failedUrls = this._failedThumbUrls();

        for (const variant of ['small', 'mini'] as const) {
            const url = this.variantUrl(file, variant);

            if (url && !failedUrls.has(url))
                return url;
        }

        return null;
    }

    private preloadLargeFor(file: AppFileItem | undefined) {
        if (!file)
            return;

        const url = this.largeThumbUrl(file);

        if (!url || this._preloadingUrls.has(url) || this._readyLargeUrls().has(url))
            return;

        this._preloadingUrls.add(url);

        preloadImage(url)
            .then(() => this._readyLargeUrls.update(ready => {
                const next = new Set(ready);
                next.add(url);
                return next;
            }))
            .catch(() => this.onImageError(url))
            .finally(() => this._preloadingUrls.delete(url));
    }

    onImageLoaded() {
        this.isImageLoaded.set(true);
    }

    onImageError(url: string | null) {
        if (!url || this.fullSrc() === url)
            return;

        this._failedThumbUrls.update(failed => {
            const next = new Set(failed);
            next.add(url);
            return next;
        });
    }

    stripThumbUrl(file: AppFileItem): string | null {
        const failedUrls = this._failedThumbUrls();

        for (const variant of ['mini', 'small'] as const) {
            const url = this.variantUrl(file, variant);

            if (url && !failedUrls.has(url))
                return url;
        }

        return null;
    }

    next() {
        const count = this.count();

        if (count > 0) {
            this.index.set((this.clampedIndex() + 1) % count);
            this.indexChanged.emit(this.clampedIndex());
            this.rescheduleSlideshowIfPlaying();
        }
    }

    previous() {
        const count = this.count();

        if (count > 0) {
            this.index.set((this.clampedIndex() - 1 + count) % count);
            this.indexChanged.emit(this.clampedIndex());
            this.rescheduleSlideshowIfPlaying();
        }
    }

    goTo(index: number) {
        this.index.set(index);
        this.indexChanged.emit(this.clampedIndex());
        this.rescheduleSlideshowIfPlaying();
    }

    close() {
        this.stopSlideshow();
        this.closed.emit();
    }

    showDetails() {
        const file = this.current();

        if (!file)
            return;

        this.stopSlideshow();
        this.detailsRequested.emit(file);
    }

    toggleCurrentSelection() {
        const file = this.current();

        if (!file || !this.canSelectCurrent())
            return;

        this.selectionToggled.emit(file);
    }

    async downloadCurrent() {
        const file = this.current();

        if (!file || !this.canDownload())
            return;

        const response = await this.operations().getDownloadLink(
            file.externalId,
            'attachment');

        const link = document.createElement('a');
        link.href = response.downloadPreSignedUrl;
        link.download = `${file.name()}${file.extension}`;
        link.click();
        link.remove();
    }

    toggleSlideshow() {
        if (this.isSlideshowPlaying()) {
            this.stopSlideshow();
        } else {
            this.startSlideshow();
        }
    }

    private startSlideshow() {
        if (this.count() < 2)
            return;

        this.isSlideshowPlaying.set(true);
        this.rescheduleSlideshowIfPlaying();
    }

    private rescheduleSlideshowIfPlaying() {
        if (!this.isSlideshowPlaying())
            return;

        this.clearSlideshowTimer();

        this._slideshowTimer = setTimeout(
            () => this.next(),
            SLIDESHOW_INTERVAL_MS);
    }

    private clearSlideshowTimer() {
        if (this._slideshowTimer != null) {
            clearTimeout(this._slideshowTimer);
            this._slideshowTimer = null;
        }
    }

    stopSlideshow() {
        this.clearSlideshowTimer();
        this.isSlideshowPlaying.set(false);
    }

    private resetZoom() {
        this.zoomScale.set(1);
        this.panX.set(0);
        this.panY.set(0);
    }

    onWheel(event: WheelEvent) {
        if (this.currentIsVideo())
            return;

        event.preventDefault();

        const factor = Math.exp(-event.deltaY * 0.002);
        const next = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, this.zoomScale() * factor));

        this.zoomScale.set(next);

        if (next === 1) {
            this.panX.set(0);
            this.panY.set(0);
        }
    }

    onDoubleClick() {
        if (this.currentIsVideo())
            return;

        if (this.isZoomed()) {
            this.resetZoom();
        } else {
            this.zoomScale.set(2.5);
        }
    }

    onPointerDown(event: PointerEvent) {
        if (this.currentIsVideo())
            return;

        (event.target as HTMLElement).setPointerCapture?.(event.pointerId);

        this._pointer = {
            id: event.pointerId,
            startX: event.clientX,
            startY: event.clientY,
            startPanX: this.panX(),
            startPanY: this.panY(),
            moved: false
        };
    }

    onPointerMove(event: PointerEvent) {
        const pointer = this._pointer;

        if (!pointer || pointer.id !== event.pointerId)
            return;

        const dx = event.clientX - pointer.startX;
        const dy = event.clientY - pointer.startY;

        if (Math.abs(dx) > 4 || Math.abs(dy) > 4) {
            pointer.moved = true;
        }

        if (this.isZoomed()) {
            this.panX.set(pointer.startPanX + dx);
            this.panY.set(pointer.startPanY + dy);
        }
    }

    onPointerUp(event: PointerEvent) {
        const pointer = this._pointer;

        if (!pointer || pointer.id !== event.pointerId)
            return;

        this._pointer = null;

        const dx = event.clientX - pointer.startX;
        const dy = event.clientY - pointer.startY;

        if (!this.isZoomed()
            && Math.abs(dx) > SWIPE_THRESHOLD_PX
            && Math.abs(dx) > Math.abs(dy)) {

            if (dx < 0) {
                this.next();
            } else {
                this.previous();
            }

            return;
        }

        if (!pointer.moved
            && !this.isZoomed()
            && event.target instanceof HTMLElement
            && event.target.classList.contains('lightbox__stage')
            && !this.isPointOverMedia(event)) {
            this.close();
        }
    }

    private isPointOverMedia(event: PointerEvent): boolean {
        const media = this._elementRef.nativeElement
            .querySelector('.lightbox__image, .lightbox__video');

        if (!media)
            return false;

        const rect = media.getBoundingClientRect();

        return event.clientX >= rect.left
            && event.clientX <= rect.right
            && event.clientY >= rect.top
            && event.clientY <= rect.bottom;
    }
}
