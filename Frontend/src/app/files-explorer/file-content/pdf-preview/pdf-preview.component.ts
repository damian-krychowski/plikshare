import { Component, computed, input, signal } from "@angular/core";
import { DomSanitizer, SafeResourceUrl } from "@angular/platform-browser";

@Component({
    selector: 'app-pdf-preview',
    imports: [],
    templateUrl: './pdf-preview.component.html',
    styleUrls: ['./pdf-preview.component.scss']
})
export class PdfPreviewComponent {
    fileUrl = input.required<string>();

    safeFileDataUrl = computed(() => this.getFileDataSafeUrl(this.fileUrl()))
    
    constructor(
        private _sanitizer: DomSanitizer){            
    }
        
    private getFileDataSafeUrl(fileUrl: string) {    
        if(!fileUrl)
            return null;

        const safePath = this
            ._sanitizer
            .bypassSecurityTrustResourceUrl(fileUrl);

        return safePath;
    }
}