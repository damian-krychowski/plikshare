import { AfterViewInit, Component, computed, ElementRef, input, OnChanges, OnDestroy, output, signal, SimpleChanges, viewChild } from "@angular/core";
import { ImageDimensions, ImageExif } from "../../file-inline-preview/file-inline-preview.component";
import { FileMetadataDto } from "../../../services/folders-and-files.api";
import { getMimeType } from "../../../services/file-type";
import exifr from "exifr";
import { HttpHeadersFactory } from "../../http-headers-factory";

@Component({
    selector: 'app-image-preview',
    imports: [],
    templateUrl: './image-preview.component.html',
    styleUrls: ['./image-preview.component.scss']
})
export class ImagePreviewComponent implements OnChanges, OnDestroy, AfterViewInit {
    fileUrl = input<string | null>(null);
    fileName = input.required<string>();
    fileExtension = input.required<string>();
    httpHeadersFactory = input.required<HttpHeadersFactory>();

    // The raw file metadata — image-preview pulls the dimensions out of it itself, so callers
    // pass the whole blob (null for files that have none) instead of pre-extracted width/height.
    metadata = input<FileMetadataDto | null>(null);

    private _dimensions = computed(() => this.metadata()?.dimensions ?? null);

    hasInitialDimensions = computed(() => {
        const d = this._dimensions();
        return d != null && d.width > 0 && d.height > 0;
    });

    aspectRatioCss = computed(() => {
        const d = this._dimensions();
        if (!this.hasInitialDimensions() || d == null)
            return null;

        return `${d.width} / ${d.height}`;
    });

    private _aspectRatio = computed(() => {
        const d = this._dimensions();
        return this.hasInitialDimensions() && d != null
            ? d.width / d.height
            : null;
    });

    // Inner content box of the frame (clientWidth minus horizontal padding, plus the vertical
    // padding) — captured during initPreviewHeight so the skeleton can be sized to the exact
    // rectangle the contained <img> will occupy.
    private _innerWidth = signal(0);
    private _frameVerticalPadding = signal(0);

    // The exact pixel rectangle the image will render at (object-fit: contain inside the frame).
    // The skeleton overlay uses these so it sits precisely where the photo will appear — the
    // image then drops into the same rectangle with zero movement.
    skeletonHeight = computed(() => {
        const aspect = this._aspectRatio();
        const innerWidth = this._innerWidth();
        const frameHeight = this.previewHeight();

        if (aspect == null || innerWidth <= 0 || frameHeight == null)
            return null;

        const innerHeight = frameHeight - this._frameVerticalPadding();
        const widthFitHeight = innerWidth / aspect;

        return Math.max(0, Math.min(innerHeight, widthFitHeight));
    });

    skeletonWidth = computed(() => {
        const aspect = this._aspectRatio();
        const height = this.skeletonHeight();

        return aspect != null && height != null
            ? height * aspect
            : null;
    });

    // Metadata is consumed by the parent (file-inline-preview) which renders it next to
    // the thumbnail cards in the "Image" section. Image-preview itself stays focused on
    // displaying the image; it just sources the data because it owns the fetched blob.
    dimensionsChange = output<ImageDimensions | null>();
    exifChange = output<ImageExif | null>();

    fileFullName = computed(() => this.fileName() + this.fileExtension());

    frameRef = viewChild<ElementRef<HTMLDivElement>>('frame');

    // Frame height in px driven by the bottom resize handle. Set from the backend dimensions
    // BEFORE the blob loads (see the effect below), so the striped skeleton already has the
    // size the image will render at and the image drops in with no vertical jump.
    previewHeight = signal<number | null>(null);
    isResizing = signal(false);

    // Gates the frame-height transition: off until the first height is set (so opening doesn't
    // animate from zero), on afterwards (so navigating between differently-shaped images morphs).
    morphReady = signal(false);

    // Upper bound of previewHeight (frame-space px), recomputed per image. Also the height of
    // the striped background layer: keeping that layer at a constant size means its diagonal
    // gradient is anchored to a box that doesn't change while the frame is dragged, so the
    // stripes stay put instead of crawling. The frame just clips more/less of the stable layer.
    maxPreviewHeight = signal(0);

    // Smallest displayable image height; the frame floor adds the frame's vertical padding
    // on top of this. Kept tiny so the user can collapse the preview to a thin strip.
    private readonly MIN_IMAGE_HEIGHT = 80;

    // The dragged height is persisted globally (one preference for all images) as raw
    // frame-space px and re-applied — clamped to each image's own range — on open. Double
    // -click clears it to fall back to the per-image natural fit.
    private readonly PREVIEW_HEIGHT_STORAGE_KEY = 'plikshare:image-preview-height';

