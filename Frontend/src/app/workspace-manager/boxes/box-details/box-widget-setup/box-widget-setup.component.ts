import { Component, OnInit, Signal, computed, signal, ViewEncapsulation, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormsModule, NgForm } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActionButtonComponent } from '../../../../shared/buttons/action-btn/action-btn.component';
import { ClipboardModule, Clipboard } from '@angular/cdk/clipboard';
import { WidgetsApi } from '../../../../services/widgets.api';
import { AppEmailProvider } from '../../../../shared/email-provider-item/email-provider-item.component';

type OriginControl = {
    value: string;
    id: string;
}

@Component({
    selector: 'app-widget-setup',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        ActionButtonComponent,
        ClipboardModule
    ],
    templateUrl: './box-widget-setup.component.html',
    styleUrls: ['./box-widget-setup.component.scss'],
    encapsulation: ViewEncapsulation.None
})
export class BoxWidgetSetupComponent implements OnInit {
    origins = signal<OriginControl[]>([]);
    widgetUrl = signal<string>('');
    
    scriptTags = signal<string | null>(null);
    
    widgetTag = computed(() => 
`<plikshare-box-widget 
    url="${this.widgetUrl()}">
</plikshare-box-widget>`
    );
    
    constructor(
        private _widgetsApi: WidgetsApi,
        private _clipboard: Clipboard,
        private _snackBar: MatSnackBar,
        public dialogRef: MatDialogRef<BoxWidgetSetupComponent>,
        @Inject(MAT_DIALOG_DATA) public data: {
            url: string,
            origins: string[]
        }) {
            this.widgetUrl.set(data.url);
            this.origins.set(data.origins.map(origin => ({
                value: origin,
                id: crypto.randomUUID()
            })));

            if(data.origins.length == 0) {
                this.addOrigin();
            }
        }

    async ngOnInit(): Promise<void> {
        try {
            const scripts = await this._widgetsApi.getWidgetScripts();
            this.scriptTags.set(scripts);
        } catch (error) {
            console.error(error);
        }
    }

    addOrigin() {
        this.origins.update(origins => [...origins, {
            value: '',
            id: crypto.randomUUID()
        }]);
    }

    removeOrigin(id: string) {
        this.origins.update(origins => {
            const index = origins.findIndex(origin => origin.id === id);
            origins.splice(index, 1);
            return origins;
        });
    }

    copyWidgetTagToClipboard() {
        if(this._clipboard.copy(this.widgetTag())) {
            this.animateCopy('.icon-copy-tag');
        }
    }

    copyWidgetScriptsToClipboard() {  
        const scripts = this.scriptTags();

        if(!scripts)
            return;
        
        if(this._clipboard.copy(scripts)) {
            this.animateCopy('.icon-copy-scripts');
        }
    }

    private animateCopy(copySelector: string) {
        const iconElement = document.querySelector(copySelector);

        if (iconElement) {
            iconElement.classList.add('copy-animation');
            setTimeout(() => {
                iconElement.classList.remove('copy-animation');
            }, 300);
        }

        this._snackBar.open('Copied to clipboard', 'Close', {
            duration: 2000,
        });
    }

    onSave() {
        const originValues = this.origins().map(o => o.value);
        this.dialogRef.close(originValues);
    }

    onCancel() {
        this.dialogRef.close();
    }
}
