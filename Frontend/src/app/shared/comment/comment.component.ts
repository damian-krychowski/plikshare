import { Component, computed, input, OnInit, output, signal, ViewChild, ViewEncapsulation, WritableSignal } from "@angular/core";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatTooltipModule } from "@angular/material/tooltip";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { LEXICAL_EMPTY_AFTER_RESET, LEXICAL_EMPTY_JSON, LexicalEditorWrapperComponent } from "../lexical/lexical-editor-wrapper.component";
import { EditorState } from "lexical";
import { AuthService } from "../../services/auth.service";
import { getBase62Guid } from "../../services/guid-base-62";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { RelativeTimeComponent } from "../relative-time/relative-time.component";

export type AppComment = {
    externalId: string;
    json: WritableSignal<string>;
    createdBy: string;
    createdAt: string;
    wasEdited: WritableSignal<boolean>;
}

@Component({
    selector: 'app-comment',
    imports: [
        MatTooltipModule,
        MatSlideToggleModule,
        LexicalEditorWrapperComponent,
        ActionButtonComponent,
        ConfirmOperationDirective,
        RelativeTimeComponent
    ],
    templateUrl: './comment.component.html',
    styleUrl: './comment.component.scss',
    encapsulation: ViewEncapsulation.None
})
export class CommentComponent implements OnInit {
    comment = input<AppComment>();

    sent = output<AppComment>();
    edited = output();
    deleted = output();

    wasEdited = computed(() => this.comment()?.wasEdited() ?? false);
    
    isUserAuthor = computed(() => {
        const comment = this.comment();

        if(!comment) return false;
        return comment.createdBy == this.auth.userEmail();
    });
    
    json = signal<string | null>(null);
    private _originalJson: string | null = null;

    isEmpty = signal(true);
    isBeingEdited = signal(false);

    @ViewChild('editor') lexicalEditor: LexicalEditorWrapperComponent | null = null;

    constructor(
        public auth: AuthService
    ) {}

    ngOnInit(): void {
        const comment = this.comment();

        if(comment) {
            this.json.set(comment.json());
        }
    }

    async editorStateChanged(state: EditorState) {        
        const json = JSON.stringify(state.toJSON());
        const isEmpty = json === LEXICAL_EMPTY_JSON || json === LEXICAL_EMPTY_AFTER_RESET;
        
        this.isEmpty.set(isEmpty);
        this.json.set(json);
    }

    sendComment() {
        if(this.isEmpty())
            return;

        const currentJson = this.json();

        if(!currentJson)
            return;

        const comment: AppComment = {
            externalId: `fa_${getBase62Guid()}`,
            json: signal(currentJson),
            createdAt: new Date().toISOString(),
            createdBy: this.auth.userEmail(),
            wasEdited: signal(false)
        };

        this.sent.emit(comment);
        this.lexicalEditor?.resetState();
        this.isEmpty.set(true);
    }

    deleteComment() {
        this.deleted.emit();
    }

    startEdit() {
        this.isBeingEdited.set(true);
        this._originalJson = this.json();
    }

    cancelEdit() {
        this.isBeingEdited.set(false);
        this.json.set(this._originalJson);
    }

    confirmEdit() {
        const comment = this.comment();

        if(!comment)
            return;

        comment.json.set(this.json() ?? '');
        comment.wasEdited.set(true);

        this.edited.emit();
        this.isBeingEdited.set(false);
    }
}