    // Drag bookkeeping (frame-space px). Recomputed on every image load.
    private resizeStartY = 0;
    private resizeStartHeight = 0;
    private resizeMinHeight = 0;
    private resizeInitialHeight = 0;

    objectUrl = signal<string | null>(null);
    // Drives the opacity fade — flipped to false right before the new objectUrl swap so the
    // <img> starts at 0 opacity, then flipped back to true on the native (load) event so
    // CSS transition runs a buttery fade-in. Avoids the harsh blink that direct src swap
    // would otherwise produce when navigating next/previous between files.
    isImageLoaded = signal(false);

    private _morphArmed = false;

    ngAfterViewInit(): void {
        // First open: the frame now exists, so reserve its height from the backend dimensions
        // before the blob finishes loading.
        this.applyBackendDimensions();
    }

    async ngOnChanges(changes: SimpleChanges): Promise<void> {
        // New file (next/previous) — fileName/extension change synchronously with the click,
        // BEFORE the download-link promise resolves. Drop to the skeleton right now so the
        // current image fades out into the skeleton and the box starts morphing immediately,
        // instead of waiting (showing the stale image) until the new URL arrives.
        if (changes['fileName'] || changes['fileExtension']) {
            this.isImageLoaded.set(false);
        }

        // New metadata (next/previous): re-size the frame now so it morphs to the new shape while
        // the skeleton is visible (and the new blob is still loading). No-op on the very first
        // change pass (frame not in the DOM yet — ngAfterViewInit covers it).
        if (changes['metadata']) {
            this.applyBackendDimensions();
        }

        const url = this.fileUrl();

        if(changes['fileUrl'] && url) {
            // The blob fades in over the skeleton once the new URL's content loads.
            this.isImageLoaded.set(false);
            await this.loadImageWithMetadata(url);
        }
    }

    // Reserves the frame height from the backend-known dimensions so the striped skeleton is
    // already the size the image will render at — the image then drops in with no vertical
    // jump. Bails when the frame isn't in the DOM yet or dimensions are unknown.
    private applyBackendDimensions(): void {
        const dimensions = this._dimensions();

        if (!this.frameRef() || !this.hasInitialDimensions() || dimensions == null)
            return;

        this.initPreviewHeight(
            dimensions.width,
            dimensions.height);

        // Arm the morph transition only after the first height is set + painted, so the open
        // snaps to size instead of animating up from nothing.
        if (!this._morphArmed) {
            this._morphArmed = true;
            requestAnimationFrame(() => this.morphReady.set(true));
        }
    }

    ngOnDestroy(): void {
        const current = this.objectUrl();
        if (current) URL.revokeObjectURL(current);
    }

    async loadImageWithMetadata(url: string): Promise<void> {
        try {
            const response = await fetch(url, {
                headers: this.httpHeadersFactory().prepareAdditionalHttpHeaders()
            });

            if (!response.ok) {
                console.error('Failed to load image:', response.status, response.statusText);
                return;
            }

            const arrayBuffer = await response.arrayBuffer();
            const mimeType = getMimeType(this.fileExtension());
            const blob = new Blob([arrayBuffer], { type: mimeType });
            const newObjectUrl = URL.createObjectURL(blob);

            // Atomic swap: prime the fade (loaded=false), set new URL, then revoke the old.
            // The <img> keeps rendering the old src until the new blob loads, but the opacity
            // dip + fade-in masks the moment of transition. Old URL is revoked AFTER the new
            // one is in place so there's never a flash of empty frame.
            const oldObjectUrl = this.objectUrl();
            this.isImageLoaded.set(false);
            this.objectUrl.set(newObjectUrl);
            if (oldObjectUrl) {
                URL.revokeObjectURL(oldObjectUrl);
            }

            // EXIF parsed from the new blob — emit either the formatted bag or null when
            // the file has no readable tags. Emitting null clears the parent's stale state
            // from the previous file; without it the metadata card would show wrong data.
            try {
                const exif = await exifr.parse(blob);

                if (exif) {
                    const formattedExif: Record<string, any> = {};

                    Object.entries(exif)
                        .filter(([_, value]) => value != null && value !== '')
                        .forEach(([key, value]) => {
                            const formattedKey = this.formatExifKey(key);
                            const formattedValue = this.formatExifValue(value);
                            formattedExif[formattedKey] = formattedValue;
                        });

                    this.exifChange.emit(formattedExif);
                } else {
                    this.exifChange.emit(null);
                }
            } catch (exifError) {
                console.log('No EXIF data available:', exifError);
                this.exifChange.emit(null);
            }

        } catch (error) {
            console.error('Error loading image metadata:', error);
        }
    }

    private formatExifKey(key: string): string {
        // Convert camelCase or snake_case to Title Case with spaces
        return key
            .replace(/([A-Z])/g, ' $1')
            .replace(/_/g, ' ')
            .replace(/^\w/, c => c.toUpperCase())
            .trim();
    }

