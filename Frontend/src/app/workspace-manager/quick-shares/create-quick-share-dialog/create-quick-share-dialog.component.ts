import { Component, Inject, ViewEncapsulation, computed, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CreateQuickShareRequest, QuickShareMode } from '../../../services/quick-shares.api';

export interface CreateQuickShareDialogData {
    selectedFiles: string[];
    selectedFolders: string[];
    excludedFiles: string[];
    excludedFolders: string[];
    defaultName: string;
    appUrl: string;
}

const SLUG_REGEX = /^[a-z0-9][a-z0-9-]{1,98}[a-z0-9]$/;

@Component({
    selector: 'app-create-quick-share-dialog',
    imports: [
        FormsModule,
        MatButtonModule,
        MatCheckboxModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatTooltipModule
    ],
    templateUrl: './create-quick-share-dialog.component.html',
    styleUrl: './create-quick-share-dialog.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CreateQuickShareDialogComponent {
    name = signal('');
    mode = signal<QuickShareMode>('browser');
    allowIndividualFileDownload = signal(true);

    useCustomSlug = signal(false);
    customSlug = signal('');
    isCustomSlugValid = computed(() => {
        if (!this.useCustomSlug()) return true;
        const slug = this.customSlug().trim().toLowerCase();
        return slug.length >= 3 && slug.length <= 100 && SLUG_REGEX.test(slug);
    });

    hasExpiration = signal(false);
    expiresAtIso = signal('');

    hasPassword = signal(false);
    password = signal('');

    hasMaxDownloads = signal(false);
    maxDownloads = signal<number | null>(10);

    isSubmitting = signal(false);

    canSubmit = computed(() => {
        if (this.isSubmitting()) return false;
        if (!this.name().trim()) return false;
        if (!this.isCustomSlugValid()) return false;
        if (this.hasPassword() && !this.password().trim()) return false;
        if (this.hasMaxDownloads() && (this.maxDownloads() ?? 0) <= 0) return false;
        if (this.hasExpiration() && !this.expiresAtIso()) return false;
        return true;
    });

    constructor(
        @Inject(MAT_DIALOG_DATA) public data: CreateQuickShareDialogData,
        public dialogRef: MatDialogRef<CreateQuickShareDialogComponent, CreateQuickShareRequest | null>
    ) {
        this.name.set(data.defaultName);
    }

    onCancel() {
        this.dialogRef.close(null);
    }

    onSubmit() {
        if (!this.canSubmit()) return;

        this.isSubmitting.set(true);

        const request: CreateQuickShareRequest = {
            name: this.name().trim(),
            customSlug: this.useCustomSlug() ? this.customSlug().trim().toLowerCase() : null,
            selectedFiles: this.data.selectedFiles,
            selectedFolders: this.data.selectedFolders,
            excludedFiles: this.data.excludedFiles,
            excludedFolders: this.data.excludedFolders,
            mode: this.mode(),
            allowIndividualFileDownload: this.allowIndividualFileDownload(),
            expiresAt: this.hasExpiration() ? new Date(this.expiresAtIso()).toISOString() : null,
            password: this.hasPassword() ? this.password() : null,
            maxDownloads: this.hasMaxDownloads() ? this.maxDownloads() : null
        };

        this.dialogRef.close(request);
    }
}
