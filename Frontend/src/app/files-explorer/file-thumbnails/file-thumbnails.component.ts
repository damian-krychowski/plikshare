import { Component, computed, effect, EventEmitter, inject, input, OnDestroy, Output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { ConfigCardComponent } from '../../shared/config-card/config-card.component';
import { StorageSizePipe } from '../../shared/storage-size.pipe';
import { AppCapabilitiesService } from '../../services/app-capabilities.service';
import { ContentDisposition, FilePreviewThumbnail, GetFileDownloadLinkResponse, ThumbnailVariant, UploadFileThumbnailRequest } from '../../services/folders-and-files.api';
import { getBase62Guid } from '../../services/guid-base-62';
import { DropFilesDirective } from '../directives/drop-files.directive';
import { MatDialog } from '@angular/material/dialog';
import { OperationConfirmComponent } from '../../shared/operation-confirm/operation-confirm.component';
import { firstValueFrom } from 'rxjs';

export type FileThumbnailsOperations = {
    uploadFileThumbnail: (request: UploadFileThumbnailRequest) => Promise<void>;
    deleteFileThumbnail: (variant: ThumbnailVariant) => Promise<void>;
    generateFileThumbnails: (variants: ThumbnailVariant[]) => Promise<void>;
    getDownloadLink: (fileExternalId: string, contentDisposition: ContentDisposition) => Promise<GetFileDownloadLinkResponse>;
    prepareAdditionalHttpHeaders: () => Record<string, string> | undefined;
};

type SlotState = {
    variant: ThumbnailVariant;
    label: string;
    targetSize: string;
    description: string;
    existing: FilePreviewThumbnail | null;
    objectUrl: string | null;
    isLoading: boolean;
    isUploading: boolean;
    isGenerating: boolean;
};

const SLOT_DEFS: { variant: ThumbnailVariant; label: string; targetSize: string; description: string }[] = [
    { variant: 'Mini', label: 'Mini thumbnail', targetSize: '~128px', description: 'File-list rows and other compact spots.' },
    { variant: 'Small', label: 'Small thumbnail', targetSize: '~400px', description: 'Gallery tiles and grid previews.' },
    { variant: 'Large', label: 'Large thumbnail', targetSize: '~1600px', description: 'Full-size preview and high-DPI screens.' },
];

@Component({
    selector: 'app-file-thumbnails',
    standalone: true,
    imports: [
        MatButtonModule,
        MatMenuModule,
        MatTooltipModule,
        MatProgressSpinnerModule,
        ActionButtonComponent,
        ConfigCardComponent,
        StorageSizePipe,
        DropFilesDirective,
    ],
    templateUrl: './file-thumbnails.component.html',
    styleUrl: './file-thumbnails.component.scss',
})
export class FileThumbnailsComponent implements OnDestroy {
    fileExternalId = input.required<string>();
    thumbnails = input.required<FilePreviewThumbnail[]>();
    operations = input.required<FileThumbnailsOperations>();

    @Output() changed = new EventEmitter<void>();

    private _capabilities = inject(AppCapabilitiesService);
    private _dialog = inject(MatDialog);

    isFfmpegAvailable = computed(() => this._capabilities.capabilities().isFfmpegAvailable);

    isExpanded = signal(true);

    slots = signal<SlotState[]>(SLOT_DEFS.map(def => ({
        variant: def.variant,
        label: def.label,
        targetSize: def.targetSize,
        description: def.description,
        existing: null,
        objectUrl: null,
        isLoading: false,
        isUploading: false,
        isGenerating: false,
    })));

    anyGenerating = computed(() => this.slots().some(s => s.isGenerating));
    anyExisting = computed(() => this.slots().some(s => s.existing));

    constructor() {
        // Capabilities are app-wide; first component to mount fires the HTTP, others reuse.
        this._capabilities.ensureLoaded();

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

    public async generateVariants(variants: ThumbnailVariant[]): Promise<void> {
        if (variants.length === 0) return;

        // Reject duplicate triggers per slot; the global path may overlap with a still-running
        // per-slot click — only the slots that aren't already generating start.
        const toGenerate = variants.filter(v =>
            !this.slots().find(s => s.variant === v)?.isGenerating);

        if (toGenerate.length === 0) return;

        if (!await this.confirmOverwrite(toGenerate)) return;

        for (const variant of toGenerate) {
            this.patchSlot(variant, s => ({ ...s, isGenerating: true }));
        }

        try {
            await this.operations().generateFileThumbnails(toGenerate);

            // Queue worker is async — emit changed periodically so parent re-fetches preview
            // details. Stop early once every requested variant shows up on the slot side.
            for (let i = 0; i < 10; i++) {
                await new Promise(r => setTimeout(r, 1500));
                this.changed.emit();

                const allPresent = toGenerate.every(v =>
                    this.slots().find(s => s.variant === v)?.existing != null);
                if (allPresent) break;
            }
        } catch (err) {
            console.error('Thumbnail generation failed:', err);
        } finally {
            for (const variant of toGenerate) {
                this.patchSlot(variant, s => ({ ...s, isGenerating: false }));
            }
        }
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

        if (file)
            await this.uploadFile(file, variant);
    }

    async onFilesDropped(files: FileList, variant: ThumbnailVariant): Promise<void> {
        const file = files?.[0];

        // Backend validates that the file is an image; bail early on an obvious mismatch so a
        // mistaken drop (eg. a PDF) doesn't fire a doomed upload round-trip.
        if (!file || !file.type.startsWith('image/'))
            return;

        await this.uploadFile(file, variant);
    }

    private async uploadFile(file: File, variant: ThumbnailVariant): Promise<void> {
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
        const slot = this.slots().find(s => s.variant === variant);

        if (!slot?.existing)
            return;

        const confirmed = await this.confirm({
            item: `the ${slot.label.toLowerCase()}`,
            verb: 'delete',
            isDanger: true,
            subtitle: 'You can generate or upload it again afterwards.'
        });

        if (!confirmed)
            return;

        await this.deleteVariant(variant);
        this.changed.emit();
    }

    async deleteAll(): Promise<void> {
        const existing = this.slots().filter(s => s.existing);

        if (existing.length === 0)
            return;

        const isSingle = existing.length === 1;

        const confirmed = await this.confirm({
            item: isSingle
                ? `the ${existing[0].label.toLowerCase()}`
                : `${existing.length} thumbnails`,
            verb: 'delete',
            isDanger: true,
            subtitle: isSingle
                ? 'You can generate or upload it again afterwards.'
                : 'You can generate or upload them again afterwards.'
        });

        if (!confirmed)
            return;

        for (const slot of existing) {
            await this.deleteVariant(slot.variant);
        }

        this.changed.emit();
    }

    private async deleteVariant(variant: ThumbnailVariant): Promise<void> {
        try {
            await this.operations().deleteFileThumbnail(variant);
        } catch (err) {
            console.error('Thumbnail delete failed:', err);
        }
    }

    onGenerateVariant(variant: ThumbnailVariant): Promise<void> {
        return this.generateVariants([variant]);
    }

    generateAll(): Promise<void> {
        return this.generateVariants(this.slots().map(s => s.variant));
    }

    // Generation silently overwrites a variant that already exists. Prompt before clobbering
    // any existing thumbnail (which may have been manually uploaded); skip the dialog entirely
    // when every target slot is empty so the common first-time path stays one-click.
    private confirmOverwrite(variants: ThumbnailVariant[]): Promise<boolean> {
        const existing = this.slots().filter(
            s => variants.includes(s.variant) && s.existing);

        if (existing.length === 0)
            return Promise.resolve(true);

        const isSingle = existing.length === 1;

        return this.confirm({
            item: isSingle
                ? `the ${existing[0].label.toLowerCase()}`
                : `${existing.length} existing thumbnails`,
            verb: 'overwrite',
            isDanger: false,
            subtitle: isSingle
                ? 'The current image will be replaced with a freshly generated thumbnail.'
                : 'The current images will be replaced with freshly generated thumbnails.'
        });
    }

    private async confirm(data: {
        item: string;
        verb: string;
        isDanger: boolean;
        subtitle: string;
    }): Promise<boolean> {
        const dialogRef = this._dialog.open(OperationConfirmComponent, {
            width: '400px',
            maxHeight: '600px',
            position: { top: '100px' },
            data
        });

        return !!(await firstValueFrom(dialogRef.afterClosed()));
    }
}
