import { Component, computed, ElementRef, input, OnChanges, OnDestroy, output, signal, SimpleChanges, viewChild } from "@angular/core";
import { ImageDimensions, ImageExif } from "../../file-inline-preview/file-inline-preview.component";
import { getMimeType } from "../../../services/filte-type";
import exifr from "exifr";
import { HttpHeadersFactory } from "../../http-headers-factory";

@Component({
    selector: 'app-image-preview',
    imports: [],
    templateUrl: './image-preview.component.html',
    styleUrls: ['./image-preview.component.scss']
})
export class ImagePreviewComponent implements OnChanges, OnDestroy {
    fileUrl = input.required<string>();
    fileName = input.required<string>();
    fileExtension = input.required<string>();
    httpHeadersFactory = input.required<HttpHeadersFactory>();

    // Metadata is consumed by the parent (file-inline-preview) which renders it next to
    // the thumbnail cards in the "Image" section. Image-preview itself stays focused on
    // displaying the image; it just sources the data because it owns the fetched blob.
    dimensionsChange = output<ImageDimensions | null>();
    exifChange = output<ImageExif | null>();

    fileFullName = computed(() => this.fileName() + this.fileExtension());

    frameRef = viewChild<ElementRef<HTMLDivElement>>('frame');

    // Frame height in px driven by the bottom resize handle. Null before the first image
    // loads — in that state the frame sizes to content and the <img> keeps its CSS 70vh cap
    // (the `--sized` class is absent), so initial behavior is unchanged. Once an image loads
    // we compute a concrete height; from then on the frame box is fixed and the image scales
    // to fit it via object-fit: contain.
    previewHeight = signal<number | null>(null);
    isResizing = signal(false);

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

    async ngOnChanges(changes: SimpleChanges): Promise<void> {
        const url = this.fileUrl();

        if(changes['fileUrl'] && url) {
            await this.loadImageWithMetadata(url);
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

        this.initPreviewHeight(
            image.naturalWidth,
            image.naturalHeight
        );

        this.isImageLoaded.set(true);
    }

    // Derives the frame height (and the drag clamps) from the just-loaded image so the first
    // render matches the legacy look: capped at 70vh, never wider than the frame. Everything
    // is expressed as frame-box height (image height + the frame's vertical padding) so the
    // numbers map straight onto [style.height.px]. Recomputed per image, which intentionally
    // resets any height the user dragged on the previous file.
    private initPreviewHeight(naturalWidth: number, naturalHeight: number): void {
        const frame = this.frameRef()?.nativeElement;

        if (!frame || naturalWidth <= 0 || naturalHeight <= 0)
            return;

        const style = getComputedStyle(frame);
        const paddingX = parseFloat(style.paddingLeft) + parseFloat(style.paddingRight);
        const paddingY = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
        const innerWidth = frame.clientWidth - paddingX;

        const ratio = naturalWidth / naturalHeight;
        // Image height at which it touches both side edges — past this point max-width:100%
        // pins the width, so dragging taller only adds empty frame. Hence it's also the max.
        const widthFitHeight = innerWidth / ratio;
        const viewportCap = window.innerHeight * 0.7;

        const initialImageHeight = Math.min(
            naturalHeight,
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
            // Best-effort — ignore availability errors.
        }
    }

    private clampHeight(height: number): number {
        if (height < this.resizeMinHeight)
            return this.resizeMinHeight;

        const maxHeight = this.maxPreviewHeight();

        if (height > maxHeight)
            return maxHeight;

        return height;
    }

    handleMediaError(event: any): void {
        console.error('Error loading media:', event);
    }
}
