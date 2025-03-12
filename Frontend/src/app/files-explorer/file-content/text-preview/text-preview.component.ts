import { Component, ViewChild, ElementRef, OnDestroy, input, signal, computed, OnChanges, SimpleChanges } from '@angular/core';

@Component({
    selector: 'app-text-preview',
    templateUrl: './text-preview.component.html',
    styleUrl: './text-preview.component.scss'
})
export class TextPreviewComponent implements OnChanges, OnDestroy {
    fileUrl = input.required<string>();
    initialHeight = input(500);

    @ViewChild('previewContainer') previewContainer!: ElementRef;
    @ViewChild('resizeHandle') resizeHandle!: ElementRef;

    previewHeight = signal<number>(0)

    fileText = signal<string | null>(null);

    private isResizing = false;

    constructor() {
        this.previewHeight.set(this.initialHeight());
    }
    
    async ngOnChanges(changes: SimpleChanges): Promise<void> {
        const fileUrl = this.fileUrl();

        if(changes['fileUrl'] && fileUrl) {
            this.fileText.set(null);
            await this.loadText(fileUrl);
        }
    }

    private async loadText(url: string): Promise<boolean> {
        try {
            const response = await fetch(url);
            const text = await response.text();
    
            this.fileText.set(text);
            return true;
        } catch (error) {
            console.error('Failed to load file content:', error);
            this.fileText.set('Error loading file content');
            return false;
        }
    }

    ngOnDestroy() {
        this.stopResize();
    }

    startResize(event: MouseEvent) {
        console.log("resizing")

        event.preventDefault();
        this.isResizing = true;
        document.addEventListener('mousemove', this.handleResize);
        document.addEventListener('mouseup', this.handleResizeEnd);
    }

    private handleResize = (event: MouseEvent) => {
        if (!this.isResizing) return;

        const containerRect = this.previewContainer.nativeElement.getBoundingClientRect();
        this.previewHeight.set(event.clientY - containerRect.top);
    }

    private handleResizeEnd = () => {
        this.isResizing = false;
        this.stopResize();
    }

    private stopResize() {
        document.removeEventListener('mousemove', this.handleResize);
        document.removeEventListener('mouseup', this.handleResizeEnd);
    }
}