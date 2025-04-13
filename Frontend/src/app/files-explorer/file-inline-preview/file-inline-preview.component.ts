import { Component, computed, input, OnChanges, OnDestroy, OnInit, output, Signal, signal, SimpleChanges, WritableSignal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { AppFileItem, AppFileItems, FileItemComponent, FileOperations } from '../../shared/file-item/file-item.component';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { RichTextEditorComponent } from '../../shared/rich-text-editor/rich-text-editor.component';
import { AppComment, CommentComponent } from '../../shared/comment/comment.component';
import { getRelativeTime } from '../../services/time.service';
import { AuthService } from '../../services/auth.service';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { ZipArchives, ZipEntry } from '../../services/zip';
import { AiInclude, AiMessageDto, ContentDisposition, FilePreviewDetailsField, GetAiMessagesResponse, GetFileDownloadLinkResponse, GetFilePreviewDetailsResponse, SendAiFileMessageRequest, StartTextractJobRequest, StartTextractJobResponse, TextractFeature, TextractJobStatus, UpdateAiConversationNameRequest, UploadFileAttachmentRequest } from '../../services/folders-and-files.api';
import { TextractIntegration } from '../../services/integrations.types';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FormsModule } from '@angular/forms';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TextractJobStatusService } from '../../services/textract-job-status.service';
import { AppTextractJobItem, AppTextractJobItemExtensions } from '../../shared/app-textract-job-item';
import { FileContentComponent, FileContentOperations, FileToPreview } from "../file-content/file-content.component";
import { containsItem, insertItem, pushItems, removeItem, unshiftItems } from '../../shared/signal-utils';
import { getFileDetails } from '../../services/filte-type';
import { FileInlinePreviewCommandsPipeline } from './file-inline-preview-commands-pipeline';
import { ChatGptIntegration, WorkspaceIntegrations } from '../../services/workspaces.api';
import { getBase62Guid } from '../../services/guid-base-62';
import { AiMessageComponent, AiMessageSentEvent } from '../../shared/ai-message/ai-message.component';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { AiConversationItemComponent, AiConversationOperations, AiConversationStatus, AppAiConversation } from '../../shared/ai-conversation-item/ai-conversation-item.component';
import { OptimisticOperation } from '../../services/optimistic-operation';
import { DataStore } from '../../services/data-store.service';
import { AiConversationsStatusService } from '../../services/ai-conversations-status.service';
import { Subscription } from 'rxjs';
import { AiResponseComponent, AiResponseOperations, AttachmentCreatedEvent } from '../../shared/ai-response/ai-response.component';
import { StorageSizePipe, StorageSizeUtils } from '../../shared/storage-size.pipe';

export type ImageDimensions = {
    width: number;
    height: number;
}

export type ImageExif = Record<string, any>;

export type ZipPreviewDetails = {
    items: ZipEntry[];
}

export type FilePreviewOperations = {
    getZipPreviewDetails: (fileExternalId: string) => Promise<ZipPreviewDetails>;
    getZipContentDownloadLink: (fileExternalId: string, zipEntry: ZipEntry, contentDisposition: ContentDisposition) => Promise<GetFileDownloadLinkResponse>;

    getFilePreviewDetails: (fileExternalId: string, fields: FilePreviewDetailsField[] | null) => Promise<GetFilePreviewDetailsResponse>;

    updateFileNote: (fileExternalId: string, noteContentJson: string) => Promise<void>;

    createFileComment: (fileExternalId: string, comment: { externalId: string, contentJson: string }) => Promise<void>;
    delefeFileComment: (fileExternalId: string, commentExternalId: string) => Promise<void>;
    updateFileComment: (fileExternalId: string, comment: { externalId: string, updatedContentJson: string }) => Promise<void>;

    startTextractJob: (request: StartTextractJobRequest) => Promise<StartTextractJobResponse>;

    updateFileContent: (fileExternalId: string, file: Blob) => Promise<void>;
    uploadFileAttachment: (fileExternalId: string, request: UploadFileAttachmentRequest) => Promise<void>;

    sendAiFileMessage(fileExternalId: string, request: SendAiFileMessageRequest): Promise<void>;
    updateAiConversationName(fileExternalId: string, fileArtifactExternalId: string, request: UpdateAiConversationNameRequest): Promise<void>;
    deleteAiConversation(fileExternalId: string, fileArtifactExternalId: string): Promise<void>;

    getAiMessages(fileExternalId: string, fileArtifactExternalId: string, fromConversationCounter: number): Promise<GetAiMessagesResponse>;
    getAllAiMessages(fileExternalId: string, fileArtifactExternalId: string): Promise<GetAiMessagesResponse>;
    prefetchAiMessages(fileExternalId: string, fileArtifactExternalId: string): void;
    
    prepareAdditionalHttpHeaders: () => Record<string, string> | undefined;
}


