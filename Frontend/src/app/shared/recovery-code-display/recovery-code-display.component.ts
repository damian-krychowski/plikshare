import { Component, Inject, ViewEncapsulation, computed, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FormsModule } from '@angular/forms';
import { AppStorageEncryptionType } from '../../services/storages.api';

export interface RecoveryCodeDisplayDialogData {
    recoveryCode: string;
    storageName: string;
    encryptionType: AppStorageEncryptionType;
}

@Component({
    selector: 'app-recovery-code-display',
    imports: [
        FormsModule,
        MatButtonModule,
        MatCheckboxModule
    ],
    templateUrl: './recovery-code-display.component.html',
    styleUrl: './recovery-code-display.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class RecoveryCodeDisplayComponent {
    hasSaved = signal(false);
    copied = signal(false);

    words = computed(() => this.data.recoveryCode.split(/\s+/).filter(w => w.length > 0));

    constructor(
        @Inject(MAT_DIALOG_DATA) public data: RecoveryCodeDisplayDialogData,
        public dialogRef: MatDialogRef<RecoveryCodeDisplayComponent>) {
    }

    async copyToClipboard() {
        await navigator.clipboard.writeText(this.data.recoveryCode);
        this.copied.set(true);
        setTimeout(() => this.copied.set(false), 2000);
    }

    downloadAsFile() {
        const safeName = this.data.storageName.replace(/[^a-zA-Z0-9-_]/g, '_');

        const warning = this.data.encryptionType === 'full'
            ? `This code is the ONLY way to regain access to this storage if you forget
your password — or if the database is ever lost or damaged. It will not
be shown again. Anyone who obtains this code can access your files —
guard it like a password.`
            : `If the database is ever lost or damaged, this code is the ONLY way to
decrypt this storage's files. It will not be shown again. Anyone who
obtains this code can decrypt your files — guard it like a password.`;

        const content =
`PlikShare storage recovery code
Storage: ${this.data.storageName}
Generated: ${new Date().toISOString()}

${this.formatWordsForFile()}

WARNING
-------
${warning}
`;

        const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `plikshare-recovery-${safeName}.txt`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    }

    onDone() {
        if (this.hasSaved())
            this.dialogRef.close(true);
    }

    private formatWordsForFile(): string {
        const words = this.words();
        const lines: string[] = [];
        for (let row = 0; row < 6; row++) {
            const parts: string[] = [];
            for (let col = 0; col < 4; col++) {
                const idx = col * 6 + row;
                if (idx < words.length) {
                    const n = (idx + 1).toString().padStart(2, ' ');
                    parts.push(`${n}. ${words[idx].padEnd(10, ' ')}`);
                }
            }
            lines.push(parts.join('  '));
        }
        return lines.join('\n');
    }
}
