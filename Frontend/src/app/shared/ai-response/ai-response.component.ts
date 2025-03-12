import { Component, input, OnChanges, SimpleChanges, ViewEncapsulation, computed, signal, WritableSignal, output } from "@angular/core";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatTooltipModule } from "@angular/material/tooltip";
import { RelativeTimeComponent } from "../relative-time/relative-time.component";
import { MarkdownComponent } from "ngx-markdown";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { getExtensionFromLanguage, getMimeType } from "../../services/filte-type";
import { UploadFileAttachmentRequest } from "../../services/folders-and-files.api";
import { getBase62Guid } from "../../services/guid-base-62";
import { AppFileItem } from "../file-item/file-item.component";

export type MessageChunk = PlainMessage | NestedMarkdown;

export type PlainMessage = {
    type: 'plain-message';
    text: string;
}

export type NestedMarkdown = {
    type: 'nested-markdown';
    text: string;
    language: string;
    filename?: string;
    isExpanded: WritableSignal<boolean>;
    isBeingUploaded: WritableSignal<boolean>;
}

export type AiResponseOperations = {
    uploadFileAttachment: (request: UploadFileAttachmentRequest) => Promise<void>
}

export type AttachmentCreatedEvent = {
    externalId: string;
    name: string;
    extension: string;
    sizeInBytes: number;
}

@Component({
    selector: 'app-ai-response',
    imports: [
        MatTooltipModule,
        MatSlideToggleModule,
        MarkdownComponent,
        RelativeTimeComponent,
        ActionButtonComponent
    ],
    templateUrl: './ai-response.component.html',
    styleUrl: './ai-response.component.scss'
})
export class AiResponseComponent implements OnChanges {
    operations = input.required<AiResponseOperations>();
    createdBy = input.required<string>();
    createdAt = input<string>();
    content = input<string>('');
    isProcessing = input(false);

    onAttachmentCreated = output<AttachmentCreatedEvent>();
    
    private _contentChunks = signal<MessageChunk[]>([]);
    contentChunks = computed(() => this._contentChunks());
    
    ngOnChanges(changes: SimpleChanges): void {
        if (changes['content']) {
            this._contentChunks.set(this.parseContentIntoChunks(this.content()));
        }
    }
    
    /**
     * Parses the content string into chunks of plain text and nested markdown
     * @param content The content string to parse
     * @returns An array of MessageChunk objects
     */
    parseContentIntoChunks(content: string): MessageChunk[] {
        if (!content) return [];
        
        const chunks: MessageChunk[] = [];
        // Updated regex to capture language:filename format as well as regular language format
        // Group 1: language or language:filename
        // Group 2: code content
        const codeBlockRegex = /```([\w-]+(?::[\w\.-]+)?)(?:\n|\r\n)([\s\S]*?)```/g;
        
        let lastIndex = 0;
        let match: RegExpExecArray | null;
        
        // Find all code blocks
        while ((match = codeBlockRegex.exec(content)) !== null) {
            const fullMatch = match[0];
            const languageAndFilename = match[1] || 'text';
            const codeContent = match[2];
            
            // Add text before the code block as a plain message (if exists)
            const textBeforeBlock = content.substring(lastIndex, match.index).trim();
            if (textBeforeBlock) {
                chunks.push({
                    type: 'plain-message',
                    text: textBeforeBlock
                });
            }
            
            // Parse language and filename if present (in language:filename format)
            let language = languageAndFilename;
            let filename: string | undefined = undefined;
            
            if (languageAndFilename.includes(':')) {
                const parts = languageAndFilename.split(':');
                language = parts[0];
                filename = parts[1];
            }
            
            // Add the code block as a nested markdown
            chunks.push({
                type: 'nested-markdown',
                text: codeContent,
                language,
                filename, // Add the filename if it exists
                isExpanded: signal(false),
                isBeingUploaded: signal(false)
            });
            
            lastIndex = match.index + fullMatch.length;
        }
        
        // Add any remaining text after the last code block
        const remainingText = content.substring(lastIndex).trim();
        if (remainingText) {
            chunks.push({
                type: 'plain-message',
                text: remainingText
            });
        }
        
        return chunks;
    }

    /**
     * Handles downloading a nested markdown/code block as a file
     * @param chunk The nested markdown chunk to download
     */
    onDownloadNestedMarkdown(chunk: NestedMarkdown) {
        const content = chunk.text;
        
        let filename: string;
        
        if (chunk.filename) {
            filename = chunk.filename;
            
            if (!filename.includes('.')) {
                const extension = getExtensionFromLanguage(chunk.language);
                filename = `${filename}${extension}`;
            }
        } else {
            const extension = getExtensionFromLanguage(chunk.language);
            filename = `code${extension}`;
        }
        
        const extension = filename.includes('.') 
            ? filename.substring(filename.lastIndexOf('.')) 
            : getExtensionFromLanguage(chunk.language);
        
        const mimeType = getMimeType(extension);
        
        const blob = new Blob([content], { type: mimeType });
        
        const url = URL.createObjectURL(blob);
        
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        
        document.body.appendChild(a);
        a.click();
        
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }

    async onUploadFileAttachment(chunk: NestedMarkdown) {
        try {
            chunk.isBeingUploaded.set(true);
            
            const content = chunk.text;
            
            let filename: string;
            let extension: string;
            
            if (chunk.filename) {
                filename = chunk.filename;
                
                if (!filename.includes('.')) {
                    extension = getExtensionFromLanguage(chunk.language);
                    filename = `${filename}${extension}`;
                } else {
                    extension = filename.substring(filename.lastIndexOf('.'));
                }
            } else {
                extension = getExtensionFromLanguage(chunk.language);
                filename = `code${extension}`;
            }
            
            const mimeType = getMimeType(extension);
            
            const blob = new Blob([content], { type: mimeType });
            
            const request: UploadFileAttachmentRequest = {
                externalId: `fi_${getBase62Guid()}`,
                name: filename.replace(/\.[^/.]+$/, ""), 
                extension: extension,
                file: blob
            };
            
            await this.operations().uploadFileAttachment(request);
            
            const attachment: AttachmentCreatedEvent = {
                externalId: request.externalId,
                name: request.name,
                extension: request.extension,
                sizeInBytes: blob.size
            };

            this.onAttachmentCreated.emit(attachment);
        } catch (error) {
            console.error('Failed to upload file attachment:', error);
        } finally {            
            chunk.isBeingUploaded.set(false);
        }
    }
}