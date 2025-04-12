import { Component, ViewChild, ElementRef, OnDestroy, input, signal, computed, OnChanges, SimpleChanges, output, AfterViewChecked } from '@angular/core';
import { MarkdownComponent } from 'ngx-markdown';
import { MarkdownEditorWrapperComponent } from '../../../shared/lexical/markdown-editor-wrapper.component';
import { FileInlinePreviewCommandsPipeline } from '../../file-inline-preview/file-inline-preview-commands-pipeline';
import { Subscription } from 'rxjs';

@Component({
    selector: 'app-markdown-preview',
    imports: [
        MarkdownComponent,
        MarkdownEditorWrapperComponent
    ],
    templateUrl: './markdown-preview.component.html',
    styleUrl: './markdown-preview.component.scss'
})
export class MarkdownPreviewComponent implements OnChanges, OnDestroy {
    fileUrl = input.required<string>();
    isEditMode = input.required<boolean>();

    commandsPipeline = input<FileInlinePreviewCommandsPipeline>();
    initialHeight = input(500);

    @ViewChild('previewContainer') previewContainer!: ElementRef;
    @ViewChild('resizeHandle') resizeHandle!: ElementRef;

    previewHeight = signal<number>(0);

    fileText = signal<string | null>(null);
    currentContent = signal<string>('');

    private _saveContentChangeCommandSubscription: Subscription | null = null;
    private _cancelContentChangeCommandSubscription: Subscription | null = null;

    private isResizing = false;

    constructor() {
        this.previewHeight.set(this.initialHeight());
    }
    
    async ngOnChanges(changes: SimpleChanges): Promise<void> {
        if (changes['isEditMode']) {
            // Force scroll to top after mode switch
            setTimeout(() => {
                if (this.previewContainer) {
                    this.previewContainer.nativeElement.scrollTop = 0;
                }
            }, 20);
        }

        const fileUrl = this.fileUrl();

        if(changes['fileUrl'] && fileUrl) {
            this.fileText.set(null);
            await this.loadText(fileUrl);
        }

        if(changes['commandsPipeline']) {
            this._saveContentChangeCommandSubscription?.unsubscribe();
            this._cancelContentChangeCommandSubscription?.unsubscribe();
            
            const commands = this.commandsPipeline();
                
            if(commands) {
                this._saveContentChangeCommandSubscription = commands
                    .subscribe('save-content-change', async (callback) => {
                        const content = this.currentContent();
                        this.fileText.set(content);
                        await callback(content, 'text/markdown');
                    });
    
                this._cancelContentChangeCommandSubscription = commands
                    .subscribe('cancel-content-change', () => {
                        const originalContent = this.fileText();
                        this.currentContent.set(originalContent ?? '');
                    });
            }
        }
    }

    private async loadText(url: string): Promise<boolean> {
        try {
            const response = await fetch(url, {credentials: 'include'});
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
        this._saveContentChangeCommandSubscription?.unsubscribe();
        this._cancelContentChangeCommandSubscription?.unsubscribe();
    }

    startResize(event: MouseEvent) {        
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

    onContentChange(content: string) {
        this.currentContent.set(content);
    }
}