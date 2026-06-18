import { Component, Inject, ViewEncapsulation, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FormsModule } from '@angular/forms';
import { ConfigCardComponent } from '../../../shared/config-card/config-card.component';

export interface AgentTokenDialogData {
    title: string;
    token: string;
}

@Component({
    selector: 'app-agent-token-dialog',
    imports: [
        FormsModule,
        MatButtonModule,
        MatCheckboxModule,
        ConfigCardComponent
    ],
    templateUrl: './agent-token-dialog.component.html',
    styleUrl: './agent-token-dialog.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class AgentTokenDialogComponent {
    hasSaved = signal(false);
    copied = signal(false);

    constructor(
        @Inject(MAT_DIALOG_DATA) public data: AgentTokenDialogData,
        public dialogRef: MatDialogRef<AgentTokenDialogComponent>) {
    }

    async copyToClipboard() {
        await navigator.clipboard.writeText(this.data.token);
        this.copied.set(true);
        setTimeout(() => this.copied.set(false), 2000);
    }

    onDone() {
        if (this.hasSaved())
            this.dialogRef.close(true);
    }
}
