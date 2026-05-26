import { Component, computed, input, OnChanges, output, signal, SimpleChanges } from "@angular/core";
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
export class ImagePreviewComponent implements OnChanges {
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

    async ngOnChanges(changes: SimpleChanges): Promise<void> {
        const url = this.fileUrl();

        if(changes['fileUrl'] && url) {
            this.resetState();
            await this.loadImageWithMetadata(url);
        }
    }

    private resetState() {
        this.objectUrl.set(null);
        this.dimensionsChange.emit(null);
        this.exifChange.emit(null);
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

            this.objectUrl.set(URL.createObjectURL(blob));

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
                }
            } catch (exifError) {
                console.log('No EXIF data available:', exifError);
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
    }

    handleMediaError(event: any): void {
        console.error('Error loading media:', event);
    }
}
