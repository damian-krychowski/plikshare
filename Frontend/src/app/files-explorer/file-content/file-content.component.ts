import { Component, computed, input, OnChanges, output, Signal, signal, SimpleChanges } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { AppFileItem } from "../../shared/file-item/file-item.component";
import { FileIconPipe } from "../file-icon-pipe/file-icon.pipe";
import { TextPreviewComponent } from "./text-preview/text-preview.component";
import { ContentDisposition, GetFileDownloadLinkResponse } from "../../services/folders-and-files.api";
import { getFileDetails } from "../../services/filte-type";
import { ZipPreviewDetails } from "../file-inline-preview/file-inline-preview.component";
import { ZipEntry } from "../../services/zip";
import { ImagePreviewComponent } from "./image-preview/image-preview.component";
import { VideoPreviewComponent } from "./video-preview/video-preview.component";
import { AudioPreviewComponent } from "./audio-preview/audio-preview.component";
import { PdfPreviewComponent } from "./pdf-preview/pdf-preview.component";
import { MarkdownPreviewComponent } from "./markdown-preview/markdown-preview.component";
import { ZipPreviewComponent, ZipPreviewOperations } from "./zip-preview/zip-preview.component";
import { FileInlinePreviewCommandsPipeline } from "../file-inline-preview/file-inline-preview-commands-pipeline";

export type FileToPreview = {
    name: Signal<string>;
    extension: string;
    sizeInBytes: number;
}

export type FileContentOperations = {    
    getDownloadLink: (contentDisposition: ContentDisposition) => Promise<GetFileDownloadLinkResponse>;
    getZipPreviewDetails: () => Promise<ZipPreviewDetails>;
    getZipContentDownloadLink: (zipEntry: ZipEntry, contentDisposition: ContentDisposition) => Promise<GetFileDownloadLinkResponse>;
    prepareAdditionalHttpHeaders: () => Record<string, string> | undefined;
}

const MB_5_FILE_SIZE = 5 * 1024 * 1024;

export type AppFileForContent = {
    name: Signal<string>;
    extension: string;
    sizeInBytes: number;
}

@Component({
    selector: 'app-file-content',
    imports: [
        FormsModule,
        MatButtonModule,
        MatSlideToggleModule,
        TextPreviewComponent,
        FileIconPipe,
        MatCheckboxModule,
        MatProgressBarModule,
        ImagePreviewComponent,
        VideoPreviewComponent,
        AudioPreviewComponent,
        PdfPreviewComponent,
        MarkdownPreviewComponent,
        ZipPreviewComponent
    ],
    templateUrl: './file-content.component.html',
    styleUrls: ['./file-content.component.scss']
})
export class FileContentComponent implements OnChanges {
    file = input.required<AppFileForContent>();    
    operations = input.required<FileContentOperations>();
    commandsPipeline = input<FileInlinePreviewCommandsPipeline>();
    isEditMode = input(false);
    
    fileFullName = computed(() => this.file().name() + this.file().extension);
    fileExtension = computed(() => this.file().extension);
    fileType = computed(() => getFileDetails(this.fileExtension()).type);
        
    fileUrl = signal<string | null>(null);
    
    canOpenAsText = computed(() => this.file().sizeInBytes <= MB_5_FILE_SIZE);

    //text
    forceFileTextDisplay = signal(false);

    //zip
    zipPreviewOperations = signal<ZipPreviewOperations | null>(null);
    zipEntryClicked = output<ZipEntry>();

    async ngOnChanges(changes: SimpleChanges): Promise<void> {
        if(changes['file'] && this.file()) {
            this.resetState();
            await this.loadFileContent();
        }
    }  

    private resetState() {
        this.fileUrl.set(null);
        this.forceFileTextDisplay.set(false);
        this.zipPreviewOperations.set(null);
    }

    private async loadFileContent() {
        const fileType = this.fileType();
        
        if(fileType == 'archive') {
            this.prepareZipPreviewOperations();
        } else {
            const result = await this
                .operations()
                .getDownloadLink("inline");
    
            this.fileUrl.set(result.downloadPreSignedUrl);
        }
    }

    private prepareZipPreviewOperations() {
        this.zipPreviewOperations.set({
            getZipContentDownloadLink: (zipEntry: ZipEntry, contentDisposition: ContentDisposition) => {
                return this.operations().getZipContentDownloadLink(
                    zipEntry,
                    contentDisposition);
            },

            getZipPreviewDetails: () => {
                return this.operations().getZipPreviewDetails();
            }
        });
    }

    async openAsText() {
        this.forceFileTextDisplay.set(true);
    }
}