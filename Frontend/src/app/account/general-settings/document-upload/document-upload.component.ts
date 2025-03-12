import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { ActionButtonComponent } from "../../../shared/buttons/action-btn/action-btn.component";
import { Component, computed, input, model, output, signal } from "@angular/core";
import { Observable, Subscription } from "rxjs";
import { HttpEvent, HttpEventType } from "@angular/common/http";
import { FormsModule } from "@angular/forms";
import { Operations, OptimisticOperation } from "../../../services/optimistic-operation";
import { MatFormFieldModule } from "@angular/material/form-field";

export type DocumentUploadApi = {
    uploadFile: (file: File) => Observable<HttpEvent<Object>>;
    deleteFile: () => Promise<void>;
}

@Component({
    selector: 'app-document-upload',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatButtonModule,
        MatTooltipModule,
        ActionButtonComponent
    ],
    templateUrl: './document-upload.component.html',
    styleUrl: './document-upload.component.scss'
})
export class DocumentUploadComponent {
    api = input.required<DocumentUploadApi>();
    placeholder = input.required<string>();
    fileName = model<string | null>(null);
    
    uploaded = output<{fileName: string}>();
    deleted = output<OptimisticOperation>();

    isLoading = signal(false);
    
    uploadProgress = signal(0);
    fileSize = signal(0);

    isUploaded = computed(() => !this.isLoading() && this.fileName() != null);
    uploadProgressPercentage = computed(() => Math.round((this.uploadProgress() / this.fileSize()) * 100));

    hasDuplicatedFileNameError = signal(false);
    recentFileName = signal('');

    onFilesSelected(event: any, fileUploadInput: HTMLInputElement) {
        const files: File[] = event.target.files;
        
        if(!files || files.length == 0)
            return;

        const file = files[0];

        this.isLoading.set(true);
        this.fileSize.set(file.size);
        this.uploadProgress.set(0);

        const originalFileName = this.fileName();

        this.fileName.set(file.name);
        this.recentFileName.set(file.name);
        this.hasDuplicatedFileNameError.set(false);

        const fileUpload = this
            .api()
            .uploadFile(file);

        const subscription: Subscription = fileUpload.subscribe({
            next: resp => {
                if (resp.type === HttpEventType.UploadProgress) {
                    this.uploadProgress.set(resp.loaded);
                } else if (resp.type === HttpEventType.Response) {                                                
                    this.uploadProgress.set(file.size);
                }
            },
            error: err => {
                console.error(err);
                subscription.unsubscribe();
                this.isLoading.set(false);
                fileUploadInput.value = "";
                this.fileName.set(originalFileName);

                if(err.error?.code === 'duplicated-file-name') {
                    this.hasDuplicatedFileNameError.set(true);
                }
            },
            complete: () => {
                subscription.unsubscribe();
                this.isLoading.set(false);
                this.uploaded.emit({fileName: file.name});
                fileUploadInput.value = "";
            }
        });
    }

    async onDeleteFile(fileUploadInput: HTMLInputElement) {
        this.isLoading.set(true);
        const operation = Operations.optimistic();
        this.deleted.emit(operation);

        try {
            await this.api().deleteFile();
            operation.succeeded();
            fileUploadInput.value = "";
        } catch (error) {
            console.error(error);
            operation.failed(error);
        } finally {
            this.isLoading.set(false);
        }
    }
}