import { Component, OnChanges, OnInit, SimpleChanges, inject, input, output, signal } from "@angular/core";
import { MatSelectModule } from "@angular/material/select";
import { FormsModule } from "@angular/forms";
import { ThumbnailsPolicyDto, WorkspacesApi } from "../../services/workspaces.api";
import { ThumbnailVariant } from "../../services/folders-and-files.api";
import { ActionTextButtonComponent } from "../buttons/action-text-btn/action-text-btn.component";
import { Debouncer } from "../../services/debouncer";

export type ThumbnailsPolicyConfigChangedEvent = {
    thumbnails: ThumbnailsPolicyDto;
};

const ALL_VARIANTS: ThumbnailVariant[] = ['Mini', 'Small', 'Large'];

@Component({
    selector: 'app-thumbnails-policy-config',
    standalone: true,
    imports: [
        MatSelectModule,
        FormsModule,
        ActionTextButtonComponent
    ],
    templateUrl: './thumbnails-policy-config.component.html',
    styleUrl: './thumbnails-policy-config.component.scss'
})
export class ThumbnailsPolicyConfigComponent implements OnInit, OnChanges {
    thumbnails = input.required<ThumbnailsPolicyDto>();
    workspaceExternalId = input.required<string>();
    hasActiveBackfill = input(false);
    configChanged = output<ThumbnailsPolicyConfigChangedEvent>();

    private _workspacesApi = inject(WorkspacesApi);

    modeOptions: { value: boolean, label: string }[] = [
        { value: false, label: 'Disabled' },
        { value: true, label: 'Generate on upload' }
    ];

    variantOptions: { value: ThumbnailVariant, name: string, size: string, description: string }[] = [
        { value: 'Mini', name: 'Mini', size: '~96px', description: 'File-list rows and other compact spots.' },
        { value: 'Small', name: 'Small', size: '~400px', description: 'Gallery tiles and grid previews.' },
        { value: 'Large', name: 'Large', size: '~1600px', description: 'Full-size preview and high-DPI screens.' }
    ];

    generateOnUpload: boolean = false;
    selectedVariants = new Set<ThumbnailVariant>();

    // Signals, not plain fields — the app is zoneless and the count resolves in a debounced
    // async callback, so a plain-field mutation would never schedule a re-render.
    isPending = signal(false);
    missingCount = signal<number | null>(null);
    isCountLoading = signal(false);

    private _savedGenerateOnUpload: boolean = false;
    private _savedVariantsKey: string = '';

    private _countDebouncer = new Debouncer(400);
    private _countRequestId = 0;

    ngOnInit() {
        this.initialize();
    }

    ngOnChanges(changes: SimpleChanges) {
        if (changes['thumbnails']) {
            this.initialize();
        }
    }

    private initialize() {
        const policy = this.thumbnails();

        this.generateOnUpload = policy.generateOnUpload;
        this.selectedVariants = new Set(policy.variants);

        this._savedGenerateOnUpload = policy.generateOnUpload;
        this._savedVariantsKey = this.variantsKey();

        this.isPending.set(false);
        this.missingCount.set(null);
        this.isCountLoading.set(false);
        this._countRequestId++;
    }

    private variantsKey(): string {
        return ALL_VARIANTS
            .filter(variant => this.selectedVariants.has(variant))
            .join(',');
    }

    isVariantSelected(variant: ThumbnailVariant): boolean {
        return this.selectedVariants.has(variant);
    }

    onModeChange() {
        // Enabling with nothing ticked would be a no-op policy — preselect everything and let
        // the user narrow it down before applying.
        if (this.generateOnUpload && this.selectedVariants.size === 0) {
            this.selectedVariants = new Set(ALL_VARIANTS);
        }

        this.onDraftChange();
    }

    toggleVariant(variant: ThumbnailVariant) {
        if (this.selectedVariants.has(variant)) {
            this.selectedVariants.delete(variant);
        } else {
            this.selectedVariants.add(variant);
        }

        this.onDraftChange();
    }

    private onDraftChange() {
        this.isPending.set(
            this.generateOnUpload !== this._savedGenerateOnUpload
            || (this.generateOnUpload && this.variantsKey() !== this._savedVariantsKey));

        this.refreshMissingCount();
    }

    private refreshMissingCount() {
        const requestId = ++this._countRequestId;
        this.missingCount.set(null);

        if (!this.isPending() || !this.generateOnUpload || this.selectedVariants.size === 0) {
            this.isCountLoading.set(false);
            return;
        }

        this.isCountLoading.set(true);

        const variants = ALL_VARIANTS.filter(
            variant => this.selectedVariants.has(variant));

        this._countDebouncer.debounceAsync(async () => {
            try {
                const result = await this._workspacesApi.getThumbnailsBackfillCount(
                    this.workspaceExternalId(),
                    variants);

                if (requestId !== this._countRequestId)
                    return;

                this.missingCount.set(result.fileCount);
            } catch (err) {
                console.error('Failed to count images missing thumbnails', err);
            } finally {
                if (requestId === this._countRequestId) {
                    this.isCountLoading.set(false);
                }
            }
        });
    }

    canApply(): boolean {
        return this.isPending()
            && (!this.generateOnUpload || this.selectedVariants.size > 0);
    }

    isDangerApply(): boolean {
        return !this.generateOnUpload && this.hasActiveBackfill();
    }

    pendingMessage(): string {
        if (!this.generateOnUpload) {
            return this.hasActiveBackfill()
                ? 'Pending thumbnail generation will be cancelled (already generated thumbnails are kept) '
                    + 'and new uploads will no longer get thumbnails automatically.'
                : 'New uploads will no longer get thumbnails automatically.';
        }

        if (this.selectedVariants.size === 0)
            return 'Select at least one thumbnail size.';

        if (this.isCountLoading())
            return 'Checking existing images…';

        const missingCount = this.missingCount();

        if (missingCount === null)
            return 'The selected thumbnails will be generated for every image uploaded from now on.';

        if (missingCount > 0)
            return `${missingCount} existing image(s) are missing the selected thumbnails and will get them, `
                + 'along with every image uploaded from now on.';

        return 'No existing images are missing the selected thumbnails — the change applies to new uploads only.';
    }

    apply() {
        if (!this.canApply())
            return;

        this.configChanged.emit({
            thumbnails: {
                generateOnUpload: this.generateOnUpload,
                variants: ALL_VARIANTS.filter(variant => this.selectedVariants.has(variant))
            }
        });
    }

    cancel() {
        this.initialize();
    }
}
