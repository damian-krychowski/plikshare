import { Component, effect, EventEmitter, input, OnDestroy, Output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { ConfigCardComponent } from '../../shared/config-card/config-card.component';
import { StorageSizePipe } from '../../shared/storage-size.pipe';
import { ContentDisposition, FilePreviewThumbnail, GetFileDownloadLinkResponse, ThumbnailVariant, UploadFileThumbnailRequest } from '../../services/folders-and-files.api';
import { getBase62Guid } from '../../services/guid-base-62';

export type FileThumbnailsOperations = {
    uploadFileThumbnail: (request: UploadFileThumbnailRequest) => Promise<void>;
    deleteFileThumbnail: (variant: ThumbnailVariant) => Promise<void>;
    getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => Promise<GetFileDownloadLinkResponse>;
    prepareAdditionalHttpHeaders: () => Record<string, string> | undefined;
};

type SlotState = {
    variant: ThumbnailVariant;
    label: string;
    targetSize: string;
    existing: FilePreviewThumbnail | null;
    objectUrl: string | null;
    isLoading: boolean;
    isUploading: boolean;
};

const SLOT_DEFS: { variant: ThumbnailVariant; label: string; targetSize: string }[] = [
    { variant: 'Small', label: 'Small thumbnail', targetSize: '~400px' },
    { variant: 'Large', label: 'Large thumbnail', targetSize: '~1600px' },
];

@Component({
    selector: 'app-file-thumbnails',
    standalone: true,
    imports: [
        MatButtonModule,
        MatTooltipModule,
        MatProgressSpinnerModule,
        ActionButtonComponent,
        ConfigCardComponent,
        StorageSizePipe,
    ],
    templateUrl: './file-thumbnails.component.html',
    styleUrl: './file-thumbnails.component.scss',
})
export class FileThumbnailsComponent implements OnDestroy {
    fileExternalId = input.required<string>();
    thumbnails = input.required<FilePreviewThumbnail[]>();
    operations = input.required<FileThumbnailsOperations>();

    @Output() changed = new EventEmitter<void>();

    slots = signal<SlotState[]>(SLOT_DEFS.map(def => ({
        variant: def.variant,
        label: def.label,
        targetSize: def.targetSize,
        existing: null,
        objectUrl: null,
        isLoading: false,
        isUploading: false,
    })));

    constructor() {
        // Reconcile slot.existing against the latest thumbnails input. Preserves objectUrl
        // for variants whose externalId is unchanged so the image doesn't flicker; revokes +
        // clears object URLs whose thumbnail was replaced or removed; then kicks off any
        // pending preview-blob fetches.
        effect(() => {
            const incoming = this.thumbnails();
            this.slots.update(current => current.map(slot => {
                const existing = incoming.find(t => t.variant === slot.variant) ?? null;
                const sameId = slot.existing?.externalId === existing?.externalId;

                if (sameId) return { ...slot, existing };

                if (slot.objectUrl) URL.revokeObjectURL(slot.objectUrl);

                return { ...slot, existing, objectUrl: null };
            }));

            this.syncObjectUrls();
        });
    }

    ngOnDestroy(): void {
        for (const slot of this.slots()) {
            if (slot.objectUrl) URL.revokeObjectURL(slot.objectUrl);
        }
    }

    private async syncObjectUrls(): Promise<void> {
        const snapshot = this.slots();
        const ops = this.operations();

        for (const slot of snapshot) {
            if (!slot.existing || slot.objectUrl || slot.isLoading) continue;

            this.patchSlot(
                slot.variant,
                s => ({ ...s, isLoading: true }));

            try {
                const link = await ops.getDownloadLink(
                    slot.existing.externalId,
                    'inline');

                const response = await fetch(link.downloadPreSignedUrl, {
                    headers: ops.prepareAdditionalHttpHeaders(),
                });

                if (!response.ok) throw new Error(`Thumbnail fetch failed: ${response.status}`);

                const blob = await response.blob();
                const url = URL.createObjectURL(blob);

                this.patchSlot(
                    slot.variant,
                    s => {
                        // Slot may have changed between the fetch start and now (variant
                        // replaced or deleted) — drop the freshly-fetched URL in that case.
                        if (s.existing?.externalId !== slot.existing!.externalId) {
                            URL.revokeObjectURL(url);
                            return s;
                        }
                        return { ...s, objectUrl: url, isLoading: false };
                    });
            } catch (err) {
                console.error('Failed to load thumbnail blob:', err);
                this.patchSlot(
                    slot.variant,
                    s => ({ ...s, isLoading: false }));
            }
        }
    }

    private patchSlot(
        variant: ThumbnailVariant,
        update: (s: SlotState) => SlotState): void {
        this.slots.update(slots => slots.map(s => s.variant === variant ? update(s) : s));
    }

    async onFilePicked(event: Event, variant: ThumbnailVariant): Promise<void> {
        const inputEl = event.target as HTMLInputElement;
        const file = inputEl.files?.[0];
        inputEl.value = '';

        if (!file) return;

        const lastDot = file.name.lastIndexOf('.');
        const name = lastDot >= 0 ? file.name.substring(0, lastDot) : file.name;
        const extension = lastDot >= 0 ? file.name.substring(lastDot).toLowerCase() : '';

        this.patchSlot(
            variant,
            s => ({ ...s, isUploading: true }));

        try {
            await this.operations().uploadFileThumbnail({
                externalId: `fi_${getBase62Guid()}`,
                name,
                extension,
                file,
                variant,
            });

            this.changed.emit();
        } catch (err) {
            console.error('Thumbnail upload failed:', err);
        } finally {
            this.patchSlot(
                variant,
                s => ({ ...s, isUploading: false }));
        }
    }

    async onDelete(variant: ThumbnailVariant): Promise<void> {
        try {
            await this.operations().deleteFileThumbnail(variant);
            this.changed.emit();
        } catch (err) {
            console.error('Thumbnail delete failed:', err);
        }
    }
}