export type AppFileForPreview = {
    externalId: string;
    name: Signal<string>;
    extension: string;
    sizeInBytes: number;
}

type OtherAiContentType = 'notes' | 'comments';
type FileAiContentType = `file:${string}`; // string will be the file.externalId
type AiContentType = OtherAiContentType | FileAiContentType;

@Component({
    selector: 'app-file-inline-preview',
    imports: [
        FormsModule,
        MatSelectModule,
        MatFormFieldModule,
        MatButtonModule,
        MatSlideToggleModule,
        RichTextEditorComponent,
        CommentComponent,
        ActionButtonComponent,
        MatCheckboxModule,
        MatProgressBarModule,
        FileItemComponent,
        FileContentComponent,
        AiMessageComponent,
        AiConversationItemComponent,
        AiResponseComponent,
        StorageSizePipe
    ],
    templateUrl: './file-inline-preview.component.html',
    styleUrls: ['./file-inline-preview.component.scss']
})
export class FileInlinePreviewComponent implements OnChanges, OnDestroy {
    file = input.required<AppFileForPreview>();
    operations = input.required<FilePreviewOperations>();
    fileOperations = input.required<FileOperations>();
    textractJobsStatusService = input.required<TextractJobStatusService>();
    commandsPipeline = input.required<FileInlinePreviewCommandsPipeline>();

    allowFileEdit = input.required<boolean>();
    allowPreviewNotes = input.required<boolean>();
    allowPreviewComments = input.required<boolean>();
    integrations = input.required<WorkspaceIntegrations>();
    isInEditMode = input.required<boolean>();

    closed = output();

    fileExternalId = computed(() => this.file().externalId);

    fileContentOperations = computed(() => this.getFileContentOperations(
        this.file().externalId));

    aiConversationOperations = computed(() => this.getAiConversationOperations(
        this.file().externalId));

    dependentFileInPreview = signal<AppFileItem | null>(null);

    dependentFileContentOperations = computed(() => {
        const dependentFile = this.dependentFileInPreview();

        if (!dependentFile)
            return null;

        return this.getFileContentOperations(dependentFile.externalId);
    });

    dependentFileCanEdit = computed(() => {
        const dependentFile = this.dependentFileInPreview();

        if (!dependentFile)
            return false;

        return AppFileItems.canEdit(dependentFile, this.allowFileEdit());
    });

    dependentFileCommandsPipeline = new FileInlinePreviewCommandsPipeline();
    dependentFileIsEditMode = signal(false);
    isDependentFileBeingSaved = signal(false);

    zipEntryInPreview = signal<ZipEntry | null>(null);
    zipEntryFileInPreview = computed(() => {
        const zipEntry = this.zipEntryInPreview();

        if (!zipEntry)
            return null;

        const nameAndExt = ZipArchives.getFileNameAndExtension(
            zipEntry);

        const fileToPreview: FileToPreview = {
            name: signal(nameAndExt.name),
            extension: nameAndExt.extension,
            sizeInBytes: zipEntry.compressedSizeInBytes
        };

        return fileToPreview;
    });
    zipEntryFileContentOperations = computed(() => {
        const zipEntry = this.zipEntryInPreview();

        if (!zipEntry)
            return null;

        return this.getZipEntryFileContentOperations(zipEntry);
    });

