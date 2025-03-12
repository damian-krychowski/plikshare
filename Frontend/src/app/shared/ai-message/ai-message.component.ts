import { Component, input, OnInit, output, signal, ViewChild, ViewEncapsulation } from "@angular/core";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatTooltipModule } from "@angular/material/tooltip";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { LEXICAL_EMPTY_AFTER_RESET, LEXICAL_EMPTY_JSON, LexicalEditorWrapperComponent } from "../lexical/lexical-editor-wrapper.component";
import { EditorState } from "lexical";
import { RelativeTimeComponent } from "../relative-time/relative-time.component";
import { AiMessageAuthorType } from "../../services/folders-and-files.api";


export type AiMessageSentEvent = {
    markdown: string;
    onFailureCallback: () => void;
}
 
@Component({
    selector: 'app-ai-message',
    imports: [
        MatTooltipModule,
        MatSlideToggleModule,
        LexicalEditorWrapperComponent,
        ActionButtonComponent,
        RelativeTimeComponent
    ],
    templateUrl: './ai-message.component.html',
    styleUrl: './ai-message.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class AiMessageComponent {
    markdown = input<string>();
    createdBy = input<string>();
    createdAt = input<string>();

    sent = output<AiMessageSentEvent>();
    
    json = signal<string | null>(null);
    isEmpty = signal(true);

    @ViewChild('editor') lexicalEditor: LexicalEditorWrapperComponent | null = null;

    constructor() {}

    async editorStateChanged(state: EditorState) {        
        const json = JSON.stringify(state.toJSON());
        const isEmpty = json === LEXICAL_EMPTY_JSON || json === LEXICAL_EMPTY_AFTER_RESET;
        
        this.isEmpty.set(isEmpty);
        this.json.set(json);
    }

    sendMessage() {
        if(this.isEmpty())
            return;

        const currentJson = this.json();

        if(!currentJson)
            return;
        
        const markdown =  this.lexicalEditor?.getMarkdownState() ?? '';

        this.lexicalEditor?.resetState();
        this.isEmpty.set(true);

        this.sent.emit({
            markdown: markdown,
            onFailureCallback: () => {
                this.json.set(currentJson);
                this.isEmpty.set(false);
            }
        });
    }
}