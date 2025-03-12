import { Component, input, model, output, signal, ViewChild } from "@angular/core";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatTooltipModule } from "@angular/material/tooltip";
import { EditorState } from "lexical";
import { Debouncer } from "../../services/debouncer";
import { LEXICAL_EMPTY_JSON, LexicalEditorWrapperComponent } from "../lexical/lexical-editor-wrapper.component";

type RichTextEditorState = 'nothing-changed' | 'saved' | 'typing' | 'saving';

@Component({
    selector: 'app-rich-text-editor',
    imports: [
        MatTooltipModule,
        MatSlideToggleModule,
        LexicalEditorWrapperComponent
    ],
    templateUrl: './rich-text-editor.component.html',
    styleUrl: './rich-text-editor.component.scss'
})
export class RichTextEditorComponent {
    json = model<string>();
    isToolbarFloating = input(false);

    placeholder = input("Enter some rich text...");
    updateOperation = input.required<(json: string, html: string) => Promise<void>>();
    
    isLoadingChanged = output<boolean>();
    
    isLoading = signal(false);

    state = signal<RichTextEditorState>('nothing-changed');
    showBadge = signal(false);
    
    @ViewChild('editor') lexicalEditor: LexicalEditorWrapperComponent | null = null;

    private _savingDebouncer = new Debouncer(1000);
    private _noTypingDebouncer = new Debouncer(500);
    private _badgeTimer: any;

    private startBadgeTimer() {
        // Cancel any existing timer
        if (this._badgeTimer) {
            clearTimeout(this._badgeTimer);
        }

        // Show the badge
        this.showBadge.set(true);

        // Set timer to hide the badge after 10 seconds
        this._badgeTimer = setTimeout(() => {
            this.showBadge.set(false);
        }, 3000);
    }

    async editorStateChanged(state: EditorState) {
        const currentJson = JSON.stringify(state.toJSON());
        const originalJson = this.json();

        const hasJsonChanged = originalJson == null
            ? currentJson != LEXICAL_EMPTY_JSON
            : currentJson != originalJson;
                
        if(hasJsonChanged) {
            this.setStage('typing');
            this._savingDebouncer.debounce(() => this.save());
        } else {
            this._savingDebouncer.cancel();
        }

        if(this.state() == 'typing') {
            this._noTypingDebouncer.debounce(() => this.state.update(value => {
                if(value == 'saved' || value == 'saving')
                    return value;

                if(this._savingDebouncer.isOn())
                    return 'saving';

                return 'nothing-changed';
            }));
        }
    }

    private async save() {
        const editorSaveState = this.lexicalEditor?.getSaveState();

        if(!editorSaveState)
            return;

        if(editorSaveState.json === this.json()) {
            this.setStage('nothing-changed');
            return;
        }

        const originalJsonBuffer = this.json();

        this.json.set(editorSaveState.json);

        try {
            this.setIsLoading(true);
            this.setStage('saving');

            await this.updateOperation()(
                editorSaveState.json, 
                editorSaveState.html);

            this.setStage('saved');
        } catch (error) {
            console.error(error);
            
            this.json.set(originalJsonBuffer);
        } finally {
            this.setIsLoading(false);
        }        
    }

    private setIsLoading(value: boolean) {
        this.isLoading.set(value);
        this.isLoadingChanged.emit(value);
    }

    private setStage(state: RichTextEditorState) {
        this.state.set(state);
        this.startBadgeTimer();
    }
}