    //ai integration
    aiResponseOperations = computed(() => {
        const operations = this.operations();
        const fileExternalId = this.fileExternalId();

        const aiOperations: AiResponseOperations = {
            uploadFileAttachment: (request: UploadFileAttachmentRequest) => operations.uploadFileAttachment(fileExternalId, request)
        };

        return aiOperations;
    });

    isAiAvailable = computed(() => this.integrations().chatGpt.length > 0);

    chatGptIntegrations = computed(() => this.integrations().chatGpt);
    aiConversations = signal<AppAiConversation[]>([]);

    activeAiConversation = signal<AppAiConversation | null>(null);

    activeAiConversationMessages = signal<AiMessageDto[]>([]);
    lastAiConversationMessage = computed(() => this.activeAiConversationMessages().at(-1));

    selectedChatGptIntegration = signal<ChatGptIntegration | null>(null);
    selectedChatGptModelAlias = signal<string | null>(null);
    selectedChatGptModel = computed(() => {
        const integration = this.selectedChatGptIntegration();

        if (!integration)
            return null;

        const selectedModelAlias = this.selectedChatGptModelAlias();

        if (!selectedModelAlias)
            return null;

        const model = integration.models.find(x => x.alias == selectedModelAlias);

        return model;
    });


    selectedAiContentTypes = signal<AiContentType[]>([]);
    selectedAiIncludes = computed(() => {
        const result: AiInclude[] = [];

        for (const contentType of this.selectedAiContentTypes()) {
            if (contentType === 'comments') {
                result.push({
                    $type: 'comments',
                    externalId: this.fileExternalId()
                });
            } else if (contentType === 'notes') {
                result.push({
                    $type: 'notes',
                    externalId: this.fileExternalId()
                });
            } else {
                const externalId = contentType.slice(5); // Remove 'file:' prefix

                result.push({
                    $type: 'file',
                    externalId
                });
            }
        }

        return result;
    });

    isAiMessageBeingProcessed = signal(false);

    aiConversationStatusSubscriptions: Subscription[] = [];


    filteredSelectableContentTypes = computed(() => {
        const model = this.selectedChatGptModel();
        const fileVal = this.file();
        const attachments = this.allAttachments();

        if (!model) {
            // If no model is selected, return all files
            return [
                `file:${fileVal.externalId}`,
                ...attachments.map(file => `file:${file.externalId}`)
            ];
        }

        const result: AiContentType[] = [];

        // Check main file
        const fileType = getFileDetails(fileVal.extension).type;
        if (
            model.supportedFileTypes.includes(fileType) &&
            fileVal.sizeInBytes <= model.maxIncludeSizeInBytes
        ) {
            result.push(`file:${fileVal.externalId}`);
        }

        // Check textract result files
        for (const file of attachments) {
            const fileType = getFileDetails(file.extension).type;
            if (
                model.supportedFileTypes.includes(fileType) &&
                file.sizeInBytes <= model.maxIncludeSizeInBytes
            ) {
                result.push(`file:${file.externalId}`);
            }
        }

        // Notes and comments are always allowed (if they exist in the original inclusion list)
        if (this.selectedAiContentTypes().includes('notes')) {
            result.push('notes');
        }

        if (this.selectedAiContentTypes().includes('comments')) {
            result.push('comments');
        }

        return result;
    });



    //textract integration
    isTextractAvailable = computed(() => this.integrations().textract
        && TextractIntegration.isSupportedForExtension(this.file().extension));

    isTextractModeActive = signal(false);

    textractLayoutScan = signal(false);
    isTextractLayoutScanAvailable = computed(() =>
        this.textractJobs().filter(j => j.features.includes('layout')).length == 0);

    textractTablesScan = signal(false);
    isTextractTablesScanAvailable = computed(() =>
        this.textractJobs().filter(j => j.features.includes('tables')).length == 0);

    textractFormsScan = signal(false);
    isTextractFormsScanAvailable = computed(() =>
        this.textractJobs().filter(j => j.features.includes('forms')).length == 0);

    isTextractExplanationVisible = signal(false);

    canStartTextractJob = computed(() =>
        (this.textractLayoutScan() && this.isTextractLayoutScanAvailable())
        || (this.textractTablesScan() && this.isTextractTablesScanAvailable())
        || (this.textractFormsScan() && this.isTextractFormsScanAvailable()));

