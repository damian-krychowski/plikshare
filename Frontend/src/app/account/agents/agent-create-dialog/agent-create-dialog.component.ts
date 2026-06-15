import { Component, signal, ViewEncapsulation, WritableSignal } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';

@Component({
    selector: 'app-agent-create-dialog',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule
    ],
    templateUrl: './agent-create-dialog.component.html',
    styleUrl: './agent-create-dialog.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class AgentCreateDialogComponent {
    name: WritableSignal<string> = signal('');

    constructor(
        public dialogRef: MatDialogRef<AgentCreateDialogComponent>) {
    }

    onCreate() {
        const value = this.name().trim();

        if (!value)
            return;

        this.dialogRef.close(value);
    }

    onCancel() {
        this.dialogRef.close();
    }
}
