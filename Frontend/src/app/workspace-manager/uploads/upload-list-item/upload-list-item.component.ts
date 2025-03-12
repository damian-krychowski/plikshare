import { Component, computed, input, OnInit, output, signal, Signal, WritableSignal } from "@angular/core";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { FileIconPipe } from "../../../files-explorer/file-icon-pipe/file-icon.pipe";
import { FileUploadApi, FileUploadManager, IFileUpload } from "../../../services/file-upload-manager/file-upload-manager";
import { ActionButtonComponent } from "../../../shared/buttons/action-btn/action-btn.component";
import { FileSlicer } from "../../../services/file-upload-manager/file-slicer";

export type AppUploadListItem = {
    externalId: Signal<string>;
    fileName: Signal<string>;
    fileExtension: Signal<string>;
    fileContentType: Signal<string>;
    fileSizeInBytes: Signal<number>;

    folderName: Signal<string>;
    folderExternalId: Signal<string>;
    folderPath: Signal<string[]>;

    alreadyUploadedPartNumbers: Signal<number[]>;
};

@Component({
    selector: 'app-upload-list-item',
    imports: [
        MatProgressBarModule,
        FileIconPipe,
        ActionButtonComponent
    ],
    templateUrl: './upload-list-item.component.html',
    styleUrl: './upload-list-item.component.scss'
})
export class UploadListItemComponent implements OnInit {
    upload = input.required<AppUploadListItem>();
    fileUploadApi = input.required<FileUploadApi>();
    aborted = output<void>();

    isLoading = signal(false);
    fileUpload: WritableSignal<IFileUpload | null> = signal(null);

    fileExtension = computed(() => this.upload().fileExtension());
    fileName = computed(() => this.upload().fileName());
    fileNameWithExtensions = computed(() => this.fileName() + this.fileExtension());
    filePath = computed(() => this.buildFilePath(this.upload()));
    fileSizeInBytes = computed(() => this.upload().fileSizeInBytes());
    progressPercentage = computed(() => this.calculateUploadProgressPercentage());

    isPaused = computed(() => this.fileUpload()?.isPaused() ?? true);
    hasFileUpload = computed(() => this.fileUpload() != null);

    constructor(
        private _fileUploadManager: FileUploadManager) {
    }

    ngOnInit(): void {
        const upload = this.upload();

        const fileUpload = this
            ._fileUploadManager
            .getFileUploadRef(upload.externalId());

        if(fileUpload) {
            this.fileUpload.set(fileUpload);
        }
    }

    private buildFilePath(upload: AppUploadListItem): any {
        if (!upload.folderName) {
            return 'All Files';
        }

        return upload
            .folderPath()
            .concat([upload.folderName()])
            .join(' / ');
    }

    private calculateUploadProgressPercentage() {        
        const fileUpload = this.fileUpload();

        if(fileUpload) {
            return fileUpload.uploadProgressPercentage();
        }

        const upload = this.upload();
        const partSizeInBytes = 10485600; //todo: that is hardcoded for now for simplicity

        const fileSizeInBytes = upload.fileSizeInBytes();
        const uploadedParts = upload.alreadyUploadedPartNumbers().length;
        
        const totalParts = Math.ceil(fileSizeInBytes / partSizeInBytes);

        return Math.round(uploadedParts / totalParts * 100);        
    }

    public async abortUpload() {
        try {
            this.isLoading.set(true);

            this.aborted.emit();

            const upload = this.upload();

            await this._fileUploadManager.abortUpload(
                upload.externalId());
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    public pickFileAndResumeUpload(event: any) {
        const file: File = event.target.files[0];

        if(file) {
            if(file.name !== this.fileNameWithExtensions()) {
                alert('File name does not match.');
                return;
            }

            if(file.size !== this.fileSizeInBytes()) {
                alert('File size does not match.');
                return;
            }

            const upload = this.upload();

            this._fileUploadManager.resumeUpload({
                contentType: file.type,
                fileSlicer: new FileSlicer(file),
                uploadExternalId: upload.externalId(),
                uploadsApi: this.fileUploadApi(),
                fileSizeInBytes: upload.fileSizeInBytes()
            }, {
                uploadResumed: (args: { fileUpload: IFileUpload; }) => {
                    this.fileUpload.set(args.fileUpload);
                }
            });  
        }
    }

    public resumeFileUpload() {
        this.fileUpload()?.resume();
    }

    public pauseFileUpload() {
        this.fileUpload()?.pause();
    }

    public resume(fileUpload: HTMLInputElement) {
        fileUpload.click();
    }
}