    textractJobs = signal<AppTextractJobItem[]>([]);

    textractResultFiles = signal<AppFileItem[]>([]);
    attachments = signal<AppFileItem[]>([]);
    allAttachments = computed(() => [...this.textractResultFiles(), ...this.attachments()]);

    runningTextractJobs = computed(() => {
        const jobs = this.textractJobs();

        return jobs.filter(job => {
            const status = job.status();

            return status != 'completed' && status != 'partially-completed';
        });
    })

    //note
    noteJson = signal<string | undefined>(undefined);
    noteChangedBy = signal<string | null>(null);
    noteChangedAt = signal<string | null>(null);
    noteChangedWhen = computed(() => {
        const changedAt = this.noteChangedAt();

        if (!changedAt) return null;
        return getRelativeTime(changedAt);
    });

    fileNoteUpdateOperation = computed<(json: string, html: string) => Promise<void>>(() => {
        const operations = this.operations();

        return (json: string, html: string) => {
            this.noteChangedBy.set(this._auth.userEmail());
            this.noteChangedAt.set(new Date().toISOString());

            return operations.updateFileNote(
                this.fileExternalId(),
                json
            );
        };
    });

    //comments
    comments = signal<AppComment[]>([]);

    constructor(
        private _auth: AuthService,
        private _aiConversationsStatusService: AiConversationsStatusService,
        public dataStore: DataStore) {
    }

    ngOnDestroy(): void {
        for (const subscription of this.aiConversationStatusSubscriptions) {
            subscription.unsubscribe();
        }
    }

    private getFileContentOperations(fileExternalId: string) {
        const fileOperations = this.fileOperations();
        const previewOperations = this.operations();

        const contentOperations: FileContentOperations = {
            getDownloadLink: (contentDisposition: ContentDisposition) => fileOperations.getDownloadLink(
                fileExternalId,
                contentDisposition),

            getZipContentDownloadLink: (zipEntry: ZipEntry, contentDisposition: ContentDisposition) => previewOperations.getZipContentDownloadLink(
                fileExternalId,
                zipEntry,
                contentDisposition),

            getZipPreviewDetails: () => previewOperations.getZipPreviewDetails(
                fileExternalId),

            prepareAdditionalHttpHeaders: () => previewOperations.prepareAdditionalHttpHeaders()
        };

        return contentOperations;
    }

    private getAiConversationOperations(fileExternalId: string): AiConversationOperations {
        const operations = this.operations();

        const aiConversationOperations: AiConversationOperations = {
            deleteAiConversation: (fileArtifactExternalId: string) => operations
                .deleteAiConversation(fileExternalId, fileArtifactExternalId),

            updateAiConversationName: (fileArtifactExternalId: string, request: UpdateAiConversationNameRequest) => operations
                .updateAiConversationName(fileExternalId, fileArtifactExternalId, request)
        };

        return aiConversationOperations;
    }

    private getZipEntryFileContentOperations(zipEntry: ZipEntry) {
        const previewOperations = this.operations();

        const contentOperations: FileContentOperations = {
            getDownloadLink: (contentDisposition: ContentDisposition) => previewOperations.getZipContentDownloadLink(
                this.fileExternalId(),
                zipEntry,
                contentDisposition),

            getZipContentDownloadLink: (zipEntry: ZipEntry, contentDisposition: ContentDisposition) => {
                throw new Error("not implemented")
            },

            getZipPreviewDetails: () => {
                throw new Error("not implemented")
            },

            prepareAdditionalHttpHeaders: () => previewOperations.prepareAdditionalHttpHeaders()
        };

        return contentOperations;
    }

    async ngOnChanges(changes: SimpleChanges): Promise<void> {
        if (changes['file'] && this.file()) {
            this.resetState();
            await this.loadFilePreviewDetails()
        }
    }

