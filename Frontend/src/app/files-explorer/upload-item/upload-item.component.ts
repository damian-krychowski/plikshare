import { Component, computed, input, output, Signal, signal, WritableSignal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { FileIconPipe } from '../file-icon-pipe/file-icon.pipe';
import { FileUploadApi, FileUploadManager, IFileUpload } from '../../services/file-upload-manager/file-upload-manager';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { CtrlClickDirective } from '../../shared/ctrl-click.directive';
import { ConfirmOperationDirective } from '../../shared/operation-confirm/confirm-operation.directive';
import { toggle } from '../../shared/signal-utils';
import { ActionButtonComponent } from "../../shared/buttons/action-btn/action-btn.component";
import { FileSlicer } from '../../services/file-upload-manager/file-slicer';
import { HttpHeadersFactory } from '../http-headers-factory';

export type AppUploadItem = {
    type: 'upload';

    externalId: string;
    fileName: WritableSignal<string>;
    fileExtension: string;
    fileContentType: string;
    fileSizeInBytes: number;
    folderExternalId: string | null;
    alreadyUploadedPartNumbers: number[];

    fileUpload: WritableSignal<IFileUpload | undefined>;

    isSelected: WritableSignal<boolean>;
    isCut: WritableSignal<boolean>;

};

export type AppUploadItemFolder = {
    name: Signal<string>;
    externalId: Signal<string>;
    path: Signal<string[]>;
}

@Component({
    selector: 'app-upload-item',
    imports: [
        FormsModule,
        MatCheckboxModule,
        MatProgressBarModule,
        FileIconPipe,
        CtrlClickDirective,
        ConfirmOperationDirective,
        ActionButtonComponent
    ],
    templateUrl: './upload-item.component.html',
    styleUrl: './upload-item.component.scss'
})
export class UploadItemComponent {
    fileUploadApi = input.required<FileUploadApi | null>();
    httpHeadersFactory = input.required<HttpHeadersFactory>();
    upload = input.required<AppUploadItem>();
    hideActions = input(false);

    aborted = output<void>();
    isSelectedChange = output<boolean>();

    fileNameWithExtension = computed(() => {
        const upload = this.upload();
        return upload.fileName() + upload.fileExtension;
    });

    progressPercentage = computed(() => this.calculateUploadProgressPercentage());

    public isLoading = signal(false);
    public areActionsVisible = signal(false);

    constructor(
        public fileUploadManager: FileUploadManager
    ) {
    }    

    private calculateUploadProgressPercentage() {        
        const upload = this.upload();
        const fileUpload = upload.fileUpload();

        if(fileUpload) {
            return fileUpload.uploadProgressPercentage();
        }

        const partSizeInBytes = 10485600; //todo: that is hardcoded for now for simplicity

        const uploadedParts = upload.alreadyUploadedPartNumbers.length;
        
        const totalParts = Math.ceil(upload.fileSizeInBytes / partSizeInBytes);

        return Math.round(uploadedParts / totalParts * 100);        
    }

    public pickFileAndResumeUpload(event: any) {
        const fileUploadApi = this.fileUploadApi();

        if(!fileUploadApi)
            return;

        const upload = this.upload();

        const file: File = event.target.files[0];

        if(!file) 
            return;
        
        if(file.name !== this.fileNameWithExtension()) {
            alert('File name does not match.');
            return;
        }

        if(file.size !== upload.fileSizeInBytes) {
            alert('File size does not match.');
            return;
        }

        this.fileUploadManager.resumeUpload({
            contentType: file.type,
            fileSlicer: new FileSlicer(file),
            uploadExternalId: upload.externalId,
            uploadsApi: fileUploadApi,
            fileSizeInBytes: file.size,
            httpHeadersFactory: this.httpHeadersFactory()
        }, {
            uploadResumed: (args: { fileUpload: IFileUpload; }) => {
                upload.fileUpload.set(args.fileUpload);
            }
        });        
        
        this.areActionsVisible.set(false);
    }

    public async abortUpload() {
        const fileUploadApi = this.fileUploadApi();

        if(!fileUploadApi)
            return;
        
        const upload = this.upload();

        try {
            this.isLoading.set(true);

            this.aborted.emit();

            await this.fileUploadManager.abortUpload(
                upload.externalId);

            await fileUploadApi.abort(
                upload.externalId);

        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    public uploadFile(fileUpload: HTMLInputElement) {
        fileUpload.click();
    }

    pauseUpload() {
        const upload = this.upload();

        upload.fileUpload()?.pause();
        this.areActionsVisible.set(false);
    }

    public resumeUpload() {
        const upload = this.upload();
        upload.fileUpload()?.resume();
        this.areActionsVisible.set(false);
    }

    toggleActions() {
        this.areActionsVisible.set(!this.areActionsVisible());
    }

    toggleSelection() {
        const upload = this.upload();
        const isSelected = toggle(upload.isSelected);
        this.isSelectedChange.emit(isSelected);
        this.areActionsVisible.set(false);
    }
}
