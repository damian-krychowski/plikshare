import { Component, computed, input, output, signal, Signal, WritableSignal } from "@angular/core";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { EditableTxtComponent } from "../editable-txt/editable-txt.component";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { RelativeTimeComponent } from "../relative-time/relative-time.component";
import { UpdateAiConversationNameRequest } from "../../services/folders-and-files.api";
import { Operations, OptimisticOperation } from "../../services/optimistic-operation";
import { PrefetchDirective } from "../prefetch.directive";

export type AiConversationStatus = 'waits-for-ai-response' | 'has-new-messages-to-read' | 'all-messages-read'

export type AppAiConversation = {
    fileArtifactExternalId: string;
    aiConversationExternalId: string;
    name: WritableSignal<string | null>;
    aiIntegrationExternalId: string;
    createdAt: string;
    createdBy: string;
    conversationCounter: WritableSignal<number>;

    isNameEditing: WritableSignal<boolean>;
    status: WritableSignal<AiConversationStatus>;
}

export type AiConversationOperations = {
    updateAiConversationName(fileArtifactExternalId: string, request: UpdateAiConversationNameRequest): Promise<void>;
    deleteAiConversation(fileArtifactExternalId: string): Promise<void>;
}

@Component({
    selector: 'app-ai-conversation-item',
    imports: [
        ConfirmOperationDirective,
        EditableTxtComponent,
        ActionButtonComponent,
        RelativeTimeComponent,
        PrefetchDirective
    ],
    templateUrl: './ai-conversation-item.component.html',
    styleUrl: './ai-conversation-item.component.scss'
})
export class AiConversationItemComponent {
    conversation = input.required<AppAiConversation>();
    operations = input.required<AiConversationOperations>();
    activeConversationExternalId = input<string>();

    deleted = output<OptimisticOperation>();
    clicked = output<void>();
    continued = output<void>();
    prefetchRequested = output<void>();
    
    isNameEditing = computed(() => this.conversation().isNameEditing());
    isActive = computed(() => this.activeConversationExternalId() == this.conversation().aiConversationExternalId);
    
    areActionsVisible = signal(false);

    async delete() {
        const conversation = this.conversation();

        const operation = Operations.optimistic();
        this.deleted.emit(operation);

        try {
            await this.operations().deleteAiConversation(conversation.fileArtifactExternalId);
            operation.succeeded();

        } catch (error) {
            console.error(error);
            operation.failed(error);
        }
    }

    async saveName(newName: string) {
        const conversation = this.conversation();
        const oldName = conversation.name();
        conversation.name.set(newName);
        
        try {
            await this.operations().updateAiConversationName(
                conversation.fileArtifactExternalId, {
                name: newName
            });            
        } catch (err: any) {
            if(err.error?.code == 'storage-name-is-not-unique') {
                conversation.name.set(oldName);
            } else {
                console.error(err);
            }
        }
    }

    editName() {
        this.conversation().isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }

    toggleActions() {
        this.areActionsVisible.update(value => !value);
    }

    onClicked() {
        this.clicked.emit();
    }

    continueConversation() {
        this.continued.emit();
    }
}