    private resetState() {
        this.dependentFileInPreview.set(null);
        // this.zipEntryInPreview.set(null);
        this.textractLayoutScan.set(false);
        this.textractFormsScan.set(false);
        this.textractTablesScan.set(false);
        this.isTextractExplanationVisible.set(false);
        this.textractJobs.set([]);
        this.textractResultFiles.set([]);
        this.attachments.set([]);
        this.noteJson.set(undefined);
        this.noteChangedAt.set(null);
        this.noteChangedBy.set(null);
        this.comments.set([]);
    }

    private async loadFilePreviewDetails() {
        if (!this.allowPreviewComments() && !this.allowPreviewNotes())
            return;

        const result = await this.operations().getFilePreviewDetails(
            this.fileExternalId(),
            null);

        if (result.note && this.allowPreviewNotes()) {
            this.noteJson.set(result.note.contentJson);
            this.noteChangedBy.set(result.note.changedBy);
            this.noteChangedAt.set(result.note.changedAt);
        }

        if (result.comments && this.allowPreviewComments()) {
            this.comments.set(result
                .comments
                .map(c => {
                    const comment: AppComment = {
                        externalId: c.externalId,
                        json: signal(c.contentJson),
                        createdAt: c.createdAt,
                        createdBy: c.createdBy,
                        wasEdited: signal(c.wasEdited)
                    };

                    return comment;
                }));
        }

        if (result.pendingTextractJobs && result.pendingTextractJobs.length) {
            for (const pendingTextractJob of result.pendingTextractJobs) {
                this.addPendingTextractJob(pendingTextractJob);
            }
        }

        if (result.aiConversations && result.aiConversations.length) {
            const conversations = result
                .aiConversations
                .map(aic => {
                    const conversation: AppAiConversation = {
                        fileArtifactExternalId: aic.fileArtifactExternalId,
                        aiConversationExternalId: aic.aiConversationExternalId,
                        aiIntegrationExternalId: aic.aiIntegrationExternalId,
                        createdAt: aic.createdAt,
                        createdBy: aic.createdBy,
                        isNameEditing: signal(false),
                        conversationCounter: signal(aic.conversationCounter),
                        name: signal(aic.name),
                        status: signal<AiConversationStatus>(aic.isWaitingForAiResponse
                            ? 'waits-for-ai-response'
                            : 'all-messages-read')
                    };

                    return conversation;
                });

            this.aiConversations.set(conversations);
            this.subscribeConversationsForStatusCheck(...conversations);
        }

        this.setTextractResultFiles(result);
        this.setAttachmentFiles(result);
    }

    private setTextractResultFiles(result: GetFilePreviewDetailsResponse) {
        if (!result || !result.textractResultFiles || !result.textractResultFiles.length)
            return;

        const files: AppFileItem[] = result
            .textractResultFiles
            .map(textractResultFile => {
                const file: AppFileItem = {
                    type: 'file',

                    externalId: textractResultFile.externalId,
                    name: signal(textractResultFile.name),
                    extension: textractResultFile.extension,
                    sizeInBytes: textractResultFile.sizeInBytes,
                    wasUploadedByUser: textractResultFile.wasUploadedByUser,
                    folderExternalId: null,

                    folderPath: null,
                    isCut: signal(false),
                    isHighlighted: signal(false),
                    isLocked: signal(false),
                    isNameEditing: signal(false),
                    isSelected: signal(false),
                };

                return file;
            });

        this.textractResultFiles.set(files);
    }

    private setAttachmentFiles(result: GetFilePreviewDetailsResponse) {
        if (!result || !result.attachments || !result.attachments.length)
            return;

        const files: AppFileItem[] = result
            .attachments
            .map(attachment => {
                const file: AppFileItem = {
                    type: 'file',

                    externalId: attachment.externalId,
                    name: signal(attachment.name),
                    extension: attachment.extension,
                    sizeInBytes: attachment.sizeInBytes,
                    wasUploadedByUser: attachment.wasUploadedByUser,
                    folderExternalId: null,

                    folderPath: null,
                    isCut: signal(false),
                    isHighlighted: signal(false),
                    isLocked: signal(false),
                    isNameEditing: signal(false),
                    isSelected: signal(false),
                };

                return file;
            });

        this.attachments.set(files);
    }