    private formatExifValue(value: any): string {
        if (value instanceof Date) {
            return value.toLocaleString();
        }
        if (typeof value === 'number') {
            // Format numbers to a reasonable precision
            return Number.isInteger(value) ? value.toString() : value.toFixed(2);
        }
        return String(value);
    }

    onImageLoad(img: EventTarget | null): void {
        const image = img as HTMLImageElement;

        if(!image)
            return;

        this.dimensionsChange.emit({
            width: image.naturalWidth,
            height: image.naturalHeight
        });

        // Fallback sizing for files without backend dimensions (the effect above never fired).
        // For files that do have them, the height is already set to the same value, so this is
        // a no-op and there's no jump.
        if (!this.hasInitialDimensions()) {
            this.initPreviewHeight(
                image.naturalWidth,
                image.naturalHeight
            );
        }

        this.isImageLoaded.set(true);
    }

    // Derives the frame height (and the drag clamps) from the given dimensions. Capped at 70vh
    // and never wider than the frame. Everything is expressed as frame-box height (image height
    // + the frame's vertical padding) so the numbers map straight onto [style.height.px].
    private initPreviewHeight(width: number, height: number): void {
        const frame = this.frameRef()?.nativeElement;

        if (!frame || width <= 0 || height <= 0)
            return;

        const style = getComputedStyle(frame);
        const paddingX = parseFloat(style.paddingLeft) + parseFloat(style.paddingRight);
        const paddingY = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
        const innerWidth = frame.clientWidth - paddingX;

        this._innerWidth.set(innerWidth);
        this._frameVerticalPadding.set(paddingY);

        const ratio = width / height;
        // Image height at which it touches both side edges — past this point max-width:100%
        // pins the width, so dragging taller only adds empty frame. Hence it's also the max.
        const widthFitHeight = innerWidth / ratio;
        const viewportCap = window.innerHeight * 0.7;

        const initialImageHeight = Math.min(
            height,
            widthFitHeight,
            viewportCap
        );

        this.resizeMinHeight = this.MIN_IMAGE_HEIGHT + paddingY;
        this.maxPreviewHeight.set(Math.max(
            widthFitHeight + paddingY,
            this.resizeMinHeight
        ));

        this.resizeInitialHeight = this.clampHeight(initialImageHeight + paddingY);

        const storedHeight = this.readStoredHeight();
        this.previewHeight.set(storedHeight !== null
            ? this.clampHeight(storedHeight)
            : this.resizeInitialHeight);
    }

    private clampHeight(height: number): number {
        return Math.min(
            Math.max(height, this.resizeMinHeight),
            this.maxPreviewHeight());
    }

    onResizeStart(event: PointerEvent): void {
        const height = this.previewHeight();

        if (height == null)
            return;

        event.preventDefault();
        (event.target as HTMLElement).setPointerCapture(event.pointerId);

        this.resizeStartY = event.clientY;
        this.resizeStartHeight = height;
        this.isResizing.set(true);
    }

    onResizeMove(event: PointerEvent): void {
        if (!this.isResizing())
            return;

        const delta = event.clientY - this.resizeStartY;
        this.previewHeight.set(this.clampHeight(this.resizeStartHeight + delta));
    }

    onResizeEnd(event: PointerEvent): void {
        if (!this.isResizing())
            return;

        (event.target as HTMLElement).releasePointerCapture(event.pointerId);
        this.isResizing.set(false);

        const height = this.previewHeight();
        if (height !== null)
            this.writeStoredHeight(height);
    }

    onResizeReset(): void {
        if (this.resizeInitialHeight <= 0)
            return;

        this.previewHeight.set(this.resizeInitialHeight);
        this.clearStoredHeight();
    }

    private readStoredHeight(): number | null {
        try {
            const raw = localStorage.getItem(this.PREVIEW_HEIGHT_STORAGE_KEY);

            if (raw === null)
                return null;

            const value = parseFloat(raw);
            return Number.isFinite(value) ? value : null;
        } catch {
            // localStorage can be unavailable (private mode, disabled) — persistence is
            // best-effort, so a read failure just means "no remembered height".
            return null;
        }
    }

    private writeStoredHeight(height: number): void {
        try {
            localStorage.setItem(
                this.PREVIEW_HEIGHT_STORAGE_KEY,
                String(height)
            );
        } catch {
            // Best-effort persistence — ignore quota/availability errors.
        }
    }

    private clearStoredHeight(): void {
        try {
            localStorage.removeItem(this.PREVIEW_HEIGHT_STORAGE_KEY);
        } catch {
            // Best-effort — ignore.
        }
    }

    handleMediaError(event: Event): void {
        console.error('Failed to render image preview', event);
    }
}
