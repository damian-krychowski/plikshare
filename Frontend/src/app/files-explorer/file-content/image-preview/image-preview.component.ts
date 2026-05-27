import { Component, computed, input, OnChanges, OnDestroy, output, signal, SimpleChanges } from "@angular/core";
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

        this.isImageLoaded.set(true);
    }

    handleMediaError(event: any): void {
        console.error('Error loading media:', event);
    }
}