    public onCancel() {
        this.closed.emit();
    }

    async commentSent(comment: AppComment) {
        this.comments.update(current => [...current, comment]);

        await this.operations().createFileComment(
            this.fileExternalId(), {
            externalId: comment.externalId,
            contentJson: comment.json()
        }
        );
    }

    async commentEdited(comment: AppComment) {
        await this.operations().updateFileComment(
            this.fileExternalId(), {
            externalId: comment.externalId,
            updatedContentJson: comment.json()
        }
        );
    }

    async commentDeleted(comment: AppComment) {
        this.comments.update(current => current.filter(c => c.externalId != comment.externalId));

        await this.operations().delefeFileComment(
            this.fileExternalId(),
            comment.externalId
        );
    }

    async startTextractJob() {
        const features: TextractFeature[] = [];

        if (this.textractFormsScan()) {
            features.push('forms');
        }

        if (this.textractLayoutScan()) {
            features.push("layout");
        }

        if (this.textractTablesScan()) {
            features.push("tables");
        }

        this.textractFormsScan.set(false);
        this.textractLayoutScan.set(false);
        this.textractTablesScan.set(false);

        const response = await this.operations().startTextractJob({
            features: features,
            fileExternalId: this.fileExternalId()
        });

        this.addPendingTextractJob({
            externalId: response.externalId,
            features: features,
            status: null
        });
    }

    private addPendingTextractJob(args: {
        externalId: string,
        status: TextractJobStatus | null,
        features: TextractFeature[]
    }) {
        const statusSignal = signal(args.status);

        const textractJob: AppTextractJobItem = {
            type: 'textract-job',
            externalId: args.externalId,
            status: statusSignal,
            features: args.features,
            progressPercentage: AppTextractJobItemExtensions.getProgressPercentageSignal(
                statusSignal),

            onCompletedHandler: () => this.reloadTextractResultFiles()
        };

        this.textractJobs.update((jobs) => [...jobs, textractJob])

        this.textractJobsStatusService()
            .subscribeToStatusCheck(textractJob);
    }

    onTextractResultPreview(file: AppFileItem) {
        // this.zipEntryInPreview.set(null);
        this.dependentFileInPreview.set(file);
    }

    onAttachmentDeleted(file: AppFileItem) {
        const fileInPreview = this.dependentFileInPreview();

        if (fileInPreview === file) {
            this.dependentFileInPreview.set(null);
        }

        if(containsItem(this.textractResultFiles, file)) {
            removeItem(this.textractResultFiles, file);
        }

        if(containsItem(this.attachments, file)){
            removeItem(this.attachments, file);
        }
    }

    private async reloadTextractResultFiles() {
        const result = await this.operations().getFilePreviewDetails(
            this.fileExternalId(),
            ['textract-result-files']);

        this.setTextractResultFiles(result);
    }

    onZipEntryClicked(zipEntry: ZipEntry) {
        const nameAndExt = ZipArchives.getFileNameAndExtension(
            zipEntry);

        const details = getFileDetails(
            nameAndExt.extension);

        if (details.type == 'archive')
            return;

        this.dependentFileInPreview.set(null);
        this.zipEntryInPreview.set(zipEntry);
    }

    dependentFileCancelContentChanges() {
        this.dependentFileIsEditMode.set(false);
        this.dependentFileCommandsPipeline.emit({
            type: 'cancel-content-change'
        });
    }

    fileInPreviewSaveContentChanges() {
        const dependentFile = this.dependentFileInPreview();

        if (!dependentFile)
            return;

        this.dependentFileCommandsPipeline.emit({
            type: 'save-content-change',
            callback: async (content: string, contentType: string) => {
                try {
                    this.isDependentFileBeingSaved.set(true);

                    const api = this.operations();
                    const file: Blob = new Blob([content], { type: contentType });

                    await api.updateFileContent(
                        dependentFile.externalId,
                        file);

                    this.dependentFileIsEditMode.set(false);
                } finally {
                    this.isDependentFileBeingSaved.set(false);
                }
            }
        });
    }

    startAiConversation() {
        const integration = this.chatGptIntegrations()[0];

        this.resetAiActiveConversation(
            integration,
            []);

        const conversation: AppAiConversation = {
            fileArtifactExternalId: `fa_${getBase62Guid()}`,
            aiConversationExternalId: `aic_${getBase62Guid()}`,
            aiIntegrationExternalId: integration.externalId,
            conversationCounter: signal(-1),
            createdAt: new Date().toISOString(),
            createdBy: this._auth.userEmail(),
            isNameEditing: signal(false),
            name: signal('Untitled conversation'),
            status: signal('all-messages-read')
        };

        this.activeAiConversation.set(conversation);
    }

    private resetAiActiveConversation(integration: ChatGptIntegration, messages: AiMessageDto[]) {
        this.selectedChatGptIntegration.set(integration);
        this.selectedChatGptModelAlias.set(integration.defaultModel)
        this.selectedAiContentTypes.set([`file:${this.fileExternalId()}`]);
        this.updateSelectedContentTypesBasedOnModel();
        this.activeAiConversationMessages.set(messages);
    }

    stopAiConversation() {
        this.activeAiConversation.set(null);
    }

    async aiMessageSent(messageEvent: AiMessageSentEvent) {
        const conversation = this.activeAiConversation();

        if (!conversation)
            return;

        const selectedIntegration = this.selectedChatGptIntegration();
        const selectedModel = this.selectedChatGptModelAlias();
        const includes = this.selectedAiIncludes();

        if (!selectedIntegration || !selectedModel)
            return;

        const lastMessage = this.lastAiConversationMessage();
        const conversationCounter = lastMessage
            ? lastMessage.conversationCounter + 1
            : 0;

        const message: AiMessageDto = {
            externalId: `aim_${getBase62Guid()}`,
            message: messageEvent.markdown,
            createdAt: new Date().toISOString(),
            createdBy: this._auth.userEmail(),
            authorType: 'human',
            aiModel: selectedModel,
            conversationCounter: conversationCounter,
            includes: []
        };

        pushItems(this.activeAiConversationMessages, message);

        let wasConversationJustAdded = this.activeAiConversationMessages().length == 1;

        if (wasConversationJustAdded) {
            unshiftItems(this.aiConversations, conversation);
        }

        conversation.conversationCounter.set(conversationCounter);
        conversation.status.set('waits-for-ai-response');

        this.isAiMessageBeingProcessed.set(true);

        try {
            await this.operations().sendAiFileMessage(
                this.file().externalId, {
                messageExternalId: message.externalId,
                fileArtifactExternalId: conversation.fileArtifactExternalId,
                conversationExternalId: conversation.aiConversationExternalId,
                conversationCounter: conversationCounter,
                aiIntegrationExternalId: selectedIntegration.externalId,
                aiModel: selectedModel,
                message: message.message,
                includes: includes
            }
            );

            this.subscribeConversationsForStatusCheck(
                conversation);
        } catch (error) {
            console.error(error);
            removeItem(this.activeAiConversationMessages, message);
            messageEvent.onFailureCallback();
            this.isAiMessageBeingProcessed.set(false);

            conversation.conversationCounter.update(counter => counter - 1);
            conversation.status.set('all-messages-read');

            if (wasConversationJustAdded) {
                removeItem(this.aiConversations, conversation);
            }
        }
    }

    onChatGptIntegrationChange(integration: ChatGptIntegration) {
        this.selectedChatGptIntegration.set(integration);
        this.selectedChatGptModelAlias.set(integration.defaultModel);
        
        // After model change, filter out incompatible content types
        this.updateSelectedContentTypesBasedOnModel();
    }
    
    onChatGptModelChange(modelAlias: string) {
        this.selectedChatGptModelAlias.set(modelAlias);
        
        // After model change, filter out incompatible content types
        this.updateSelectedContentTypesBasedOnModel();
    }

    async onAiConversationDeleted(conversation: AppAiConversation, operation: OptimisticOperation) {
        const removal = removeItem(this.aiConversations, conversation);

        const result = await operation.wait();

        if (result.type === 'failure') {
            insertItem(this.aiConversations, conversation, removal.index);
        }
    }

    onAiConversationPrefetch(conversation: AppAiConversation) {
        this.operations().prefetchAiMessages(
            this.fileExternalId(),
            conversation.fileArtifactExternalId
        );
    }

    async onAiConversationOpened(conversation: AppAiConversation) {
        const aiIntegrations = this.chatGptIntegrations();

        const selectedIntegration = aiIntegrations.find(i => i.externalId === conversation.aiIntegrationExternalId)
            ?? aiIntegrations[0];

        const result = await this.operations().getAllAiMessages(
            this.fileExternalId(),
            conversation.fileArtifactExternalId);

        this.resetAiActiveConversation(
            selectedIntegration,
            result.messages);

        const conversationCounter = result.messages
            .at(-1)?.conversationCounter ?? -1;

        conversation.status.set('all-messages-read');
        conversation.conversationCounter.set(conversationCounter);
        conversation.name.set(result.conversationName);

        this.activeAiConversation.set(conversation);
    }

    private subscribeConversationsForStatusCheck(...conversations: AppAiConversation[]) {
        for (let index = 0; index < conversations.length; index++) {
            const conversation = conversations[index];

            const subscription = this
                ._aiConversationsStatusService
                .subscribe(
                    conversation,
                    (aiConversationExternalId: string) => this.markAiConversationAsHasNewMessages(aiConversationExternalId));

            if (subscription) {
                this.aiConversationStatusSubscriptions.push(subscription);
            }
        }
    }

    private async markAiConversationAsHasNewMessages(aiConversationExternalId: string) {
        const conversations = this.aiConversations();

        for (const conversation of conversations) {
            if (conversation.aiConversationExternalId === aiConversationExternalId) {
                conversation.status.set('has-new-messages-to-read');
            }
        }

        const activeConversation = this.activeAiConversation();

        if (activeConversation?.aiConversationExternalId === aiConversationExternalId) {
            const lastMessage = this.lastAiConversationMessage();
            const currentConversationCounter = lastMessage?.conversationCounter ?? -1;

            const result = await this.operations().getAiMessages(
                this.fileExternalId(),
                activeConversation.fileArtifactExternalId,
                currentConversationCounter + 1);

            activeConversation.name.set(result.conversationName);
            activeConversation.status.set('all-messages-read');
            this.activeAiConversationMessages.update(messages => [...messages, ...result.messages]);
        }
    }

    getFileLackOfCompatibilityReason(fileExtension: string, fileSizeInBytes: number): string {
        const model = this.selectedChatGptModel();
        if (!model) return '';

        const fileType = getFileDetails(fileExtension).type;
        const isTypeSupported = model.supportedFileTypes.includes(fileType);
        const isSizeSupported = fileSizeInBytes <= model.maxIncludeSizeInBytes;

        if (isTypeSupported && isSizeSupported) {
            return 'ok';
        } else if (!isTypeSupported) {
            return `wrong extension`;
        } else {
            return `too large (${StorageSizeUtils.formatSize(fileSizeInBytes)} > ${StorageSizeUtils.formatSize(model.maxIncludeSizeInBytes)})`;
        }
    }

    private updateSelectedContentTypesBasedOnModel() {
        const currentlySelected = this.selectedAiContentTypes();
        const allowedOptions = this.filteredSelectableContentTypes();
        
        const filteredSelection = currentlySelected.filter(option => 
            allowedOptions.includes(option)
        );
        
        if (filteredSelection.length !== currentlySelected.length) {
            this.selectedAiContentTypes.set(filteredSelection);
        }
    }

    onAiResponseAttachmentCreated(attachment: AttachmentCreatedEvent) {
        const file: AppFileItem = {
            type: 'file',
        
            externalId: attachment.externalId,
            extension: attachment.extension,
            name: signal(attachment.name),
            sizeInBytes: attachment.sizeInBytes,

            folderExternalId: null,
            folderPath: null,
        
            isCut: signal(false),
            isHighlighted: signal(false),
            isLocked: signal(false),
            isNameEditing: signal(false),
            isSelected: signal(false),
            wasUploadedByUser: true
        };

        this.attachments.update(attachments => [...attachments, file]);
    }
}